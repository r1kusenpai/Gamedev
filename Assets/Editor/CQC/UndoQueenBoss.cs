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
 [InitializeOnLoad] public static class UndoQueenBoss
 {
  const string Request="Assets/Editor/CQC/UndoQueenBoss-v1.request.txt";
  const string SourceBackup="C:/Users/Joshua/Documents/Codex/2026-10-11/ca/work/queen-boss-before/";
  const string Archive="C:/Users/Joshua/Documents/Codex/2026-10-11/ca/work/queen-boss-undone/";
  static double next;static UndoQueenBoss(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;try{Apply();AssetDatabase.DeleteAsset(Request);AssetDatabase.Refresh();}catch(Exception e){File.WriteAllText("Logs/UndoQueenBoss-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  static void Apply()
  {
   var active=SceneManager.GetActiveScene();const string path="Assets/Scenes/ThroneDomain.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);string archive=Archive+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"/";Directory.CreateDirectory(archive);
   try{
    var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ThroneDomainRoom>(true)).Single();var roots=room.transform.GetComponentsInChildren<Transform>(true);var models=roots.Where(t=>!t.name.StartsWith("Queen Battle - ")).ToArray();var matrices=models.Select(t=>t.localToWorldMatrix).ToArray();
    string backup="Assets/ForestExpansion/Backups/ThroneDomain-before-undo-boss-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up current hall edits.");
    int removed=0;foreach(var component in room.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c&&(c.GetType().Name=="QueenBossEncounter"||c.GetType().Name=="QueenBossHurtbox")).ToArray()){Undo.DestroyObjectImmediate(component);removed++;}
    foreach(var marker in room.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Queen Battle - ")).ToArray())Undo.DestroyObjectImmediate(marker.gameObject);
    for(int i=0;i<models.Length;i++)if(models[i]&&models[i].localToWorldMatrix!=matrices[i])throw new InvalidOperationException("Unrelated hall geometry moved.");
    EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save peaceful hall.");
    string weapon="Assets/Starter Assets/Runtime/ThirdPersonController/Scripts/WeaponController.cs",portal="Assets/Scripts/World/ChapelThroneDomain.cs";
    File.Copy(weapon,archive+"WeaponController-with-boss.cs");File.Copy(portal,archive+"ChapelThroneDomain-with-boss.cs");File.Copy(SourceBackup+"WeaponController.cs",weapon,true);File.Copy(SourceBackup+"ChapelThroneDomain.cs",portal,true);
    string[] files={"Assets/Starter Assets/Runtime/ThirdPersonController/Scripts/ICombatDamageReceiver.cs","Assets/Starter Assets/Runtime/ThirdPersonController/Scripts/WeaponCombatReceivers.cs","Assets/Scripts/World/QueenBossHurtbox.cs","Assets/Scripts/World/QueenBossEncounter.cs","Assets/Scripts/World/QueenBossEncounter.Combat.cs","Assets/Scripts/World/QueenBossEncounter.Effects.cs","Assets/Scripts/World/QueenBossEncounter.UI.cs","Assets/Scripts/World/QueenBossProjectile.cs","Assets/Editor/CQC/InstallQueenBoss.cs","Assets/Editor/CQC/TestQueenBoss.cs"};
    foreach(var file in files)foreach(var name in new[]{file,file+".meta"})if(File.Exists(name))File.Move(name,archive+Path.GetFileName(name));
    foreach(var prefix in new[]{"InstallQueenBoss","TestQueenBoss"})foreach(var file in Directory.GetFiles("Assets/Editor/CQC",prefix+"*.request.txt*"))File.Move(file,archive+Path.GetFileName(file));
    File.WriteAllText("Logs/UndoQueenBoss-report.txt","PASS: removed "+removed+" boss components and throne challenge/spawn markers. Restored weapon and chapel scripts exactly to pre-boss versions. Archived boss runtime scripts, installer, tester and request files outside Assets. Queen, cape, throne, hall scale, map, chapel entrance and domain return preserved. Current edited hall backed up first.\nBackup: "+backup+"\nArchived files: "+archive);
    Debug.Log("Queen boss build undone. Peaceful queen, hall, map and chapel teleport preserved.");
   }finally{if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);if(opened)EditorSceneManager.CloseScene(scene,true);}
  }
 }
}
#endif
