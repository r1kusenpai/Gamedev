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
 [InitializeOnLoad] public static class MixedUNSGroundRetry
 {
  const string Request="Assets/Editor/CQC/MixedUNSGroundRetry.request.txt";
  static double next;
  static MixedUNSGroundRetry(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   int group=Undo.GetCurrentGroup();
   try{Apply();AssetDatabase.DeleteAsset(Request);}
   catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/MixedUNSGroundRetry-error.txt",e.ToString());Debug.LogException(e);string moved=AssetDatabase.MoveAsset(Request,Request+".failed");if(!string.IsNullOrEmpty(moved))Debug.LogError(moved);}
  }
  [MenuItem("Tools/Forest/Restore Mixed UNS Ground With Verified Saving")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode first.");
   var scene=SceneManager.GetActiveScene();
   var grass=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Terrain/Layers/UNS_Grass.terrainlayer");
   if(!grass)throw new InvalidOperationException("Original UNS grass layer missing.");
   var dirt=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Terrain/Layers/UNS_Dirt.terrainlayer");
   if(!dirt)throw new InvalidOperationException("UNS dirt layer missing.");
   var terrains=Terrain.activeTerrains.Where(t=>t.gameObject.scene==scene).ToArray();
   if(terrains.Length==0)throw new InvalidOperationException("No terrains in MyForest.");
   var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");if(!shader)throw new InvalidOperationException("Terrain shader missing.");
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-mixed-uns-"+stamp+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
   string folder="Assets/ForestExpansion/MixedUNS-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var material=new Material(shader){name="Selected Forest Ground"};material.SetFloat("_EnableHeightBlend",0);AssetDatabase.CreateAsset(material,folder+"/DemoTerrain.mat");
   int count=0;double greenSum=0,dirtSum=0;long samples=0;var diagnostics=new System.Text.StringBuilder();
   foreach(var terrain in terrains)
   {
    // Copy the current terrain so elevations, foliage, and independent editing stay intact.
    var current=terrain.terrainData;Vector3 pos=terrain.transform.position,size=current.size;
    // Inspect the old painted map before replacing it, rather than assuming a request succeeded visually.
    var previous=current.GetAlphamaps(0,0,current.alphamapWidth,current.alphamapHeight);
    diagnostics.AppendLine(terrain.name+": previous layers "+string.Join(", ",current.terrainLayers.Select(l=>l?l.name:"missing"))+"; center weights "+string.Join(", ",Enumerable.Range(0,previous.GetLength(2)).Select(i=>previous[previous.GetLength(0)/2,previous.GetLength(1)/2,i].ToString("F3"))));
    // New native terrain data gives every tile its own newly allocated control textures.
    var data=new TerrainData{name=terrain.name+" Mixed UNS Surface",heightmapResolution=current.heightmapResolution,alphamapResolution=current.alphamapResolution,baseMapResolution=current.baseMapResolution,size=size};
    data.SetHeights(0,0,current.GetHeights(0,0,current.heightmapResolution,current.heightmapResolution));
    data.terrainLayers=new[]{grass,dirt};data.treePrototypes=current.treePrototypes;data.treeInstances=current.treeInstances;
    data.detailPrototypes=current.detailPrototypes;
    if(current.detailResolution>0){data.SetDetailResolution(current.detailResolution,current.detailResolutionPerPatch);for(int l=0;l<current.detailPrototypes.Length;l++)data.SetDetailLayer(0,0,l,current.GetDetailLayer(0,0,current.detailWidth,current.detailHeight,l));}
    data.wavingGrassAmount=current.wavingGrassAmount;data.wavingGrassSpeed=current.wavingGrassSpeed;data.wavingGrassStrength=current.wavingGrassStrength;data.wavingGrassTint=current.wavingGrassTint;
    data.enableHolesTextureCompression=current.enableHolesTextureCompression;data.SetHoles(0,0,current.GetHoles(0,0,current.holesResolution,current.holesResolution));
    int width=data.alphamapWidth,height=data.alphamapHeight;var alpha=new float[height,width,2];
    for(int z=0;z<height;z++)for(int x=0;x<width;x++)
    {
     float wx=pos.x+x*size.x/Mathf.Max(1,width-1),wz=pos.z+z*size.z/Mathf.Max(1,height-1);
     float noise=.7f*Mathf.PerlinNoise((wx+1847)/19f,(wz+937)/19f)+.3f*Mathf.PerlinNoise((wx+391)/6f,(wz+2367)/6f);
     float brown=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.49f,.69f,noise))*.92f;
     alpha[z,x,0]=1-brown;alpha[z,x,1]=brown;greenSum+=1-brown;dirtSum+=brown;samples++;
    }
    // Persist terrain first; creating an asset can recreate its internal control textures.
    string dataPath=folder+"/Terrain-"+terrain.GetEntityId()+".asset";AssetDatabase.CreateAsset(data,dataPath);
    data.SetAlphamaps(0,0,alpha);data.SetBaseMapDirty();EditorUtility.SetDirty(data);
    foreach(var control in data.alphamapTextures)EditorUtility.SetDirty(control);
    AssetDatabase.SaveAssets();
    var verified=data.GetAlphamaps(0,0,width,height);
    for(int z=0;z<height;z++)for(int x=0;x<width;x++)if(Mathf.Abs(verified[z,x,0]-alpha[z,x,0])>.02f)throw new InvalidOperationException("Saved texture blend failed on "+terrain.name+" at ("+x+", "+z+"): expected "+alpha[z,x,0]+", read "+verified[z,x,0]+"; transposed "+verified[x,z,0]+"; layer count "+verified.GetLength(2));
    Undo.RecordObject(terrain,"Apply selected ground surface");terrain.terrainData=data;terrain.materialTemplate=material;
    var collider=terrain.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Preserve terrain collision");collider.terrainData=data;}
    terrain.Flush();EditorUtility.SetDirty(terrain);count++;
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/MixedUNSGroundRetry-report.txt","Rebuilt independent terrain/control textures for "+count+" tiles using original UNS_Grass (green) and UNS_Dirt (brown), with organic world-space patches across tile boundaries. Average grass weight "+(greenSum/samples).ToString("P1")+", dirt "+(dirtSum/samples).ToString("P1")+". All written control-map samples verified within 0.02. Heights, trees, details, holes, roads and buildings retained.\nBackup: "+backup+"\nPrevious terrain layer diagnostics:\n"+diagnostics);
   Debug.Log("Mixed UNS green ground and brown dirt patches rebuilt and saved.");
  }
 }
}
#endif
