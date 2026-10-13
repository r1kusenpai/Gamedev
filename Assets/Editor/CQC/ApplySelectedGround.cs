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
 [InitializeOnLoad] public static class ApplySelectedGround
 {
  const string Request="Assets/Editor/CQC/ApplySelectedGround.request.txt";
  static double next;
  static ApplySelectedGround(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   int group=Undo.GetCurrentGroup();
   try{Apply();AssetDatabase.DeleteAsset(Request);}
   catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ApplySelectedGround-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Forest/Apply Selected Forest Ground Surface")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode first.");
   var scene=SceneManager.GetActiveScene();
   var source=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/ForestExpansion/ForestGround_-2_-1 1.asset");
   if(!source)throw new InvalidOperationException("Selected terrain asset missing.");
   var layers=source.terrainLayers;
   if(layers.Length==0||layers.Any(l=>!l))throw new InvalidOperationException("Selected terrain has missing texture layers.");
   var original=source.GetAlphamaps(0,0,source.alphamapWidth,source.alphamapHeight);
   var terrains=Terrain.activeTerrains.Where(t=>t.gameObject.scene==scene).ToArray();
   if(terrains.Length==0)throw new InvalidOperationException("No terrains in MyForest.");
   var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");if(!shader)throw new InvalidOperationException("Terrain shader missing.");
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-selected-ground-"+stamp+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
   string folder="Assets/ForestExpansion/SelectedGround-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var material=new Material(shader){name="Selected Forest Ground"};material.SetFloat("_EnableHeightBlend",0);AssetDatabase.CreateAsset(material,folder+"/DemoTerrain.mat");
   int count=0;
   foreach(var terrain in terrains)
   {
    // Copy the current terrain so elevations, foliage, and independent editing stay intact.
    var data=UnityEngine.Object.Instantiate(terrain.terrainData);data.name=terrain.name+" Selected Surface";data.terrainLayers=layers;
    int width=data.alphamapWidth,height=data.alphamapHeight;var alpha=new float[height,width,layers.Length];
    for(int z=0;z<height;z++)for(int x=0;x<width;x++)
    {
     float sx=x*(original.GetLength(1)-1f)/Mathf.Max(1,width-1),sz=z*(original.GetLength(0)-1f)/Mathf.Max(1,height-1);
     int x0=Mathf.FloorToInt(sx),z0=Mathf.FloorToInt(sz),x1=Mathf.Min(x0+1,original.GetLength(1)-1),z1=Mathf.Min(z0+1,original.GetLength(0)-1);
     for(int l=0;l<layers.Length;l++)alpha[z,x,l]=Mathf.Lerp(Mathf.Lerp(original[z0,x0,l],original[z0,x1,l],sx-x0),Mathf.Lerp(original[z1,x0,l],original[z1,x1,l],sx-x0),sz-z0);
    }
    data.SetAlphamaps(0,0,alpha);data.SetBaseMapDirty();AssetDatabase.CreateAsset(data,folder+"/Terrain-"+terrain.GetEntityId()+".asset");
    Undo.RecordObject(terrain,"Apply selected ground surface");terrain.terrainData=data;terrain.materialTemplate=material;
    var collider=terrain.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Preserve terrain collision");collider.terrainData=data;}
    terrain.Flush();EditorUtility.SetDirty(terrain);count++;
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ApplySelectedGround-report.txt","Applied texture layers and original painted surface from Assets/ForestExpansion/ForestGround_-2_-1 1.asset to "+count+" terrain tiles. Layers: "+string.Join(", ",layers.Select(l=>l.name))+". Heights, foliage, buildings and roads retained; independent terrain copies used.\nBackup: "+backup);
   Debug.Log("Selected ForestGround surface applied to all terrain and saved.");
  }
 }
}
#endif
