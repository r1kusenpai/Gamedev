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
 [InitializeOnLoad]public static class MatchInsideTavernSizes
 {
  const string Request="Assets/Editor/CQC/MatchInsideTavernSizes.request.txt";static double next;
  static MatchInsideTavernSizes(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(File.Exists("Assets/Editor/CQC/TripleCityTaverns.request.txt"))return;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/MatchInsideTavernSizes-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/Buildings/Match Inside Tavern Sizes To Outside")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");var city=GameObject.Find("Lower Castle City");if(!mountain||!city)throw new InvalidOperationException("Castle and lower city missing.");
   if(mountain.transform.Find("Inside Tavern Sizes Matched To Outside"))return;
   var outside=city.transform.Find("House Neighborhoods");if(!outside)throw new InvalidOperationException("Outside houses missing.");var references=outside.Cast<Transform>().Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).GroupBy(t=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)).ToDictionary(g=>g.Key,g=>g.OrderBy(t=>BoundsOf(t.gameObject).size.y).ElementAt(g.Count()/2));
   var inside=new List<Transform>();foreach(string name in new[]{"One Ring Of Houses","Added Marked Taverns"}){var group=mountain.transform.Find(name);if(group)foreach(Transform t in group)if(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))inside.Add(t);}
   if(inside.Count==0)throw new InvalidOperationException("No inside taverns found.");foreach(var t in inside)if(!references.ContainsKey(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)))throw new InvalidOperationException("No matching outside model for "+t.name);
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-match-inside-taverns-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var heights=new List<string>();foreach(var house in inside)
   {
    var before=BoundsOf(house.gameObject);var reference=references[PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(house.gameObject)];float target=BoundsOf(reference.gameObject).size.y;Undo.RecordObject(house,"Match inside tavern size to outside");house.localScale*=target/before.size.y;var after=BoundsOf(house.gameObject);
    // Keep each existing lot's center and floor contact, independent of model pivot.
    house.position+=new Vector3(before.center.x-after.center.x,before.min.y-after.min.y,before.center.z-after.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(house);heights.Add(house.name+": "+target.ToString("F2")+"m tall");
   }
   int closePairs=0;for(int i=0;i<inside.Count;i++)for(int j=i+1;j<inside.Count;j++){var a=BoundsOf(inside[i].gameObject);var b=BoundsOf(inside[j].gameObject);if(Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z)closePairs++;}
   var marker=new GameObject("Inside Tavern Sizes Matched To Outside");marker.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(marker,"Remember matched tavern sizes");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/MatchInsideTavernSizes-report.txt","Matched "+inside.Count+" inside castle taverns to the median outside size for each identical prefab model. Uniform scaling includes door openings and existing colliders. Building centers, rotations and base heights retained; walls, towers, castle, chapel, blacksmiths and ground retained. Pending outside resize is processed first. Building bounding-box overlap pairs after matching: "+closePairs+".\n"+string.Join("\n",heights)+"\nBackup: "+backup);Debug.Log("Inside castle tavern sizes matched to outside. Scene saved.");
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Building geometry missing.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
