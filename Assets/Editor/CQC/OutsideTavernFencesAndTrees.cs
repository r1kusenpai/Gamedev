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
 [InitializeOnLoad]public static class OutsideTavernFencesAndTrees
 {
  const string Request="Assets/Editor/CQC/OutsideTavernFencesAndTrees.request.txt";static double next;
  static OutsideTavernFencesAndTrees(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(File.Exists("Assets/Editor/CQC/TripleCityTaverns.request.txt")||File.Exists("Assets/Editor/CQC/EnlargeTallTavernsOnly.request.txt"))return;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/OutsideTavernFencesAndTrees-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  static List<Vector3[]> roads;static Vector3 market;static List<Bounds> buildings;static List<Bounds> trees;
  [MenuItem("Tools/City/Add Wooden Fences And Tree To Each Outside Tavern")]
  public static void Apply()
  {
   var city=GameObject.Find("Lower Castle City");if(!city)throw new InvalidOperationException("Lower city missing.");if(city.transform.Find("Individual Tavern Fences And Trees"))return;var houses=city.transform.Find("House Neighborhoods");var streets=city.transform.Find("Winding Roads and Market Square");if(!houses||!streets)throw new InvalidOperationException("City houses or roads missing.");
   var houseList=houses.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray();if(houseList.Length==0)throw new InvalidOperationException("No outside taverns found.");buildings=houseList.Select(t=>BoundsOf(t.gameObject)).ToList();var smiths=city.transform.Find("Three Outside Castle Blacksmiths");if(smiths)foreach(Transform t in smiths)buildings.Add(BoundsOf(t.gameObject));trees=new List<Bounds>();roads=new List<Vector3[]>();
   foreach(var f in streets.GetComponentsInChildren<MeshFilter>()){if(f.name=="Central Market Square"){market=BoundsOf(f.gameObject).center;continue;}var v=f.sharedMesh.vertices;for(int i=0;i+3<v.Length;i+=2){var a=f.transform.TransformPoint((v[i]+v[i+1])*.5f);var b=f.transform.TransformPoint((v[i+2]+v[i+3])*.5f);if(Vector3.Distance(a,b)>12)continue;roads.Add(new[]{a,b,new Vector3(Vector3.Distance(f.transform.TransformPoint(v[i]),f.transform.TransformPoint(v[i+1]))*.5f,0,0)});}}
   var fence=AddMarkedTaverns.Build("OutsideWoodFence",false);var spruce=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Trees/Fir/Prefabs/UNS_Spruce_01.prefab");if(!spruce)throw new InvalidOperationException("Existing spruce asset missing.");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-individual-tavern-fences-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");var root=new GameObject("Individual Tavern Fences And Trees");root.transform.SetParent(city.transform,false);Undo.RegisterCreatedObjectUndo(root,"Fence each outside tavern and add trees");int panels=0,count=0,gaps=0;var notes=new List<string>();
   foreach(var house in houseList)
   {
    var lot=new GameObject(house.name+" Fenced Yard");lot.transform.SetParent(root.transform,false);var b=BoundsOf(house.gameObject);var rectangle=new[]{new Vector3(b.min.x-2.5f,0,b.min.z-2.5f),new Vector3(b.max.x+2.5f,0,b.min.z-2.5f),new Vector3(b.max.x+2.5f,0,b.max.z+2.5f),new Vector3(b.min.x-2.5f,0,b.max.z+2.5f)};
    int gateSide=0;float closest=float.PositiveInfinity;for(int i=0;i<4;i++){var mid=(rectangle[i]+rectangle[(i+1)%4])*.5f;float dist=RoadDistance(mid);if(dist<closest){closest=dist;gateSide=i;}}
    int lotPanels=0;
    for(int side=0;side<4;side++)
    {
     Vector3 a=rectangle[side],z=rectangle[(side+1)%4];float length=Vector3.Distance(a,z);int segments=Mathf.CeilToInt(length/1.8f);
     for(int i=0;i<segments;i++)
     {
      var p=Vector3.Lerp(a,z,(float)i/segments);var q=Vector3.Lerp(a,z,(float)(i+1)/segments);var mid=(p+q)*.5f;if(side==gateSide&&Mathf.Abs((i+.5f)*length/segments-length*.5f)<2){gaps++;continue;}
      if(RoadDistance(mid)<.55f||RoadDistance(p)<.35f||RoadDistance(q)<.35f||FlatDistance(mid,market)<18.5f){gaps++;continue;}
      float y0=Ground(p),y1=Ground(q);if(float.IsNaN(y0)||float.IsNaN(y1)||Mathf.Abs(y1-y0)>.8f){gaps++;continue;}
      var footprint=new Bounds(new Vector3(mid.x,(y0+y1)*.5f+.6f,mid.z),new Vector3(Mathf.Abs(q.x-p.x)+.18f,1.3f,Mathf.Abs(q.z-p.z)+.18f));if(buildings.Any(o=>Overlap(footprint,o,.35f))){gaps++;continue;}
      var go=(GameObject)PrefabUtility.InstantiatePrefab(fence);go.transform.SetParent(lot.transform,true);var natural=BoundsOf(go);go.transform.localScale=Vector3.Scale(go.transform.localScale,new Vector3((length/segments+.015f)/natural.size.x,1.25f/natural.size.y,1.5f));var tangent=(q-p).normalized;go.transform.rotation=Quaternion.LookRotation(Vector3.Cross(tangent,Vector3.up))*Quaternion.Euler(0,0,Mathf.Atan2(y1-y0,length/segments)*Mathf.Rad2Deg);Place(go,mid,Mathf.Min(y0,y1));go.name="Wood Fence "+(++lotPanels).ToString("000");
      var filter=go.GetComponentInChildren<MeshFilter>();var box=go.AddComponent<BoxCollider>();var mesh=filter.sharedMesh.bounds;box.center=go.transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.center));box.size=new Vector3(mesh.size.x,mesh.size.y,Mathf.Max(mesh.size.z,.12f));PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);panels++;
     }
    }
    if(lotPanels==0)throw new InvalidOperationException("No fence fits around "+house.name+"; scene retained.");
    var tree=(GameObject)PrefabUtility.InstantiatePrefab(spruce);tree.transform.SetParent(lot.transform,true);var native=tree.transform.localScale;float naturalHeight=BoundsOf(tree).size.y;bool found=false;
    foreach(float height in new[]{7f,5f,3.5f})
    {
     tree.transform.localScale=native*(height/naturalHeight);
     for(int ring=0;ring<5&&!found;ring++)for(int i=0;i<24;i++)
     {
      float angle=i*Mathf.PI/12;var p=b.center+new Vector3(Mathf.Cos(angle)*(b.extents.x+1.6f+ring*1.7f),0,Mathf.Sin(angle)*(b.extents.z+1.6f+ring*1.7f));float y=Ground(p);if(float.IsNaN(y)||RoadDistance(p)<2||FlatDistance(p,market)<20)continue;tree.transform.rotation=Quaternion.Euler(0,i*47,0);Place(tree,p,y);var tb=BoundsOf(tree);if(buildings.Any(o=>Overlap(tb,o,.6f))||trees.Any(o=>Overlap(tb,o,1)))continue;bool supported=true;foreach(var corner in new[]{new Vector3(tb.min.x,0,tb.min.z),new Vector3(tb.max.x,0,tb.max.z)}){float gy=Ground(corner);if(float.IsNaN(gy)||Mathf.Abs(gy-y)>.8f){supported=false;break;}}if(!supported)continue;found=true;break;
     }
     if(found)break;
    }
    if(!found)throw new InvalidOperationException("No tree space beside "+house.name+"; scene retained.");tree.name="Yard Spruce";PrefabUtility.RecordPrefabInstancePropertyModifications(tree.transform);trees.Add(BoundsOf(tree));count++;notes.Add(house.name+": "+lotPanels+" fence panels, one tree.");
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/OutsideTavernFencesAndTrees-report.txt","Added individual wooden fence yards and one spruce tree for each of "+count+" outside taverns. Extracted one style-1 panel from wood_fence.glb; shared 408-vertex mesh and original texture reused. Total panels: "+panels+". Four-meter entrance gaps face nearest street; "+gaps+" fence intervals omitted at entrances, roads or neighboring buildings. Ground support and building/tree separation checked. Existing houses, roads, castle and ground unchanged.\n"+string.Join("\n",notes)+"\nBackup: "+backup);Debug.Log("Individual outside tavern fences and trees added. Scene saved.");
  }
  static float Ground(Vector3 v){foreach(var t in Terrain.activeTerrains){var p=t.transform.position;var size=t.terrainData.size;if(v.x>=p.x&&v.x<=p.x+size.x&&v.z>=p.z&&v.z<=p.z+size.z)return p.y+t.terrainData.GetInterpolatedHeight((v.x-p.x)/size.x,(v.z-p.z)/size.z);}return float.NaN;}
  static float FlatDistance(Vector3 a,Vector3 b){return Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));}
  static float RoadDistance(Vector3 p){float best=float.PositiveInfinity;foreach(var s in roads){var a=new Vector2(s[0].x,s[0].z);var d=new Vector2(s[1].x-s[0].x,s[1].z-s[0].z);var q=new Vector2(p.x,p.z)-a;best=Mathf.Min(best,(q-d*Mathf.Clamp01(Vector2.Dot(q,d)/Mathf.Max(.001f,d.sqrMagnitude))).magnitude-s[2].x);}return best;}
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.02f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Visible model missing.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
