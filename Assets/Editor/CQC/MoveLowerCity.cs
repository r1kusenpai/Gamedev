#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class MoveLowerCity
 {
  const string Request="Assets/Editor/CQC/MoveLowerCity.request.txt";
  const string Original="Assets/ForestExpansion/Backups/MyForest-before-lower-city-20261013-143033.unity";
  static double next;
  static MoveLowerCity(){EditorApplication.update+=Pending;}
  static void Pending()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
   if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||SceneManager.GetActiveScene().name!="MyForest")return;
   try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/MoveLowerCity-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}
  }
  [MenuItem("Tools/Buildings/Move City To Castle Approach")]
  public static void Apply()
  {
   var scene=SceneManager.GetActiveScene();var old=GameObject.Find("Lower Castle City");var mountain=GameObject.Find("Castle Mountain");
   if(!old||!mountain||!File.Exists(Original))throw new InvalidOperationException("Existing city, castle and original forest backup are required.");
   string backup="Assets/ForestExpansion/Backups/MyForest-before-city-relocation-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up current scene.");
   var lift=mountain.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c&&c.GetType().Name=="CastleFunicular");
   if(!lift)throw new InvalidOperationException("Funicular lower station required to locate the marked area.");
   Vector3 station=new SerializedObject(lift).FindProperty("LowerStation").vector3Value;
   Vector3 target=new Vector3(station.x+42,0,station.z-32);
   Scene source=EditorSceneManager.OpenScene(Original,OpenSceneMode.Additive);int restored=0;
   try
   {
    var actual=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).GroupBy(Key).ToDictionary(g=>g.Key,g=>g.First());
    foreach(var t in source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
    {
     if(!actual.TryGetValue(Key(t),out var dest))continue;
     var terrain=t.GetComponent<Terrain>();var current=dest.GetComponent<Terrain>();
     if(terrain&&current&&CityAsset(current.terrainData))
     {Undo.RecordObject(current,"Restore previous city forest terrain");current.terrainData=terrain.terrainData;var collider=dest.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Restore previous terrain collision");collider.terrainData=terrain.terrainData;}restored++;}
     var grass=t.GetComponent<StarterAssets.ForestGroundCover>();var currentGrass=dest.GetComponent<StarterAssets.ForestGroundCover>();
     if(grass&&currentGrass&&CityAsset(currentGrass.Data)){Undo.RecordObject(currentGrass,"Restore previous city grass");currentGrass.Data=grass.Data;restored++;}
     var mesh=t.GetComponent<MeshFilter>();var currentMesh=dest.GetComponent<MeshFilter>();
     if(mesh&&currentMesh&&CityAsset(currentMesh.sharedMesh)){Undo.RecordObject(currentMesh,"Restore previous grass patches");currentMesh.sharedMesh=mesh.sharedMesh;restored++;}
     string n=t.name.ToLowerInvariant();
     if(t.gameObject.activeSelf&&!dest.gameObject.activeSelf&&(n.Contains("spruce")||n.Contains("tree")||n.Contains("bush")||n.Contains("standard_rock")))
     {Undo.RecordObject(dest.gameObject,"Restore old city vegetation");dest.gameObject.SetActive(true);restored++;}
    }
   }
   finally{EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(scene);}
   // Build at the marked approach first, retaining the old city as an undoable object until success.
   Undo.RecordObject(old,"Relocate lower city");old.name="Previous City Relocation Backup";old.SetActive(false);
   try{LowerCastleCity.Relocating=true;LowerCastleCity.RequestedCenter=target;LowerCastleCity.Build();}
   catch{old.name="Lower Castle City";old.SetActive(true);throw;}
   finally{LowerCastleCity.Relocating=false;}
   Undo.DestroyObjectImmediate(old);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");
   File.WriteAllText("Logs/MoveLowerCity-report.txt","City moved to marked castle/funicular approach. New center: "+target+"; lower station: "+station+". Wider irregular neighborhoods, narrower streets and closer house spacing. Original forest restored at former city site ("+restored+" objects/data references). Grass now cleared only beneath roads, plaza and houses; green spaces retained. Previous distant city removed.\nCurrent scene backup: "+backup);
   Debug.Log("City moved beside the castle approach, with closer house groups and green spaces. Scene saved.");
  }
  static bool CityAsset(UnityEngine.Object asset){return asset&&AssetDatabase.GetAssetPath(asset).StartsWith("Assets/CityExpansion/Generated-");}
  static string Key(Transform t){var indices=new List<int>();Transform root=t;while(root.parent){indices.Add(root.GetSiblingIndex());root=root.parent;}indices.Reverse();return root.name+"/"+string.Join("/",indices);}
 }
}
#endif
