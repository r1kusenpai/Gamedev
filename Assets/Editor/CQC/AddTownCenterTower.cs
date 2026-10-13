#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class AddTownCenterTower
 {
  const string Request="Assets/Editor/CQC/AddTownCenterTower.request.txt";
  static double next;
  static AddTownCenterTower(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddTownCenterTower-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Buildings/Add Gothic Tower To Town Center")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode first.");
   var city=GameObject.Find("Lower Castle City");if(!city)throw new InvalidOperationException("Lower town missing.");
   if(city.transform.Find("Gothic Town Center Tower"))return;
   var square=city.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Central Market Square");
   if(!square)throw new InvalidOperationException("Central market square missing.");
   var floor=square.GetComponent<Renderer>();if(!floor)throw new InvalidOperationException("Market square floor renderer missing.");
   var area=floor.bounds;
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-town-tower-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var prefab=AddMarkedTaverns.Build("GothicTownTower");
   var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);Undo.RegisterCreatedObjectUndo(go,"Add town center tower");go.name="Gothic Town Center Tower";go.transform.SetParent(city.transform,true);
   var bounds=BoundsOf(go);if(bounds.size.y<=0)throw new InvalidOperationException("Tower mesh has no height.");
   // A landmark taller than the town houses, leaving a walking route around its base.
   float scale=Mathf.Min(30f/bounds.size.y,12f/Mathf.Max(bounds.size.x,bounds.size.z));go.transform.localScale*=scale;bounds=BoundsOf(go);
   Vector3 target=area.center;float ground=area.max.y;
   foreach(var terrain in Terrain.activeTerrains){var pos=terrain.transform.position;var size=terrain.terrainData.size;if(target.x>=pos.x&&target.x<=pos.x+size.x&&target.z>=pos.z&&target.z<=pos.z+size.z)ground=Mathf.Max(ground,terrain.SampleHeight(target)+pos.y);}
   go.transform.position+=new Vector3(target.x-bounds.center.x,ground+.03f-bounds.min.y,target.z-bounds.center.z);bounds=BoundsOf(go);
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddTownCenterTower-report.txt","Imported gotischer_turm.glb with its original materials and textures. Added Gothic Town Center Tower at central market square. Position: "+go.transform.position+". Bounds: "+bounds.size+". Mesh collisions enabled. Existing houses, roads and castle retained.\nBackup: "+backup);
   Debug.Log("Gothic tower added at town square center. Scene saved.");
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Tower has no renderers.");Bounds b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
