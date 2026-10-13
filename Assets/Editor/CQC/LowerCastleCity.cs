#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class LowerCastleCity
 {
  const string Request="Assets/Editor/CQC/LowerCastleCity.request.txt",RootName="Lower Castle City";
  static double next;static Vector3 center;static Bounds mountainBounds;static string folder;
  class Street{public string Name;public float Width;public List<Vector3> Points=new List<Vector3>();}
  public static bool Relocating;public static Vector3 RequestedCenter;
  static float radiusX=172,radiusZ=145;static HashSet<Vector2Int> clearedCells;
  static List<Street> streets;static List<Bounds> homes;static List<Bounds> cliffs;static int buildUndo=-1;
  static LowerCastleCity(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   if(SceneManager.GetActiveScene().name!="MyForest")return;
   try{Build();AssetDatabase.DeleteAsset(Request);}catch(Exception e){if(buildUndo>=0){Undo.RevertAllDownToGroup(buildUndo);buildUndo=-1;}EditorUtility.ClearProgressBar();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/LowerCastleCity-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Buildings/Build Lower Castle City")]
  public static void Build()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode to build the city.");
   var scene=SceneManager.GetActiveScene();if(scene.name!="MyForest")throw new InvalidOperationException("Open MyForest first.");
   if(GameObject.Find(RootName)){Debug.Log("Lower Castle City already exists; existing placements preserved.");return;}
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");
   var slope=mountain.transform.Find("Rocky Mountain Slopes");mountainBounds=BoundsOf(slope?slope.gameObject:mountain);
   cliffs=new List<Bounds>();
   foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()))
   {
    if(t.IsChildOf(mountain.transform)||!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))continue;
    string n=t.name.ToLowerInvariant();if(!(n.Contains("cliff")||n.Contains("mountain")||n.Contains("rock")))continue;
    if(t.GetComponentsInChildren<Renderer>().Length==0)continue;var b=BoundsOf(t.gameObject);
    if(b.size.x>20||b.size.z>20)cliffs.Add(b);
   }
   center=new Vector3(mountainBounds.center.x+145,0,mountainBounds.center.z-180);
   int best=-1;
   foreach(var offset in new[]{new Vector3(145,0,-180),new Vector3(170,0,-240),new Vector3(220,0,-120),new Vector3(145,0,-280)})
   {
    Vector3 candidate=new Vector3(mountainBounds.center.x,0,mountainBounds.center.z)+offset;int score=0;
    for(int x=-4;x<=4;x++)for(int z=-4;z<=4;z++){Vector3 spot=candidate+new Vector3(x*30,0,z*25);if(!float.IsNaN(Ground(spot.x,spot.z))&&!MountainContains(spot,5)&&!cliffs.Any(b=>FlatDistance(spot,b)<10))score++;}
    if(score>best){best=score;center=candidate;}
   }
   if(Relocating){center=RequestedCenter;radiusX=170;radiusZ=102;}else{radiusX=172;radiusZ=145;}
   var paths=new[]{"Assets/Building/ImageToStl.com_tavern(1).fbx","Assets/Building/ImportedTaverns/MedievalTavern/MedievalTavern.prefab","Assets/Building/ImportedTaverns/TallTavern/TallTavern.prefab"};
   var prefabs=paths.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();
   if(prefabs.Any(p=>!p))throw new InvalidOperationException("All three existing house assets are required.");
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-lower-city-"+stamp+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
   Directory.CreateDirectory("Assets/CityExpansion");folder="Assets/CityExpansion/Generated-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   Undo.IncrementCurrentGroup();buildUndo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Build lower castle city");
   var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Build lower castle city");
   var roads=Group(root.transform,"Winding Roads and Market Square");var houses=Group(root.transform,"House Neighborhoods");var decorations=Group(root.transform,"Market and Street Details");
   streets=new List<Street>();homes=new List<Bounds>();
   var roadSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/Building/Cobblestone/CastlePavingStone01.mat");
   var roadMat=roadSource?new Material(roadSource):new Material(Shader.Find("Universal Render Pipeline/Lit"));roadMat.name="City Cobblestone Roads";roadMat.SetColor("_BaseColor",new Color(.73f,.73f,.70f));roadMat.SetFloat("_Smoothness",.12f);roadMat.SetFloat("_Parallax",0);roadMat.DisableKeyword("_PARALLAXMAP");roadMat.enableInstancing=true;AssetDatabase.CreateAsset(roadMat,folder+"/CityRoad.mat");
   var stone=Material("Foundation Stone",new Color(.38f,.36f,.31f));var wood=Material("Market Timber",new Color(.28f,.15f,.07f));
   // Irregular curved neighborhood loops with connecting spokes, not a square grid.
   for(int ring=0;ring<(Relocating?4:3);ring++)
   {
    float rx=(Relocating?new[]{35f,70f,110f,145f}:new[]{48f,94f,145f})[ring],rz=(Relocating?new[]{18f,38f,62f,84f}:new[]{40f,79f,119f})[ring];var anchors=new List<Vector3>();
    for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16;float variation=1+.07f*Mathf.Sin(a*3+ring)+.04f*Mathf.Cos(a*5-ring);anchors.Add(center+new Vector3(Mathf.Cos(a)*rx*variation,0,Mathf.Sin(a)*rz*variation));}
    AddStreet("Neighborhood Loop "+(ring+1),Relocating?(ring==1?4.5f:3.5f):(ring==1?6:4.5f),Smooth(anchors,true));
   }
   for(int spoke=0;spoke<8;spoke++)
   {
    float a=spoke*Mathf.PI*2/8+.12f;var points=new List<Vector3>{center};
    for(int j=1;j<=3;j++){float bend=a+Mathf.Sin(j*1.3f+spoke)*.12f;points.Add(center+new Vector3(Mathf.Cos(bend)*j*50f,0,Mathf.Sin(bend)*j*(Relocating?29f:41f)));}
    AddStreet("Market Street "+(spoke+1),Relocating?(spoke%2==0?5f:3.5f):(spoke%2==0?6.5f:4.5f),Smooth(points,false));
   }
   var lift=mountain.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c && c.GetType().Name=="CastleFunicular");
   if(lift)
   {
    Vector3 start=new SerializedObject(lift).FindProperty("LowerStation").vector3Value;start.z-=7f;start.y=0;
    AddStreet("Funicular Approach Road",7f,Smooth(new List<Vector3>{start,start+new Vector3(28,0,-20),center+new Vector3(-35,0,24),center},false));
   }
   int roadIndex=0;foreach(var street in streets){Ribbon(roads,street,roadMat,roadIndex++);}
   Disc(roads,"Central Market Square",center,18,roadMat);
   var rng=new System.Random(71326);int count=0;int[] styleCounts=new int[3];
   // Lots face their local street, with spacing checks against every road and other house.
   foreach(var street in streets)
   {
    float run=12;
    for(int j=1;j<street.Points.Count;j++)
    {
     Vector3 a=street.Points[j-1],b=street.Points[j];float length=Vector3.Distance(a,b);run+=length;
     if(run<(Relocating?16:22))continue;run=0;
     Vector3 tangent=(b-a).normalized;Vector3 side=Vector3.Cross(Vector3.up,tangent).normalized;
     foreach(int sign in new[]{-1,1})
     {
      int style=count%3;float targetWidth=(Relocating?7.8f:9)+(float)rng.NextDouble()*(Relocating?3:3.5f);
      Vector3 spot=b+side*sign*(street.Width*.5f+targetWidth*.65f+2.5f);
      if(cliffs.Any(b=>FlatDistance(spot,b)<targetWidth)||!Inside(spot)||Vector2.Distance(new Vector2(spot.x,spot.z),new Vector2(center.x,center.z))<27||MountainContains(spot,10))continue;
      if(RoadDistance(spot)<targetWidth*.62f+1.5f||homes.Any(h=>FlatDistance(spot,h)<targetWidth*.75f+1.5f))continue;
      float ground=Ground(spot.x,spot.z);if(float.IsNaN(ground))continue;
      var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[style],houses);
      go.name=new[]{"Classic Tavern ","Medieval House ","Tall Timber House "}[style]+(styleCounts[style]+1).ToString("000");
      go.transform.rotation=Quaternion.LookRotation(-side*sign)*Quaternion.Euler(0,(float)rng.NextDouble()*8-4,0);
      Bounds bounds=BoundsOf(go);float scale=Mathf.Min(targetWidth/Mathf.Max(bounds.size.x,bounds.size.z),(8+(float)rng.NextDouble()*4)/bounds.size.y);go.transform.localScale*=scale;
      bounds=BoundsOf(go);go.transform.position+=new Vector3(spot.x-bounds.center.x,ground+.1f-bounds.min.y,spot.z-bounds.center.z);bounds=BoundsOf(go);
      if(homes.Any(h=>Overlap(h,bounds,1))||RoadDistance(bounds.center)<new Vector2(bounds.extents.x,bounds.extents.z).magnitude+.4f){UnityEngine.Object.DestroyImmediate(go);continue;}
      float maxGround=ground,minGround=ground;bool supported=true;
      foreach(var corner in Corners(bounds)){float h=Ground(corner.x,corner.z);if(float.IsNaN(h)){supported=false;break;}maxGround=Mathf.Max(maxGround,h);minGround=Mathf.Min(minGround,h);}
      if(!supported||maxGround-minGround>1.4f){UnityEngine.Object.DestroyImmediate(go);continue;}
      go.transform.position+=Vector3.up*(maxGround-ground);bounds=BoundsOf(go);
      foreach(var c in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
      AddBox(go);
      Cube(houses,"Stone House Foundation "+(count+1),new Vector3(bounds.center.x,(maxGround+minGround)*.5f+.03f,bounds.center.z),new Vector3(bounds.size.x+.3f,Mathf.Max(.12f,maxGround-minGround+.12f),bounds.size.z+.3f),stone,true);
      PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);homes.Add(bounds);styleCounts[style]++;count++;
     }
    }
   }
   if(count<40)throw new InvalidOperationException("Too few supported, non-overlapping house sites: "+count+". Restore the scene backup before rebuilding.");
   // Small readable town-center features; streets remain clear for the player.
   for(int i=0;i<6;i++)
   {
    float a=(i+.5f)*Mathf.PI*2/8+.12f;Vector3 p=center+new Vector3(Mathf.Cos(a)*14,0,Mathf.Sin(a)*14);p.y=Ground(p.x,p.z)+.15f;
    Cube(decorations,"Market Stall Counter "+(i+1),p+Vector3.up*.75f,new Vector3(2.6f,1.5f,1.3f),wood,true);
    Cube(decorations,"Market Stall Canopy "+(i+1),p+Vector3.up*2.4f,new Vector3(3f,.12f,2f),wood,false);
    foreach(float dx in new[]{-1.3f,1.3f})Cube(decorations,"Stall Post",p+new Vector3(dx,1.2f,0),new Vector3(.12f,2.4f,.12f),wood,false);
   }
   clearedCells=new HashSet<Vector2Int>();
   for(int x=Mathf.FloorToInt((center.x-radiusX-20)/3);x<=Mathf.CeilToInt((center.x+radiusX+20)/3);x++)
    for(int z=Mathf.FloorToInt((center.z-radiusZ-20)/3);z<=Mathf.CeilToInt((center.z+radiusZ+20)/3);z++)
    {
     Vector3 p=new Vector3((x+.5f)*3,0,(z+.5f)*3);
     if(Inside(p)&&!MountainContains(p,2)&&(RoadDistance(p)<2.5f||homes.Any(b=>FlatDistance(p,b)<2.5f)||Vector2.Distance(new Vector2(p.x,p.z),new Vector2(center.x,center.z))<20))clearedCells.Add(new Vector2Int(x,z));
    }
   int cleared=ClearPlants(scene,root);
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
   Undo.CollapseUndoOperations(buildUndo);buildUndo=-1;
   Selection.activeGameObject=root;SceneView.lastActiveSceneView?.Frame(new Bounds(center+Vector3.up*12,new Vector3(350,50,290)),false);SceneView.RepaintAll();
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/LowerCastleCity-report.txt","Built "+count+" houses using all three existing house styles ("+string.Join(", ",styleCounts)+").\n"+streets.Count+" winding streets, market square, six market stalls, funicular approach. Terrain-conforming roads and house foundations; no house/road overlap; minimum 40 supported lots verified.\nVegetation cleared only within new town footprint / street-building sites; surrounding forest preserved. Cleared objects/instances: "+cleared+".\nCity centre: "+center+".\nBackup: "+backup+"\nGenerated assets: "+folder);
   Debug.Log("Lower castle city ready: "+count+" houses, winding cobblestone roads and market square. Scene saved.");
  }
  static Transform Group(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
  static bool Inside(Vector3 p){float x=(p.x-center.x)/radiusX,z=(p.z-center.z)/radiusZ;return x*x+z*z<1;}
  static bool MountainContains(Vector3 p,float pad){return p.x>mountainBounds.min.x-pad&&p.x<mountainBounds.max.x+pad&&p.z>mountainBounds.min.z-pad&&p.z<mountainBounds.max.z+pad;}
  static float Ground(float x,float z){foreach(var t in Terrain.activeTerrains){var p=t.transform.position;var size=t.terrainData.size;if(x>=p.x&&x<=p.x+size.x&&z>=p.z&&z<=p.z+size.z)return p.y+t.terrainData.GetInterpolatedHeight((x-p.x)/size.x,(z-p.z)/size.z);}return float.NaN;}
  static float FlatDistance(Vector3 p,Bounds b){float x=Mathf.Max(0,Mathf.Abs(p.x-b.center.x)-b.extents.x),z=Mathf.Max(0,Mathf.Abs(p.z-b.center.z)-b.extents.z);return Mathf.Sqrt(x*x+z*z);}
  static bool Overlap(Bounds a,Bounds b,float gap){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+gap&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+gap;}
  static IEnumerable<Vector3> Corners(Bounds b){yield return new Vector3(b.min.x,0,b.min.z);yield return new Vector3(b.max.x,0,b.min.z);yield return new Vector3(b.max.x,0,b.max.z);yield return new Vector3(b.min.x,0,b.max.z);}
  static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b){Vector2 v=new Vector2(b.x-a.x,b.z-a.z),w=new Vector2(p.x-a.x,p.z-a.z);float t=Mathf.Clamp01(Vector2.Dot(w,v)/Mathf.Max(v.sqrMagnitude,.0001f));return (w-v*t).magnitude;}
  static float RoadDistance(Vector3 p){float d=float.PositiveInfinity;foreach(var road in streets)for(int j=1;j<road.Points.Count;j++)d=Mathf.Min(d,SegmentDistance(p,road.Points[j-1],road.Points[j])-road.Width*.5f);return d;}
  static void AddStreet(string name,float width,List<Vector3> points){var s=new Street{Name=name,Width=width,Points=points};streets.Add(s);}
  static List<Vector3> Smooth(List<Vector3> anchors,bool loop)
  {
   var result=new List<Vector3>();int n=anchors.Count;
   for(int i=0;i<(loop?n:n-1);i++)
   {
    Vector3 a=anchors[loop?(i+n-1)%n:Mathf.Max(0,i-1)],b=anchors[i],c=anchors[(i+1)%n],d=anchors[loop?(i+2)%n:Mathf.Min(n-1,i+2)];
    int steps=Mathf.Max(3,Mathf.CeilToInt(Vector3.Distance(b,c)/2));
    for(int j=0;j<steps;j++){float t=j/(float)steps;result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}
   }
   result.Add(loop?anchors[0]:anchors[n-1]);return result;
  }
  static void Ribbon(Transform parent,Street road,Material material,int id)
  {
   var verts=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
   for(int j=1;j<road.Points.Count;j++)
   {
    Vector3 a=road.Points[j-1],b=road.Points[j];if(MountainContains((a+b)*.5f,1)||cliffs.Any(c=>FlatDistance((a+b)*.5f,c)<road.Width*.5f))continue;
    Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized)*road.Width*.5f;
    var pts=new[]{a-side,a+side,b-side,b+side};bool valid=true;
    for(int k=0;k<4;k++){float h=Ground(pts[k].x,pts[k].z);if(float.IsNaN(h)){valid=false;break;}pts[k].y=h+.085f+id*.0005f;}
    if(!valid)continue;int start=verts.Count;verts.AddRange(pts);foreach(var p in pts)uv.Add(new Vector2(p.x/3,p.z/3));indices.AddRange(new[]{start,start+2,start+1,start+1,start+2,start+3});
   }
   MeshObject(parent,road.Name,verts.ToArray(),uv.ToArray(),indices.ToArray(),material);
  }
  static void Disc(Transform parent,string name,Vector3 at,float radius,Material mat)
  {
   const int n=96;var v=new Vector3[n+1];var uv=new Vector2[n+1];v[0]=new Vector3(at.x,Ground(at.x,at.z)+.12f,at.z);uv[0]=new Vector2(at.x/3,at.z/3);var tri=new int[n*3];
   for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;Vector3 p=at+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);p.y=Ground(p.x,p.z)+.12f;v[i+1]=p;uv[i+1]=new Vector2(p.x/3,p.z/3);tri[i*3]=0;tri[i*3+1]=(i+1)%n+1;tri[i*3+2]=i+1;}
   MeshObject(parent,name,v,uv,tri,mat);
  }
  static void MeshObject(Transform parent,string name,Vector3[] v,Vector2[] uv,int[] tri,Material mat){if(v.Length==0)return;var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Road-"+name.Replace(" ","-")+".asset");var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;go.AddComponent<MeshCollider>().sharedMesh=mesh;}
  static Material Material(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.1f);m.enableInstancing=true;AssetDatabase.CreateAsset(m,folder+"/"+name.Replace(" ","-")+".mat");return m;}
  static void Cube(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool collide){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!collide)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
  static Bounds BoundsOf(GameObject go){bool found=false;Bounds b=new Bounds();foreach(var r in go.GetComponentsInChildren<Renderer>()){if(!found){b=r.bounds;found=true;}else b.Encapsulate(r.bounds);}if(!found)throw new InvalidOperationException("House has no visible meshes.");return b;}
  static void AddBox(GameObject go){Bounds local=new Bounds();bool first=true;foreach(var f in go.GetComponentsInChildren<MeshFilter>()){if(!f.sharedMesh)continue;Bounds b=f.sharedMesh.bounds;for(int k=0;k<8;k++){Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));p=go.transform.InverseTransformPoint(f.transform.TransformPoint(p));if(first){local=new Bounds(p,Vector3.zero);first=false;}else local.Encapsulate(p);}}var box=go.AddComponent<BoxCollider>();box.center=local.center;box.size=local.size;}
  static bool Cleared(Vector3 p){return clearedCells!=null&&clearedCells.Contains(new Vector2Int(Mathf.FloorToInt(p.x/3),Mathf.FloorToInt(p.z/3)));}
  static bool Occupied(Vector3 p){return Cleared(p)&&(RoadDistance(p)<1||homes.Any(b=>FlatDistance(p,b)<1)||Vector2.Distance(new Vector2(p.x,p.z),new Vector2(center.x,center.z))<20);}
  static int ClearPlants(Scene scene,GameObject city)
  {
   int count=0;
   // Preserve originals by creating independent terrain data only for touched tiles.
   foreach(var terrain in Terrain.activeTerrains)
   {
    var original=terrain.terrainData;Vector3 tp=terrain.transform.position,size=original.size;var bounds=new Bounds(tp+size*.5f,size);
    if(bounds.max.x<center.x-180||bounds.min.x>center.x+180||bounds.max.z<center.z-150||bounds.min.z>center.z+150)continue;
    var copy=UnityEngine.Object.Instantiate(original);copy.name=terrain.name+" City";
    var trees=copy.treeInstances.Where(t=>!Cleared(tp+Vector3.Scale(t.position,size))).ToArray();count+=copy.treeInstanceCount-trees.Length;copy.treeInstances=trees;
    for(int layer=0;layer<copy.detailPrototypes.Length;layer++){var data=copy.GetDetailLayer(0,0,copy.detailWidth,copy.detailHeight,layer);for(int z=0;z<copy.detailHeight;z++)for(int x=0;x<copy.detailWidth;x++){Vector3 p=tp+new Vector3((x+.5f)/copy.detailWidth*size.x,0,(z+.5f)/copy.detailHeight*size.z);if(Cleared(p))data[z,x]=0;}copy.SetDetailLayer(0,0,layer,data);}
    AssetDatabase.CreateAsset(copy,folder+"/Terrain-"+terrain.GetEntityId()+".asset");Undo.RecordObject(terrain,"Clear city terrain plants");terrain.terrainData=copy;var collider=terrain.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Use independent city terrain");collider.terrainData=copy;}
   }
   foreach(var cover in UnityEngine.Object.FindObjectsByType<StarterAssets.ForestGroundCover>())
   {
    if(!cover.Data)continue;var original=cover.Data;var copy=UnityEngine.Object.Instantiate(original);copy.name="City Cleared "+original.name;
    var batches=new List<StarterAssets.ForestGroundCoverData.Batch>();
    foreach(var batch in original.Batches){var matrices=batch.Matrices.Where(m=>!Cleared(m.GetColumn(3))).ToArray();count+=batch.Matrices.Length-matrices.Length;batches.Add(new StarterAssets.ForestGroundCoverData.Batch{Mesh=batch.Mesh,DistantMesh=batch.DistantMesh,Material=batch.Material,Submesh=batch.Submesh,Bounds=batch.Bounds,Matrices=matrices});}
    copy.Batches=batches.ToArray();copy.TotalPlants=batches.Sum(b=>b.Matrices.Length);AssetDatabase.CreateAsset(copy,folder+"/GroundCover-"+cover.GetEntityId()+".asset");Undo.RecordObject(cover,"Clear city ground cover");cover.Data=copy;
   }
   // Scene vegetation objects (tree/bush/small rock prefabs) only, never existing buildings or cliffs.
   foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray())
   {
    if(!t||t.IsChildOf(city.transform)||!t.gameObject.activeInHierarchy||!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))continue;
    string n=t.name.ToLowerInvariant();if(!(n.Contains("spruce")||n.Contains("tree")||n.Contains("bush")||n.Contains("standard_rock")))continue;
    var renderers=t.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;Bounds b=BoundsOf(t.gameObject);if(!Cleared(b.center)||b.size.x>25||b.size.z>25)continue;
    Undo.RecordObject(t.gameObject,"Clear city site vegetation");t.gameObject.SetActive(false);count++;
   }
   // Keep complete vertex attributes when trimming combined grass mesh patches.
   int meshId=0;foreach(var f in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).ToArray())
   {
    if(!f || f.transform.IsChildOf(city.transform)||!f.sharedMesh)continue;
    if(!f.transform.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("Original Dense Grass")||t.name.Contains("Visible Original Grass")))continue;
    var r=f.GetComponent<Renderer>();if(!r||r.bounds.max.x<center.x-180||r.bounds.min.x>center.x+180||r.bounds.max.z<center.z-150||r.bounds.min.z>center.z+150)continue;
    var source=f.sharedMesh;
    using(var input=MeshUtility.AcquireReadOnlyMeshData(source))
    {
     var data=input[0];using(var positions=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp)){data.GetVertices(positions);var kept=new List<int>();var submeshes=new List<SubMeshDescriptor>();int removed=0;
     for(int sub=0;sub<source.subMeshCount;sub++){int start=kept.Count;var desc=data.GetSubMesh(sub);using(var indices=new NativeArray<int>(desc.indexCount,Allocator.Temp)){data.GetIndices(indices,sub,true);for(int i=0;i<indices.Length;i+=3){Vector3 p=f.transform.TransformPoint((positions[indices[i]]+positions[indices[i+1]]+positions[indices[i+2]])/3);if(Cleared(p)){removed++;continue;}kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);}}submeshes.Add(new SubMeshDescriptor(start,kept.Count-start,MeshTopology.Triangles){bounds=source.bounds,vertexCount=data.vertexCount});}
     if(removed==0)continue;
     var writable=Mesh.AllocateWritableMeshData(1);var target=writable[0];target.SetVertexBufferParams(data.vertexCount,source.GetVertexAttributes());for(int stream=0;stream<source.vertexBufferCount;stream++)data.GetVertexData<byte>(stream).CopyTo(target.GetVertexData<byte>(stream));target.SetIndexBufferParams(kept.Count,IndexFormat.UInt32);var buffer=target.GetIndexData<int>();for(int i=0;i<kept.Count;i++)buffer[i]=kept[i];target.subMeshCount=submeshes.Count;for(int i=0;i<submeshes.Count;i++)target.SetSubMesh(i,submeshes[i],MeshUpdateFlags.DontRecalculateBounds);
     var mesh=new Mesh{name=source.name+" City Cleared"};Mesh.ApplyAndDisposeWritableMeshData(writable,mesh,MeshUpdateFlags.DontRecalculateBounds);mesh.bounds=source.bounds;AssetDatabase.CreateAsset(mesh,folder+"/Grass-"+(meshId++)+".asset");Undo.RecordObject(f,"Trim grass around city");f.sharedMesh=mesh;count+=removed;
     }
    }
   }
   return count;
  }
 }
}
#endif
