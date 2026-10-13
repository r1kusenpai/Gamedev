#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]
 public static class ImportGreatHeldSlash
 {
  const string PathName="Assets/Resources/WeaponAnimations/Great/great sword held slash.fbx";
  const string Request="Assets/Editor/CQC/ImportGreatHeldSlash.request.txt";
  static double nextCheck;
  static ImportGreatHeldSlash(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   try{Apply();AssetDatabase.DeleteAsset(Request);}
   catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GreatHeldSlash-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Weapons/Import Great Sword Held Slash")]
  public static void Apply()
  {
   AssetDatabase.ImportAsset(PathName,ImportAssetOptions.ForceSynchronousImport);
   var importer=AssetImporter.GetAtPath(PathName) as ModelImporter;
   if(!importer)throw new InvalidOperationException("Great Sword Slash FBX missing.");
   importer.animationType=ModelImporterAnimationType.Human;
   importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
   importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;
   importer.materialImportMode=ModelImporterMaterialImportMode.None;
   var clips=importer.defaultClipAnimations;
   if(clips.Length==0)throw new InvalidOperationException("FBX contains no animation takes.");
   var clip=clips[0];clip.name="great sword held slash";
   // Runtime restarts each complete swing and clears the damage hit list once per swing.
   clip.loopTime=false;clip.loopPose=false;
   clip.lockRootRotation=true;clip.keepOriginalOrientation=true;
   clip.lockRootHeightY=true;clip.keepOriginalPositionY=false;clip.heightFromFeet=true;
   // Extract travelling motion into the player collider so the body does not rewind at the loop boundary.
   clip.lockRootPositionXZ=false;clip.keepOriginalPositionXZ=false;
   clip.events=new AnimationEvent[0];importer.clipAnimations=new[]{clip};
   importer.userData="GreatHeldSlash-v2";importer.SaveAndReimport();
   var imported=AssetDatabase.LoadAllAssetsAtPath(PathName).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(PathName);
   var avatar=model?model.GetComponent<Animator>():null;
   if(!imported||!imported.isHumanMotion||imported.length<=0||!avatar||!avatar.avatar||!avatar.avatar.isValid)
    throw new InvalidOperationException("Slash did not import as a valid Humanoid animation.");
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GreatHeldSlash-report.txt","Supplied Great Sword Slash imported as Humanoid: "+imported.length.ToString("F2")+" seconds. Hold left click repeats full swings, release finishes the current swing. Root translation extracted into the character motor; travelled position retained across repeats. Greatsword crouch keeps base legs and uses a two-handed handle grip. No scene changes required.");
   Debug.Log("Greatsword held slash ready. Hold left click to repeat; release to finish. Two-handed crouch grip installed.");
  }
 }
}
#endif
