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
 [InitializeOnLoad] public static class GreenTownTerrain
 {
  const string Request="Assets/Editor/CQC/GreenTownTerrain.request.txt";
  static double next;
  static GreenTownTerrain(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   int group=Undo.GetCurrentGroup();
   try{Apply();AssetDatabase.DeleteAsset(Request);}
   catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GreenTownTerrain-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Forest/Use UNS Green Ground In Town")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode first.");
   var scene=SceneManager.GetActiveScene();
   var grass=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Terrain/Layers/UNS_Grass.terrainlayer");
   if(!grass)throw new InvalidOperationException("Original UNS grass layer missing.");
   var city=GameObject.Find("Lower Castle City");if(!city)throw new InvalidOperationException("Town missing.");
   var square=city.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Central Market Square");
   if(!square)throw new InvalidOperationException("Town center missing.");
   Vector3 center=square.GetComponent<Renderer>().bounds.center;
   var terrains=Terrain.activeTerrains.Where(t=>t.gameObject.scene==scene).ToArray();
   if(terrains.Length==0)throw new InvalidOperationException("No terrains in MyForest.");
   var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");if(!shader)throw new InvalidOperationException("Terrain shader missing.");
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-green-town-"+stamp+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
   string folder="Assets/ForestExpansion/GreenTown-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var material=new Material(shader){name="Selected Forest Ground"};material.SetFloat("_EnableHeightBlend",0);AssetDatabase.CreateAsset(material,folder+"/DemoTerrain.mat");
   int count=0;
   foreach(var terrain in terrains)
   {
    // Copy the current terrain so elevations, foliage, and independent editing stay intact.
    var current=terrain.terrainData;Vector3 pos=terrain.transform.position,size=current.size;
    if(pos.x>center.x+215||pos.x+size.x<center.x-215||pos.z>center.z+132||pos.z+size.z<center.z-132)continue;
    var layers=current.terrainLayers;int index=Array.IndexOf(layers,grass);
    var old=current.GetAlphamaps(0,0,current.alphamapWidth,current.alphamapHeight);
    if(index<0){index=layers.Length;layers=layers.Concat(new[]{grass}).ToArray();}
    int width=current.alphamapWidth,height=current.alphamapHeight;var alpha=new float[height,width,layers.Length];int changed=0;
    for(int z=0;z<height;z++)for(int x=0;x<width;x++)
    {
     float wx=pos.x+x*size.x/Mathf.Max(1,width-1),wz=pos.z+z*size.z/Mathf.Max(1,height-1);
     float nx=(wx-center.x)/215,nz=(wz-center.z)/132;float distance=Mathf.Sqrt(nx*nx+nz*nz);
     float blend=Mathf.Clamp01((1-distance)/.035f);
     if(blend>0)changed++;
     for(int l=0;l<layers.Length;l++)alpha[z,x,l]=Mathf.Lerp(l<old.GetLength(2)?old[z,x,l]:0,l==index?1:0,blend);
    }
    if(changed==0)continue;
    var data=UnityEngine.Object.Instantiate(current);data.name=terrain.name+" Green Town Surface";data.terrainLayers=layers;
    data.SetAlphamaps(0,0,alpha);data.SetBaseMapDirty();AssetDatabase.CreateAsset(data,folder+"/Terrain-"+terrain.GetEntityId()+".asset");
    Undo.RecordObject(terrain,"Apply selected ground surface");terrain.terrainData=data;terrain.materialTemplate=material;
    var collider=terrain.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Preserve terrain collision");collider.terrainData=data;}
    terrain.Flush();EditorUtility.SetDirty(terrain);count++;
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GreenTownTerrain-report.txt","Changed brown terrain within the lower town footprint to original UNS_Grass green ground on "+count+" tiles, with blended boundary. Center: "+center+"; radii 215m x 132m. Roads, square, grass plants, terrain heights and buildings retained. Outside town unchanged.\nBackup: "+backup);
   Debug.Log("Town brown terrain changed to original UNS green surface. Scene saved.");
  }
 }
}
#endif
