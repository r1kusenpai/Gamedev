#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class PrepareWeaponPacks
 {
  const string Request="Assets/Editor/CQC/PrepareWeaponPacks.request.txt",Folder="Assets/Resources/WeaponAnimations";
  static PrepareWeaponPacks(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WeaponPacks-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Weapons/Prepare Weapon Packs")]public static void Apply()
  {
   var player=GameObject.Find("PlayerArmature");if(!player)throw new InvalidOperationException("PlayerArmature is required.");var animator=player.GetComponent<Animator>();if(!animator||!animator.isHuman||!animator.avatar.isValid)throw new InvalidOperationException("Robot needs a valid Humanoid avatar.");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-weapons-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var report=new StringBuilder();int count=0;bool valid=true;
   foreach(string group in new[]{"Sword","Great","Bow"})
   {
    foreach(string path in Directory.GetFiles(Folder+"/"+group,"*.fbx"))
    {
     AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(!importer)throw new InvalidOperationException("FBX importer unavailable: "+path);
     string name=Path.GetFileNameWithoutExtension(path);if(importer.userData!="Weapons-v1")
     {
      importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
      var animations=importer.defaultClipAnimations;
      foreach(var clip in animations){clip.name=name;clip.loopTime=name.Contains("idle")||name.Contains("walk")||name.Contains("run")&&!name.Contains("stop")||name.Contains("strafe")||name=="fall a loop";clip.loopPose=clip.loopTime;clip.lockRootRotation=true;clip.keepOriginalOrientation=true;clip.lockRootHeightY=true;clip.keepOriginalPositionY=false;clip.heightFromFeet=true;clip.lockRootPositionXZ=false;clip.keepOriginalPositionXZ=false;}
      importer.clipAnimations=animations;importer.userData="Weapons-v1";importer.SaveAndReimport();
     }
     if(name=="X Bot")continue;
     var imported=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));bool ok=imported&&imported.isHumanMotion&&imported.length>0;valid&=ok;count++;report.AppendLine(group+"/"+name+": "+(ok?"Humanoid "+imported.length.ToString("F2")+"s":"NOT READY"));
    }
   }
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WeaponPacks-Import.txt",report.ToString());if(!valid)throw new InvalidOperationException("Some weapon clips did not import as Humanoid. See Logs/WeaponPacks-Import.txt.");
   if(!player.GetComponent<StarterAssets.WeaponController>())Undo.AddComponent<StarterAssets.WeaponController>(player);
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Selection.activeGameObject=player;Debug.Log("Weapon packs ready: "+count+" Humanoid clips. 1 sword/shield, 2 greatsword, 3 bow, 4/X unarmed. Left Alt dodges with weapon held.");File.WriteAllText("Logs/WeaponSystem-report.txt","Prepared "+count+" validated Humanoid clips and attached WeaponController to existing robot. Runtime placeholder weapons, four-step combos, hold/release charges, moving/air attacks, rear crouched damage bonus, directional dodges and bow projectiles.\n1/Numpad1 sword and shield; 2/Numpad2 greatsword; 3/Numpad3 bow; 4/Numpad4/X unarmed.\nExisting CQC graph extended with weapon overlay, original unarmed locomotion/CQC retained.\nBackup: "+backup);
  }
 }
}
#endif
