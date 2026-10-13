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
 [InitializeOnLoad]public static class GreenDirtGround
 {
  const string Request="Assets/Editor/CQC/GreenDirtGround.request.txt";
  static double next;
  static GreenDirtGround(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GreenDirtGround-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Forest/Apply Green Dirt Surface")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode before changing ground.");
   var scene=SceneManager.GetActiveScene();
   var sourceDirt=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Terrain/Layers/UNS_Dirt.terrainlayer");
   if(!sourceDirt||!sourceDirt.diffuseTexture)throw new InvalidOperationException("Original dirt texture missing.");
   var terrains=Terrain.activeTerrains.Where(t=>t.gameObject.scene==scene).ToArray();if(terrains.Length==0)throw new InvalidOperationException("No terrain found.");
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-all-green-land-"+stamp+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
   string folder="Assets/ForestExpansion/GreenLand-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var greenDirt=UnityEngine.Object.Instantiate(sourceDirt);greenDirt.name="Original Dirt - Green Tint";
   greenDirt.diffuseRemapMin=Vector4.zero;greenDirt.diffuseRemapMax=new Vector4(.45f,1.35f,.18f,1);
   AssetDatabase.CreateAsset(greenDirt,folder+"/GreenDirt.terrainlayer");
   var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");if(!shader)throw new InvalidOperationException("URP terrain shader missing.");
   var material=new Material(shader){name="All Green Land"};material.SetColor("_BaseColor",Color.white);material.SetFloat("_EnableHeightBlend",0);AssetDatabase.CreateAsset(material,folder+"/AllGreenTerrain.mat");
   int count=0;long painted=0;
   foreach(var terrain in terrains)
   {
    // Independent copies preserve elevations, trees and collisions and avoid linked terrain editing.
    var data=UnityEngine.Object.Instantiate(terrain.terrainData);data.name=terrain.name+" Green Dirt Ground";
    data.terrainLayers=new[]{greenDirt};int index=0;
    int width=data.alphamapWidth,height=data.alphamapHeight;var alpha=new float[height,width,data.alphamapLayers];
    for(int z=0;z<height;z++)for(int x=0;x<width;x++)alpha[z,x,index]=1;
    data.SetAlphamaps(0,0,alpha);data.SetBaseMapDirty();AssetDatabase.CreateAsset(data,folder+"/Terrain-"+terrain.GetEntityId()+".asset");
    Undo.RecordObject(terrain,"Restore original green ground");terrain.terrainData=data;terrain.materialTemplate=material;terrain.Flush();
    var collider=terrain.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Preserve independent terrain collision");collider.terrainData=data;}
    EditorUtility.SetDirty(terrain);count++;painted+=(long)width*height;
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GreenDirtGround-report.txt","Recolored the original UNS_Dirt terrain texture green on "+count+" terrain tiles ("+painted+" texture samples). Original dirt diffuse texture retained; green tint applied through TerrainLayer.diffuseRemapMax (.45, 1.35, .18). No grass texture substituted and no grass plants added. Distant base maps refreshed. Terrain heights, trees, houses, cobblestone roads and castle paving unchanged. Independent terrain data preserved.\nBackup: "+backup);
   Debug.Log("Dirt surface recolored green using its original texture. Scene saved.");
  }
 }
}
#endif
