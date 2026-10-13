#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class UndoMountainIsolation
    {
        [Serializable] private class Entry { public string Name,Guid; public bool AutoConnect; public int Group; }
        [Serializable] private class Settings { public Entry[] Entries; }
        private const string Request="Assets/Editor/CQC/UndoMountainIsolation.request.json";
        static UndoMountainIsolation()
        {
            EditorApplication.delayCall+=Apply;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Apply;};
        }
        private static void Apply()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Apply;return;}
            Scene scene=SceneManager.GetActiveScene();if(scene.name!="MyForest")return;
            try
            {
                Settings settings=JsonUtility.FromJson<Settings>(File.ReadAllText(Request));
                Terrain[] tiles=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true)).ToArray();
                foreach(Entry e in settings.Entries)
                    if(!tiles.Any(t=>t.name==e.Name)||!AssetDatabase.LoadAssetAtPath<TerrainData>(AssetDatabase.GUIDToAssetPath(e.Guid)))
                        throw new InvalidOperationException("Cannot restore original terrain "+e.Name);
                string backup="Assets/ForestExpansion/Backups/MyForest-before-undo-isolation-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
                if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not save backup.");
                foreach(Entry e in settings.Entries)
                {
                    Terrain terrain=tiles.First(t=>t.name==e.Name);
                    TerrainData data=AssetDatabase.LoadAssetAtPath<TerrainData>(AssetDatabase.GUIDToAssetPath(e.Guid));
                    Undo.RecordObject(terrain,"Undo terrain isolation");terrain.terrainData=data;
                    terrain.SetNeighbors(null,null,null,null);terrain.allowAutoConnect=e.AutoConnect;terrain.groupingID=e.Group;
                    TerrainCollider collider=terrain.GetComponent<TerrainCollider>();
                    if(collider){Undo.RecordObject(collider,"Undo terrain isolation");collider.terrainData=data;EditorUtility.SetDirty(collider);}
                    EditorUtility.SetDirty(terrain);
                }
                EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Use Ctrl+S to save undo.");
                File.WriteAllText("Logs/UndoMountainIsolation-report.txt","Restored original terrain data references, auto-connect and grouping settings on "+settings.Entries.Length+" terrains. Other scene objects preserved. Scene saved.\n");
                AssetDatabase.DeleteAsset(Request);SceneView.RepaintAll();Debug.Log("Terrain isolation undone.");
            }
            catch(Exception ex){Debug.LogException(ex);File.WriteAllText("Logs/UndoMountainIsolation-error.txt",ex.ToString());File.Move(Request,Request+".failed");}
        }
    }
}
#endif
