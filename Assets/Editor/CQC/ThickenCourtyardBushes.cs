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
 [InitializeOnLoad]public static class ThickenCourtyardBushes
 {
  const string Request="Assets/Editor/CQC/ThickenCourtyardBushes.request.txt";static double next;
  static ThickenCourtyardBushes(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ThickenCourtyardBushes-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/City/Make Courtyard Bushes Four Times Thicker")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");var gardens=mountain.transform.Find("Courtyard Gardens On Both Sides");if(!gardens)throw new InvalidOperationException("Courtyard garden bushes missing.");
   // Marker makes refresh/retry safe: multiply only once.
   if(gardens.Find("Bush Width Increased 4x"))return;
   var bushes=gardens.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Courtyard Boxwood ",StringComparison.Ordinal)).ToArray();if(bushes.Length==0)throw new InvalidOperationException("No courtyard boxwood found.");
   var protectedTransforms=mountain.GetComponentsInChildren<Transform>(true).Where(t=>!bushes.Any(b=>t==b||t.IsChildOf(b))).ToArray();var matrices=protectedTransforms.Select(t=>t.localToWorldMatrix).ToArray();
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-thicker-bushes-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   foreach(var bush in bushes){Undo.RecordObject(bush,"Make bush four times thicker");var scale=bush.localScale;scale.x*=4;bush.localScale=scale;PrefabUtility.RecordPrefabInstancePropertyModifications(bush);}
   var marker=new GameObject("Bush Width Increased 4x");marker.transform.SetParent(gardens,false);Undo.RegisterCreatedObjectUndo(marker,"Remember bush thickness change");
   for(int i=0;i<protectedTransforms.Length;i++)if(protectedTransforms[i].localToWorldMatrix!=matrices[i])throw new InvalidOperationException("An unrelated object moved; changes rolled back.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ThickenCourtyardBushes-report.txt","Made "+bushes.Length+" courtyard boxwood bushes four times wider on local X. Height, hedge length and position preserved. Trees, existing wall borders, chapel, fountain, castle and other structures unchanged. Applied once to avoid repeated multiplication.\nBackup: "+backup);Debug.Log("Courtyard bushes are now four times thicker. Scene saved.");
  }
 }
}
#endif
