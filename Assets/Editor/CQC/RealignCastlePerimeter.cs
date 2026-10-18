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
 [InitializeOnLoad]public static class RealignCastlePerimeter
 {
  const string Request="Assets/Editor/CQC/RealignCastlePerimeter.request.txt";static double next;
  static RealignCastlePerimeter(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RealignCastlePerimeter-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Realign Outer Castle Walls And Gate Towers")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");var old=mountain.transform.Find("Outer Castle Walls And Towers");if(!old)throw new InvalidOperationException("Existing outer wall group missing.");var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshCollider>();if(!floor)throw new InvalidOperationException("Outer floor missing.");
   var area=floor.bounds;Vector3 center=area.center;center.y=area.max.y;float rx=area.extents.x-3.2f,rz=area.extents.z-3.2f;
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-perimeter-realignment-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var wallPrefab=AddMarkedTaverns.Build("MedievalWall");var towerPrefab=AddMarkedTaverns.Build("KickelhahnTower");
   var root=new GameObject("Realigned Outer Castle Walls And Towers");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Realign walls and towers");var walls=new GameObject("Perimeter Wall Sections");walls.transform.SetParent(root.transform,false);var towers=new GameObject("Eight Edge Towers");towers.transform.SetParent(root.transform,false);
   // Both ends of the open arc terminate inside the two gate towers.
   float gateHalf=Mathf.Asin(Mathf.Clamp(10f/rx,.02f,.35f)),start=1.5f*Mathf.PI+gateHalf,sweep=2*Mathf.PI-2*gateHalf;const int n=92;
   var sections=new List<GameObject>();var joints=new List<Vector3>();
   for(int i=0;i<n;i++)
   {
    float a=start+sweep*i/n,b=start+sweep*(i+1)/n;Vector3 left=Edge(center,rx,rz,a),right=Edge(center,rx,rz,b),tangent=(right-left).normalized,target=(left+right)*.5f;
    var go=(GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);go.name="Wall Section "+(i+1).ToString("00");go.transform.SetParent(walls.transform,true);go.transform.rotation=Quaternion.LookRotation(Vector3.Cross(tangent,Vector3.up));var bounds=BoundsOf(go);go.transform.localScale*=7.5f/bounds.size.y;
    float span=Span(go,tangent);var scale=go.transform.localScale;scale.x*=((right-left).magnitude+1.0f)/span;go.transform.localScale=scale;Place(go,target,center.y);
    float low=float.PositiveInfinity,high=float.NegativeInfinity;foreach(var p in Corners(go)){float projection=Vector3.Dot(p-target,tangent);low=Mathf.Min(low,projection);high=Mathf.Max(high,projection);}
    float half=(right-left).magnitude*.5f;if(low>-half-.3f||high<half+.3f)throw new InvalidOperationException("Wall segment does not overlap its endpoints.");
    sections.Add(go);joints.Add(right);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   }
   var angles=new List<float>();for(int i=0;i<8;i++)if(i!=6)angles.Add(i*Mathf.PI/4);angles.Add(1.5f*Mathf.PI-gateHalf);angles.Add(1.5f*Mathf.PI+gateHalf);
   int count=0;foreach(float angle in angles)
   {
    Vector3 target=Edge(center,rx,rz,angle),normal=new Vector3(Mathf.Cos(angle)/rx,0,Mathf.Sin(angle)/rz).normalized;
    var go=(GameObject)PrefabUtility.InstantiatePrefab(towerPrefab);go.name=(count>=7?"Gate Tower ":"Edge Tower ")+(++count).ToString("00");go.transform.SetParent(towers.transform,true);go.transform.rotation=Quaternion.LookRotation(normal);var bounds=BoundsOf(go);go.transform.localScale*=18f/bounds.size.y;bounds=BoundsOf(go);go.transform.localScale*=Mathf.Min(1,7f/Mathf.Max(bounds.size.x,bounds.size.z));Place(go,target,center.y);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    if(!floor.Raycast(new Ray(target+Vector3.up*50,Vector3.down),out RaycastHit hit,100))throw new InvalidOperationException("Tower center is unsupported.");
   }
   Physics.SyncTransforms();int verified=0;
   for(int i=0;i<n-1;i++)
   {
    Vector3 joint=joints[i],outward=Vector3.ProjectOnPlane(joint-center,Vector3.up).normalized;Ray ray=new Ray(joint+Vector3.up*2+outward*12,-outward);
    bool closed=sections[i].GetComponentsInChildren<MeshCollider>().Concat(sections[i+1].GetComponentsInChildren<MeshCollider>()).Any(c=>c.Raycast(ray,out RaycastHit hit,24));
    if(!closed)throw new InvalidOperationException("Collision gap remains at wall join "+(i+1)+"; original walls retained.");verified++;
   }
   // Commit only after every rebuilt joint is covered. Old child offsets/duplicates disappear with the old group.
   Undo.DestroyObjectImmediate(old.gameObject);root.name="Outer Castle Walls And Towers";AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RealignCastlePerimeter-report.txt","Rebuilt outer perimeter from original textured wall/tower prefabs: "+n+" wall sections, 7 perimeter towers and 2 matching gate towers. Uniform wall baseline and common ellipse retained; each segment overlaps endpoints by 0.5m. "+verified+"/"+(n-1)+" wall joins verified by collider raycasts. Gate ends terminate inside towers; central funicular entrance stays open. Removed malformed duplicated tower child parts by replacing only the outer perimeter group. Merchant stalls, houses, inner castle, rock slope and transportation unchanged.\nBackup: "+backup);Debug.Log("Outer wall sections and towers realigned; wall joins verified. Scene saved.");
  }
  static Vector3 Edge(Vector3 c,float rx,float rz,float a){return c+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.01f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Perimeter prefab missing mesh.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static float Span(GameObject go,Vector3 axis){float low=float.PositiveInfinity,high=float.NegativeInfinity;foreach(var p in Corners(go)){float v=Vector3.Dot(p,axis);low=Mathf.Min(low,v);high=Mathf.Max(high,v);}return high-low;}
  static List<Vector3> Corners(GameObject go){var points=new List<Vector3>();foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)points.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}return points;}
 }
}
#endif
