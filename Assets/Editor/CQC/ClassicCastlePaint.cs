#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class ClassicCastlePaint
 {
  const string Folder="Assets/Building/ClassicCastlePaint",Request="Assets/Editor/CQC/ClassicCastlePaint.request.txt";
  static ClassicCastlePaint(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ClassicCastlePaint-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Paint Centre Castle Classic Stone Brick")]public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");Transform target=mountain.transform.Find("Central Upper Floor Building");
   if(!target||!target.gameObject.activeInHierarchy)target=mountain.transform.Find("Central Castle (WDW)");if(!target||!target.gameObject.activeInHierarchy)throw new InvalidOperationException("Activate the restored central castle first.");
   AssetDatabase.ImportAsset(Folder+"/ClassicStoneBrick.shader",ImportAssetOptions.ForceSynchronousImport);Shader stone=Shader.Find("Castle/Classic Stone Brick");if(!stone||ShaderUtil.ShaderHasError(stone))throw new InvalidOperationException("Castle stone shader did not compile.");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-classic-stone-paint-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var renderers=target.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
   var cache=new Dictionary<string,Material>();int slots=0;
   foreach(var renderer in renderers)
   {
    var materials=renderer.sharedMaterials;Undo.RecordObject(renderer,"Paint castle classic stone brick");
    for(int i=0;i<materials.Length;i++)
    {
     string sourceName=materials[i]?materials[i].name:"Stone";string name=sourceName.ToLowerInvariant();string kind="Stone";
     if(name.Contains("roof")||name.Contains("slate"))kind="SlateRoof";
     else if(name.Contains("glass")||name.Contains("window"))kind="DarkWindows";
     else if(name.Contains("wood")||name.Contains("door"))kind="DarkOak";
     else if(name.Contains("concrete")||name.Contains("aluminum")||name.Contains("metal")||name.Contains("silver"))kind="StoneTrim";
     if(!cache.TryGetValue(kind,out Material material))
     {
      bool flat=kind=="DarkOak"||kind=="DarkWindows";material=new Material(flat?Shader.Find("Universal Render Pipeline/Lit"):stone){name="ClassicCastle_"+kind};
      Color color=kind=="DarkOak"?new Color(.24f,.115f,.05f):kind=="DarkWindows"?new Color(.065f,.09f,.105f):kind=="StoneTrim"?new Color(.37f,.39f,.38f):new Color(.58f,.59f,.57f);
      material.SetColor("_BaseColor",color);
      if(flat){material.SetFloat("_Smoothness",kind=="DarkWindows"?.65f:.1f);material.SetFloat("_Cull",0);}
      else {material.SetColor("_MortarColor",new Color(.29f,.30f,.29f));material.SetColor("_RoofColor",new Color(.15f,.18f,.22f));material.SetFloat("_RoofStartY",b.min.y+b.size.y*.28f);material.SetFloat("_AutoRoof",target.name.Contains("WDW")?1:0);material.SetFloat("_ForceRoof",kind=="SlateRoof"?1:0);material.SetFloat("_BlockWidth",.75f);material.SetFloat("_BlockHeight",.32f);material.SetFloat("_Joint",.018f);}
      material.doubleSidedGI=true;AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+material.name+".mat"));cache.Add(kind,material);
     }
     materials[i]=material;slots++;
    }
    renderer.sharedMaterials=materials;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);EditorUtility.SetDirty(renderer);
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Selection.activeGameObject=target.gameObject;SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ClassicCastlePaint-report.txt","Painted active restored model: "+target.name+"\nClean grey stone brick in world coordinates, dark mortar, darker trim and slate roofing. Named door/wood and window sections receive oak and dark glass colours. Material slots: "+slots+".\nStone shader compile check passed. Geometry, placement, surrounding walls and transport preserved.\nBackup: "+backup);Debug.Log("Centre castle painted with clean classic stone brick and slate roofing.");
  }
 }
}
#endif
