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
 [InitializeOnLoad] public static class SpreadCrowdAcrossStreets
 {
  const string Request="Assets/Editor/CQC/SpreadCrowdAcrossStreets-v1.request.txt";static double next;
  static SpreadCrowdAcrossStreets(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SpreadCrowdAcrossStreets-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/NPC/Spread Existing Residents Across All Cached Streets")]
  public static void Apply()
  {
   var crowd=UnityEngine.Object.FindAnyObjectByType<CityCrowdManager>();if(!crowd||crowd.People==null||crowd.People.Length!=750)throw new InvalidOperationException("The existing 750 residents are required.");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-npc-route-variety-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("NPC scene backup failed.");
   var skin=crowd.People.Select(p=>p.Skin).ToArray();var role=crowd.People.Select(p=>p.Role).ToArray();var speeds=crowd.People.Select(p=>p.Speed).ToArray();var regions=crowd.People.GroupBy(p=>crowd.Routes[p.Route].Region).ToDictionary(g=>g.Key,g=>g.Count());
   Undo.RecordObject(crowd,"Spread existing residents across the street network");crowd.SpreadAcrossRoadsOnStart=true;int moved=crowd.SpreadResidentsAcrossRoads();if(moved!=750)throw new InvalidOperationException("Not every resident has a walking street.");
   for(int i=0;i<750;i++)if(crowd.People[i].Skin!=skin[i]||crowd.People[i].Role!=role[i]||crowd.People[i].Speed!=speeds[i])throw new InvalidOperationException("NPC appearance, role or speed changed unexpectedly.");foreach(var region in regions)if(crowd.People.Count(p=>crowd.Routes[p.Route].Region==region.Key)!=region.Value)throw new InvalidOperationException("Regional resident count changed.");
   if(crowd.EditorPreview)foreach(Transform t in crowd.EditorPreview.transform)if(t.name.StartsWith("NPC Preview ")&&int.TryParse(t.name.Substring(12),out int index)&&index<crowd.People.Length){var p=crowd.People[index];Undo.RecordObject(t,"Preview residents across town roads");t.SetPositionAndRotation(p.Position,Quaternion.Euler(0,p.Yaw,0));}
   EditorUtility.SetDirty(crowd);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save street distribution.");
   string distribution=string.Join(", ",regions.Select(p=>"region "+p.Key+": "+p.Value));Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SpreadCrowdAcrossStreets-report.txt","Spread 750 existing residents over unique sampled road positions rather than circle-only spawn routes. "+distribution+". Runtime destination-based routing uses 8 stable route preferences, decreasing goal distance to prevent loops, and occupancy costs to prefer quieter alternatives. Fixed CircleNext preference removed. All movement remains on existing cached road polylines. Appearance mix, speeds, population totals, region totals, transport code and schedules retained. Runtime route diversity needs Play-mode observation.\nBackup: "+backup);Debug.Log("750 NPCs distributed across streets. Enter Play to see different routes to shared destinations.");SceneView.RepaintAll();
  }
 }
}
#endif
