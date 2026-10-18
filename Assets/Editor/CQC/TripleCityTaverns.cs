#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class TripleCityTaverns
 {
  const string Request="Assets/Editor/CQC/TripleCityTaverns.request.txt";
  static double next;static string folder;static Vector3 town;static Bounds castle;
  class Road{public Vector3 A,B;public float Half;}
  static List<Road> roads;static List<Bounds> occupied;static List<Bounds> cliffs;
  static TripleCityTaverns(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   if(EditorApplication.isPlaying)return;
   if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TripleCityTaverns-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}
  }
  [MenuItem("Tools/Buildings/Make All Lower City Taverns Three Times Larger")]
  public static void Apply()
  {
   var scene=SceneManager.GetActiveScene();var city=GameObject.Find("Lower Castle City");var mountain=GameObject.Find("Castle Mountain");var player=GameObject.Find("PlayerArmature");
   if(!city||!mountain||!player)throw new InvalidOperationException("City, castle and player required.");
   if(city.transform.Find("Taverns Enlarged 3x"))return;
   var group=city.transform.Find("House Neighborhoods");var houseList=group.Cast<Transform>().Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).OrderByDescending(t=>BoundsOf(t.gameObject).size.x*BoundsOf(t.gameObject).size.z).ToArray();
   if(houseList.Length==0)throw new InvalidOperationException("No city houses found.");
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-triple-taverns-"+stamp+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
   folder="Assets/CityExpansion/TripleTaverns-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var slope=mountain.transform.Find("Rocky Mountain Slopes");castle=BoundsOf(slope?slope.gameObject:mountain);
   var lift=mountain.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c&&c.GetType().Name=="CastleFunicular");
   Vector3 station=new SerializedObject(lift).FindProperty("LowerStation").vector3Value;town=new Vector3(station.x+42,0,station.z-32);
   var cc=player.GetComponent<CharacterController>();float playerHeight=cc.height*Mathf.Abs(player.transform.lossyScale.y),minimumHeight=Mathf.Max(14.4f,playerHeight*8);
   roads=new List<Road>();
   foreach(var filter in city.transform.Find("Winding Roads and Market Square").GetComponentsInChildren<MeshFilter>())
   {
    if(!filter.name.Contains("Street")&&!filter.name.Contains("Loop")&&!filter.name.Contains("Road"))continue;
    Vector3[] v=filter.sharedMesh.vertices;
    for(int i=0;i+3<v.Length;i+=2){Vector3 a=filter.transform.TransformPoint((v[i]+v[i+1])*.5f),b=filter.transform.TransformPoint((v[i+2]+v[i+3])*.5f);if(Vector3.Distance(a,b)>12)continue;roads.Add(new Road{A=a,B=b,Half=Vector3.Distance(filter.transform.TransformPoint(v[i]),filter.transform.TransformPoint(v[i+1]))*.5f});}
   }
   cliffs=new List<Bounds>();
   foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()))
   {
    if(t.IsChildOf(mountain.transform)||!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))continue;
    string n=t.name.ToLowerInvariant();if(!(n.Contains("cliff")||n.Contains("mountain")||n.Contains("rock")))continue;
    Bounds b=BoundsOf(t.gameObject);if(b.size.x>20||b.size.z>20)cliffs.Add(b);
   }
   occupied=new List<Bounds>();var blacksmiths=city.transform.Find("Three Outside Castle Blacksmiths");if(blacksmiths)foreach(Transform t in blacksmiths)occupied.Add(BoundsOf(t.gameObject));var details=city.transform.Find("Market and Street Details");if(details)foreach(Transform t in details)if(t.GetComponentsInChildren<Renderer>().Length>0)occupied.Add(BoundsOf(t.gameObject));int moved=0,colliders=0;float lowest=float.PositiveInfinity;
   var foundations=group.Cast<Transform>().Where(t=>t.name.StartsWith("Stone House Foundation")).ToArray();
   var material=foundations.FirstOrDefault()?.GetComponent<Renderer>().sharedMaterial;
   foreach(var foundation in foundations)Undo.DestroyObjectImmediate(foundation.gameObject);
   int index=0;
   foreach(var house in houseList)
   {
    Bounds old=BoundsOf(house.gameObject);Vector3 original=old.center;original.y=0;Undo.RecordObject(house,"Enlarge city house");
    house.localScale*=3f;
    Bounds enlarged=BoundsOf(house.gameObject);float radius=new Vector2(enlarged.extents.x,enlarged.extents.z).magnitude;
    var shape=Footprint(house.gameObject);var shapeCenter=new Vector2(enlarged.center.x,enlarged.center.z);int[] rejects=new int[5];Vector3 chosen=Vector3.zero;bool found=false;float best=float.PositiveInfinity;
    // Find a nearby larger lot; preserve the marked approach district and the cobblestone streets.
    for(int ring=0;ring<=280;ring++)
    {
     int samples=ring==0?1:96;
     for(int j=0;j<samples;j++)
     {
      float angle=(j+.23f*(index%3))*Mathf.PI*2/samples;Vector3 p=original+new Vector3(Mathf.Cos(angle)*ring*4,0,Mathf.Sin(angle)*ring*4);
      if(CastleContains(p,radius*.25f)||Distance(p,town)<23+radius||Distance(p,station)<10+radius)continue;
      float ground=Ground(p.x,p.z);if(float.IsNaN(ground)){rejects[0]++;continue;}
      var footprint=new Bounds(new Vector3(p.x,ground+enlarged.extents.y,p.z),enlarged.size);
      if(cliffs.Any(b=>Overlap(b,footprint,1))){rejects[1]++;continue;}if(occupied.Any(b=>Overlap(b,footprint,1.2f))){rejects[2]++;continue;}var offset=new Vector2(p.x,p.z)-shapeCenter;if(CutsRoad(shape,offset)){rejects[3]++;continue;}
      float low=ground,high=ground;foreach(var c in Corners(footprint)){float h=Ground(c.x,c.z);if(float.IsNaN(h)){high=float.PositiveInfinity;break;}low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
      if(high-low>1.2f){rejects[4]++;continue;}float cost=Distance(p,original)+Mathf.Max(0,RoadClearance(p)-radius-8)*.4f;
      if(cost<best){best=cost;chosen=new Vector3(p.x,high+.05f,p.z);found=true;}
     }
     if(found&&ring>=3)break;
    }
    if(!found)throw new InvalidOperationException("No clear enlarged lot for "+house.name+". Candidate rejection counts [no terrain, cliffs, buildings, roads, slope]: "+string.Join(",",rejects)+". Scene changes rolled back; backup retained.");
    Bounds current=BoundsOf(house.gameObject);house.position+=new Vector3(chosen.x-current.center.x,chosen.y-current.min.y,chosen.z-current.center.z);current=BoundsOf(house.gameObject);
    if(Distance(current.center,original)>1)moved++;occupied.Add(current);lowest=Mathf.Min(lowest,current.size.y);
    foreach(var collider in house.GetComponentsInChildren<Collider>())Undo.DestroyObjectImmediate(collider);
    foreach(var filter in house.GetComponentsInChildren<MeshFilter>())
    {
     if(!filter.sharedMesh||filter.sharedMesh.vertexCount<3)continue;
     var collider=Undo.AddComponent<MeshCollider>(filter.gameObject);collider.sharedMesh=filter.sharedMesh;collider.convex=false;colliders++;
    }
    float min=chosen.y;foreach(var c in Corners(current))min=Mathf.Min(min,Ground(c.x,c.z));
    var pad=GameObject.CreatePrimitive(PrimitiveType.Cube);pad.name="Stone House Foundation "+(++index);pad.transform.SetParent(group,false);pad.transform.position=new Vector3(current.center.x,(min+chosen.y)*.5f,current.center.z);pad.transform.localScale=new Vector3(current.size.x+.15f,Mathf.Max(.1f,chosen.y-min),current.size.z+.15f);if(material)pad.GetComponent<Renderer>().sharedMaterial=material;Undo.RegisterCreatedObjectUndo(pad,"Resize house foundation");
    PrefabUtility.RecordPrefabInstancePropertyModifications(house);foreach(var r in house.GetComponentsInChildren<Renderer>())EditorUtility.SetDirty(r);
   }
   int removed=ClearNewLots(scene,city);var marker=new GameObject("Taverns Enlarged 3x");marker.transform.SetParent(city.transform,false);Undo.RegisterCreatedObjectUndo(marker,"Remember tavern scale change");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();
   Directory.CreateDirectory("Logs");if(File.Exists("Logs/TripleCityTaverns-error.txt"))File.Delete("Logs/TripleCityTaverns-error.txt");File.WriteAllText("Logs/TripleCityTaverns-report.txt","Enlarged all "+houseList.Length+" lower city taverns and house variants to exactly 3x their previous uniform scale. Door openings and mesh colliders scale with buildings; player capsule height "+playerHeight.ToString("F2")+"m. Relocated "+moved+" buildings to supported larger lots across available terrain with oriented road footprint and house/cliff clearance; widest buildings placed first. Minimum building height "+lowest.ToString("F2")+"m. Matching foundations adjusted; "+colliders+" mesh colliders preserve modeled openings. Existing castle, roads, blacksmiths and terrain textures retained. Vegetation cleared only on enlarged building lots: "+removed+". Marker prevents repeated scale multiplication.\nBackup: "+backup);
   Debug.Log("All lower city taverns enlarged 3x. Scene saved.");
  }
  static bool InTown(Vector3 p){float x=(p.x-town.x)/340,z=(p.z-town.z)/230;return x*x+z*z<1;}
  static bool CastleContains(Vector3 p,float pad){return p.x>castle.min.x-pad&&p.x<castle.max.x+pad&&p.z>castle.min.z-pad&&p.z<castle.max.z+pad;}
  static float Distance(Vector3 a,Vector3 b){return Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));}
  static float Ground(float x,float z){foreach(var t in Terrain.activeTerrains){Vector3 p=t.transform.position,size=t.terrainData.size;if(x>=p.x&&x<=p.x+size.x&&z>=p.z&&z<=p.z+size.z)return p.y+t.terrainData.GetInterpolatedHeight((x-p.x)/size.x,(z-p.z)/size.z);}return float.NaN;}
  static bool CutsRoad(Vector2[] footprint,Vector2 offset)
  {
   var points=footprint.Select(v=>v+offset).ToArray();float xmin=points.Min(v=>v.x),xmax=points.Max(v=>v.x),zmin=points.Min(v=>v.y),zmax=points.Max(v=>v.y);
   foreach(var road in roads){float half=road.Half+.7f;var a=new Vector2(road.A.x,road.A.z);var b=new Vector2(road.B.x,road.B.z);if(Mathf.Max(a.x,b.x)+half<xmin||Mathf.Min(a.x,b.x)-half>xmax||Mathf.Max(a.y,b.y)+half<zmin||Mathf.Min(a.y,b.y)-half>zmax)continue;var d=(b-a).normalized;var normal=new Vector2(-d.y,d.x)*half;var poly=new[]{a-normal,a+normal,b+normal,b-normal};if(Intersects(points,poly))return true;}return false;
  }
  static bool Intersects(Vector2[] a,Vector2[] b){foreach(var polygon in new[]{a,b})for(int i=0;i<polygon.Length;i++){var edge=polygon[(i+1)%polygon.Length]-polygon[i];if(edge.sqrMagnitude<.00001f)continue;var axis=new Vector2(-edge.y,edge.x).normalized;float amin=float.PositiveInfinity,amax=float.NegativeInfinity,bmin=amin,bmax=amax;foreach(var p in a){float v=Vector2.Dot(axis,p);amin=Mathf.Min(amin,v);amax=Mathf.Max(amax,v);}foreach(var p in b){float v=Vector2.Dot(axis,p);bmin=Mathf.Min(bmin,v);bmax=Mathf.Max(bmax,v);}if(amax<bmin||bmax<amin)return false;}return true;}
  static Vector2[] Footprint(GameObject go){Bounds local=new Bounds();bool first=true;foreach(var f in go.GetComponentsInChildren<MeshFilter>()){if(!f.sharedMesh)continue;var b=f.sharedMesh.bounds;for(int k=0;k<8;k++){var point=go.transform.InverseTransformPoint(f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));if(first){local=new Bounds(point,Vector3.zero);first=false;}else local.Encapsulate(point);}}var result=new Vector2[4];var corners=new[]{new Vector3(local.min.x,0,local.min.z),new Vector3(local.max.x,0,local.min.z),new Vector3(local.max.x,0,local.max.z),new Vector3(local.min.x,0,local.max.z)};for(int i=0;i<4;i++){var p=go.transform.TransformPoint(corners[i]);result[i]=new Vector2(p.x,p.z);}return result;}
  static float RoadClearance(Vector3 p){float d=float.PositiveInfinity;foreach(var r in roads){Vector2 a=new Vector2(r.A.x,r.A.z),v=new Vector2(r.B.x-r.A.x,r.B.z-r.A.z),w=new Vector2(p.x,p.z)-a;d=Mathf.Min(d,(w-v*Mathf.Clamp01(Vector2.Dot(w,v)/Mathf.Max(.001f,v.sqrMagnitude))).magnitude-r.Half);}return d;}
  static bool Overlap(Bounds a,Bounds b,float gap){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+gap&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+gap;}
  static Bounds BoundsOf(GameObject go){bool found=false;Bounds b=new Bounds();foreach(var r in go.GetComponentsInChildren<Renderer>()){if(!found){b=r.bounds;found=true;}else b.Encapsulate(r.bounds);}return b;}
  static IEnumerable<Vector3> Corners(Bounds b){yield return new Vector3(b.min.x,0,b.min.z);yield return new Vector3(b.max.x,0,b.min.z);yield return new Vector3(b.max.x,0,b.max.z);yield return new Vector3(b.min.x,0,b.max.z);}
  static bool OnHouse(Vector3 p){return occupied.Any(b=>Mathf.Abs(p.x-b.center.x)<b.extents.x+1.5f&&Mathf.Abs(p.z-b.center.z)<b.extents.z+1.5f);}
  static int ClearNewLots(Scene scene,GameObject city)
  {
   int count=0;foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray())
   {if(!t||t.IsChildOf(city.transform)||!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))continue;string n=t.name.ToLowerInvariant();if(!(n.Contains("spruce")||n.Contains("tree")||n.Contains("bush")||n.Contains("standard_rock")))continue;Bounds b=BoundsOf(t.gameObject);if(!OnHouse(b.center)||b.size.x>25||b.size.z>25)continue;Undo.RecordObject(t.gameObject,"Clear enlarged house footprint");t.gameObject.SetActive(false);count++;}
   foreach(var cover in UnityEngine.Object.FindObjectsByType<StarterAssets.ForestGroundCover>())
   {
    if(!cover.Data)continue;var original=cover.Data;var copy=UnityEngine.Object.Instantiate(original);var batches=new List<StarterAssets.ForestGroundCoverData.Batch>();
    foreach(var b in original.Batches){var m=b.Matrices.Where(v=>!OnHouse(v.GetColumn(3))).ToArray();count+=b.Matrices.Length-m.Length;batches.Add(new StarterAssets.ForestGroundCoverData.Batch{Mesh=b.Mesh,DistantMesh=b.DistantMesh,Material=b.Material,Submesh=b.Submesh,Bounds=b.Bounds,Matrices=m});}
    copy.Batches=batches.ToArray();copy.TotalPlants=batches.Sum(b=>b.Matrices.Length);AssetDatabase.CreateAsset(copy,folder+"/Lot-Grass-"+cover.GetEntityId()+".asset");Undo.RecordObject(cover,"Clear enlarged lots");cover.Data=copy;
   }
   return count;
  }
 }
}
#endif
