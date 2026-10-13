#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class FitTavernWallRow
 {
  const string Request="Assets/Editor/CQC/FitTavernWallRow.request.txt";
  static FitTavernWallRow(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating||File.Exists("Assets/Editor/CQC/CastleInnerOutline.request.txt")){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FitTavernWallRow-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Fit Taverns Against Inner Wall")] public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");
   var inner=mountain.transform.Find("Inner Castle Mountain");var wall=inner.Find("Inner Rocky Mountain Sides").GetComponent<Renderer>().bounds;
   var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshCollider>();var area=floor.bounds;
   var list=new List<Transform>();foreach(string name in new[]{"One Ring Of Houses","Added Marked Taverns"}){var group=mountain.transform.Find(name);if(group)foreach(Transform h in group)list.Add(h);}
   if(list.Count<3)throw new InvalidOperationException("Tavern row missing.");
   Vector3 c=wall.center;c.y=area.max.y;float rx=wall.extents.x,rz=wall.extents.z;
   list.Sort((a,b)=>Angle(BoundsOf(a.gameObject).center,c,rx,rz).CompareTo(Angle(BoundsOf(b.gameObject).center,c,rx,rz)));
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-wall-house-row-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   float gateHalfAngle=.12f;float step=(Mathf.PI*2-gateHalfAngle*2)/list.Count;int checks=0;
   for(int i=0;i<list.Count;i++)
   {
    var h=list[i];Undo.RecordObject(h,"Fit tavern against wall");float a=Mathf.PI+gateHalfAngle+(i+.5f)*step;Vector3 edge=Edge(c,rx,rz,a),normal=new Vector3(Mathf.Cos(a)/rx,0,Mathf.Sin(a)/rz).normalized,tangent=new Vector3(-normal.z,0,normal.x);
    h.rotation=Quaternion.LookRotation(normal);Bounds b=BoundsOf(h.gameObject);h.localScale*=12f/b.size.y;
    var corners=Corners(h);float width=Span(corners,tangent);float available=(Vector3.Distance(edge,Edge(c,rx,rz,a-step))+Vector3.Distance(edge,Edge(c,rx,rz,a+step)))*.5f;
    h.localScale*=Mathf.Min(1f,available*.94f/width);
    // Keep deep models within the narrow walkway as well as within their frontage.
    corners=Corners(h);float depth=Span(corners,normal);Vector3 outerEdge=Edge(c,area.extents.x,area.extents.z,a);float walkway=Vector3.Dot(outerEdge-edge,normal);
    h.localScale*=Mathf.Min(1f,(walkway-1.2f)/depth);
    b=BoundsOf(h.gameObject);corners=Corners(h);float minRadial=float.PositiveInfinity;foreach(var p in corners)minRadial=Mathf.Min(minRadial,Vector3.Dot(p-b.center,normal));
    Vector3 target=edge+normal*(-minRadial+.35f);h.position+=new Vector3(target.x-b.center.x,area.max.y+.02f-b.min.y,target.z-b.center.z);
    corners=Corners(h);
    foreach(var p in corners)
    {
     float outside=Vector3.Dot(p-edge,normal);if(outside<.3f)throw new InvalidOperationException("Tavern clips wall: "+h.name);
     if(!floor.Raycast(new Ray(new Vector3(p.x,area.max.y+100,p.z),Vector3.down),out RaycastHit hit,150))throw new InvalidOperationException("Tavern overhangs floor: "+h.name);checks++;
    }
    PrefabUtility.RecordPrefabInstancePropertyModifications(h);
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FitTavernWallRow-report.txt","Resized and arranged "+list.Count+" taverns against the inner vertical wall, facing outward.\nTarget height 12m, limited by each frontage and walkway depth. Rear clearance 0.35m.\nFloor support and wall separation corner checks passed: "+checks+".\nBackup: "+backup);Selection.activeGameObject=mountain;SceneView.RepaintAll();Debug.Log("Taverns resized to fill the row against the inner wall.");
  }
  static float Angle(Vector3 p,Vector3 c,float rx,float rz){float a=Mathf.Atan2((p.z-c.z)/rz,(p.x-c.x)/rx);return a<0?a+Mathf.PI*2:a;}
  static Vector3 Edge(Vector3 c,float rx,float rz,float a){return c+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);}
  static float Span(List<Vector3> points,Vector3 axis){float min=float.PositiveInfinity,max=float.NegativeInfinity;foreach(var p in points){float d=Vector3.Dot(p,axis);min=Mathf.Min(min,d);max=Mathf.Max(max,d);}return max-min;}
  static List<Vector3> Corners(Transform h){var result=new List<Vector3>();foreach(var f in h.GetComponentsInChildren<MeshFilter>()){if(!f.sharedMesh)continue;Bounds b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)result.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}if(result.Count==0)throw new InvalidOperationException("No house mesh: "+h.name);return result;}
  static Bounds BoundsOf(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
 }
}
#endif

