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
 [InitializeOnLoad]public static class EnlargeTallTavernsOnly
 {
  const string Request="Assets/Editor/CQC/EnlargeTallTavernsOnly.request.txt";static double next;
  static EnlargeTallTavernsOnly(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EnlargeTallTavernsOnly-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/Buildings/Enlarge Only Tall Tavern Model By Fifty Percent")]
  public static void Apply()
  {
   const string model="Assets/Building/ImportedTaverns/TallTavern/TallTavern.prefab";
   var scene=SceneManager.GetActiveScene();var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");if(mountain.transform.Find("Tall Tavern Extra 50 Percent Applied"))return;
   var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var taverns=all.Where(t=>t.gameObject.activeInHierarchy&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)==model).ToArray();if(taverns.Length==0)throw new InvalidOperationException("Tall Tavern model instances missing.");
   var foundations=all.Where(t=>t.name.StartsWith("Stone House Foundation",StringComparison.Ordinal)&&t.GetComponent<Renderer>()).ToArray();var resizedFoundations=new HashSet<Transform>();var entries=new List<string>();
   string backup="Assets/ForestExpansion/Backups/MyForest-before-tall-tavern-extra-size-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   foreach(var house in taverns)
   {
    var before=BoundsOf(house.gameObject);var foundation=foundations.FirstOrDefault(t=>!resizedFoundations.Contains(t)&&Vector2.Distance(new Vector2(BoundsOf(t.gameObject).center.x,BoundsOf(t.gameObject).center.z),new Vector2(before.center.x,before.center.z))<.5f);
    Undo.RecordObject(house,"Make only Tall Tavern bigger");house.localScale*=1.5f;var after=BoundsOf(house.gameObject);house.position+=new Vector3(before.center.x-after.center.x,before.min.y-after.min.y,before.center.z-after.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(house);
    if(foundation){Undo.RecordObject(foundation,"Resize Tall Tavern footing");var scale=foundation.localScale;scale.x*=1.5f;scale.z*=1.5f;foundation.localScale=scale;resizedFoundations.Add(foundation);}
    entries.Add(house.name+": "+before.size.y.ToString("F2")+"m -> "+BoundsOf(house.gameObject).size.y.ToString("F2")+"m");
   }
   var marker=new GameObject("Tall Tavern Extra 50 Percent Applied");marker.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(marker,"Remember Tall Tavern enlargement");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EnlargeTallTavernsOnly-report.txt","Enlarged "+taverns.Length+" active copies of the exact TallTavern prefab by another 50%, uniformly in all axes, inside and outside castle. Door openings and existing colliders scale automatically; building centers, rotations and floor contact preserved. Resized "+resizedFoundations.Count+" matching footings. Other tavern models and structures unchanged.\n"+string.Join("\n",entries)+"\nBackup: "+backup);Debug.Log("Only Tall Tavern model copies enlarged another 50%. Scene saved.");
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Tavern geometry missing.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
