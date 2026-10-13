#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class ReplaceCentralCastle
 {
  const string Request="Assets/Editor/CQC/ReplaceCentralCastle.request.txt";
  static ReplaceCentralCastle(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ReplaceCentralCastle-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Replace Centre With WDW Castle")]public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle mountain missing.");if(mountain.transform.Find("Central Castle (WDW)"))return;
   var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();Bounds area=floor.bounds;
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-WDW-castle-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var prefab=AddMarkedTaverns.Build("WDWCastle");var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Central Castle (WDW)";go.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(go,"Replace centre castle");
   Bounds b=BoundsOf(go);float scale=Mathf.Min(area.size.x*.66f/b.size.x,area.size.z*.66f/b.size.z,35f/b.size.y);go.transform.localScale*=scale;b=BoundsOf(go);
   go.transform.position+=new Vector3(area.center.x-b.center.x,area.max.y+.01f-b.min.y,area.center.z-b.center.z);b=BoundsOf(go);Physics.SyncTransforms();
   foreach(Vector3 p in new[]{new Vector3(b.min.x,area.max.y+100,b.min.z),new Vector3(b.max.x,area.max.y+100,b.min.z),new Vector3(b.min.x,area.max.y+100,b.max.z),new Vector3(b.max.x,area.max.y+100,b.max.z)})if(!floor.Raycast(new Ray(p,Vector3.down),out RaycastHit hit,150))throw new InvalidOperationException("Castle footprint is not on upper floor.");
   var old=mountain.transform.Find("Central Upper Floor Building");if(old){Undo.RecordObject(old.gameObject,"Replace previous central model");old.gameObject.SetActive(false);}
   PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Selection.activeGameObject=go;SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ReplaceCentralCastle-report.txt","Supplied WDWcastle OBJ optimized and installed as central castle. Centred on upper floor and base aligned. All four footprint corners supported.\nSource contains no material colours or UV textures; neutral stone material applied. Previous central model hidden; walls, towers, houses and transport preserved.\nBackup: "+backup);Debug.Log("Central model replaced with your WDW castle.");
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();Bounds b=r[0].bounds;foreach(var item in r)b.Encapsulate(item.bounds);return b;}
 }
}
#endif
