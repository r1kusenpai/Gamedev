#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class EnlargeTallTaverns
 {
  const string Request="Assets/Editor/CQC/EnlargeTallTaverns.request.txt";
  static EnlargeTallTaverns(){EditorApplication.delayCall+=Pending;}
  static void Pending()
  {
   if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
   if(SceneManager.GetActiveScene().name!="MyForest")return;
   try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EnlargeTallTaverns-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Buildings/Enlarge Brown Tall Taverns")] public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");
   var group=mountain.transform.Find("Added Marked Taverns");var wall=mountain.transform.Find("Inner Castle Mountain/Inner Rocky Mountain Sides").GetComponent<Renderer>().bounds;
   var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshCollider>();var area=floor.bounds;
   if(!group)throw new InvalidOperationException("Added taverns missing.");
   string backup="Assets/ForestExpansion/Backups/MyForest-before-larger-tall-taverns-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";var scene=SceneManager.GetActiveScene();if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   int changed=0,checks=0;
   foreach(Transform house in group)
   {
    if(!house.name.StartsWith("Tall Tavern ",StringComparison.Ordinal))continue;
    Undo.RecordObject(house,"Enlarge entire tall tavern");Bounds before=BoundsOf(house);house.localScale*=1.4f;
    Bounds after=BoundsOf(house);house.position+=new Vector3(before.center.x-after.center.x,area.max.y+.02f-after.min.y,before.center.z-after.center.z);
    Vector3 normal=Vector3.ProjectOnPlane(house.forward,Vector3.up).normalized;
    float a=Mathf.Atan2(normal.z*wall.extents.z,normal.x*wall.extents.x);
    Vector3 edge=new Vector3(wall.center.x+Mathf.Cos(a)*wall.extents.x,area.max.y,wall.center.z+Mathf.Sin(a)*wall.extents.z);
    var corners=Corners(house);float closest=float.PositiveInfinity;foreach(var p in corners)closest=Mathf.Min(closest,Vector3.Dot(p-edge,normal));house.position+=normal*(.35f-closest);
    foreach(var p in Corners(house))
    {
     if(Vector3.Dot(p-edge,normal)<.3f)throw new InvalidOperationException("Enlarged house clips wall.");
     if(!floor.Raycast(new Ray(new Vector3(p.x,area.max.y+100,p.z),Vector3.down),out RaycastHit hit,150))throw new InvalidOperationException("Enlarged house overhangs floor: "+house.name);checks++;
    }
    PrefabUtility.RecordPrefabInstancePropertyModifications(house);changed++;
   }
   if(changed==0)throw new InvalidOperationException("Brown Tall Tavern copies were not found.");
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EnlargeTallTaverns-report.txt","Enlarged all "+changed+" brown Tall Tavern copies by 40% uniformly, including every model part and mesh collider.\nBases aligned with floor and backs kept clear of wall. Support and wall checks passed: "+checks+".\nBackup: "+backup);Selection.activeGameObject=group.gameObject;SceneView.RepaintAll();Debug.Log("All brown Tall Tavern houses are now 40% bigger.");
  }
  static Bounds BoundsOf(Transform t){var renderers=t.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
  static List<Vector3> Corners(Transform t){var points=new List<Vector3>();foreach(var f in t.GetComponentsInChildren<MeshFilter>()){if(!f.sharedMesh)continue;Bounds b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)points.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}return points;}
 }
}
#endif
