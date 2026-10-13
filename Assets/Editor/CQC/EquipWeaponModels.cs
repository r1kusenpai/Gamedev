#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class EquipWeaponModels
 {
  const string Request="Assets/Editor/CQC/EquipWeaponModels.request.txt";
  static EquipWeaponModels(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating||File.Exists("Assets/Editor/CQC/PrepareWeaponPacks.request.txt")){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EquipWeaponModels-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Weapons/Equip Supplied Weapon Models")]public static void Apply()
  {
   var player=GameObject.Find("PlayerArmature");var weapons=player?player.GetComponent<StarterAssets.WeaponController>():null;if(!weapons)throw new InvalidOperationException("Prepare Weapon Packs first.");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-real-weapon-models-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var sword=AddMarkedTaverns.Build("ElucidatorSword",false);var great=AddMarkedTaverns.Build("InfernoCleaver",false);var bow=AddMarkedTaverns.Build("GreatEagleBow",false);
   foreach(var prefab in new[]{sword,great,bow})
   {
    if(!prefab||prefab.GetComponentsInChildren<MeshRenderer>().Length==0)throw new InvalidOperationException("Weapon prefab has no model.");
    foreach(var r in prefab.GetComponentsInChildren<MeshRenderer>())if(!r.sharedMaterial||!r.sharedMaterial.GetTexture("_BaseMap"))throw new InvalidOperationException("Weapon colour texture missing.");
   }
   Undo.RecordObject(weapons,"Replace placeholder weapon models");weapons.SwordModelPrefab=sword;weapons.GreatswordModelPrefab=great;weapons.BowModelPrefab=bow;EditorUtility.SetDirty(weapons);PrefabUtility.RecordPrefabInstancePropertyModifications(weapons);
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Selection.activeGameObject=player;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EquipWeaponModels-report.txt","Elucidator sword (1.2m), Inferno Cleaver (2.05m) and Great Eagle bow (1.65m) assigned to player weapon slots. Original albedo/PBR textures retained, Inferno emission retained.\nMeshes normalized with grip at origin and length along hand socket axis. Sword duplicate geometry reduced; no weapon mesh colliders added. Existing player animation packs and combat logic preserved.\nBow model imported in neutral pose; character uses existing Longbow pack. Source GLB skeletal bow animation is not imported.\nBackup: "+backup);Debug.Log("Supplied sword, greatsword and bow models now replace placeholders. Press 1, 2 or 3 in Play mode.");
  }
 }
}
#endif
