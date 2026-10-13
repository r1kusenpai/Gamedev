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
 [InitializeOnLoad] public static class RestoreDemoGround
 {
  const string Request="Assets/Editor/CQC/RestoreDemoGround.request.txt";
  static double next;
  static RestoreDemoGround(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   int group=Undo.GetCurrentGroup();
   try{Apply();AssetDatabase.DeleteAsset(Request);}
   catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RestoreDemoGround-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Forest/Restore Original Demo Ground Blend")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode first.");
   var scene=SceneManager.GetActiveScene();
   var source=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Terrain/Data/UNS_Terrain.asset");
   if(!source)throw new InvalidOperationException("Original demo TerrainData missing.");
   var layers=source.terrainLayers;
   int grass=Array.FindIndex(layers,l=>l&&l.name.Contains("Grass"));
   int dirt=Array.FindIndex(layers,l=>l&&l.name.Contains("Dirt"));
   if(grass<0||dirt<0)throw new InvalidOperationException("Demo terrain must contain both original grass and dirt layers.");
   var original=source.GetAlphamaps(0,0,source.alphamapWidth,source.alphamapHeight);
   float grassMax=0,dirtMax=0;
   for(int z=0;z<original.GetLength(0);z++)for(int x=0;x<original.GetLength(1);x++){grassMax=Mathf.Max(grassMax,original[z,x,grass]);dirtMax=Mathf.Max(dirtMax,original[z,x,dirt]);}
   if(grassMax<.1f||dirtMax<.1f)throw new InvalidOperationException("Original demo no longer contains a mixed grass/dirt map; nothing changed.");
   var terrains=Terrain.activeTerrains.Where(t=>t.gameObject.scene==scene).ToArray();
   if(terrains.Length==0)throw new InvalidOperationException("No terrains in MyForest.");
   var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");if(!shader)throw new InvalidOperationException("Terrain shader missing.");
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-demo-blend-"+stamp+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
   string folder="Assets/ForestExpansion/DemoGround-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var material=new Material(shader){name="Original Demo Grass and Dirt"};material.SetFloat("_EnableHeightBlend",0);AssetDatabase.CreateAsset(material,folder+"/DemoTerrain.mat");
   int count=0;
   foreach(var terrain in terrains)
   {
    // Copy the current terrain so elevations, foliage, and independent editing stay intact.
    var data=UnityEngine.Object.Instantiate(terrain.terrainData);data.name=terrain.name+" Original Demo Blend";data.terrainLayers=layers;
    int width=data.alphamapWidth,height=data.alphamapHeight;var alpha=new float[height,width,layers.Length];
    for(int z=0;z<height;z++)for(int x=0;x<width;x++)
    {
     float sx=x*(original.GetLength(1)-1f)/Mathf.Max(1,width-1),sz=z*(original.GetLength(0)-1f)/Mathf.Max(1,height-1);
     int x0=Mathf.FloorToInt(sx),z0=Mathf.FloorToInt(sz),x1=Mathf.Min(x0+1,original.GetLength(1)-1),z1=Mathf.Min(z0+1,original.GetLength(0)-1);
     for(int l=0;l<layers.Length;l++)alpha[z,x,l]=Mathf.Lerp(Mathf.Lerp(original[z0,x0,l],original[z0,x1,l],sx-x0),Mathf.Lerp(original[z1,x0,l],original[z1,x1,l],sx-x0),sz-z0);
    }
    data.SetAlphamaps(0,0,alpha);data.SetBaseMapDirty();AssetDatabase.CreateAsset(data,folder+"/Terrain-"+terrain.GetEntityId()+".asset");
    Undo.RecordObject(terrain,"Restore demo ground blend");terrain.terrainData=data;terrain.materialTemplate=material;
    var collider=terrain.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Restore demo terrain collision");collider.terrainData=data;}
    terrain.Flush();EditorUtility.SetDirty(terrain);count++;
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RestoreDemoGround-report.txt","Restored original demo grass/dirt texture layers and resampled original painted blend on "+count+" tiles. Layers: "+string.Join(", ",layers.Select(l=>l.name))+". Demo maximum grass weight: "+grassMax+"; dirt weight: "+dirtMax+". Heights, foliage, buildings and roads retained.\nBackup: "+backup);
   Debug.Log("Original demo green grass and brown dirt blend restored and saved.");
  }
 }
}
#endif
