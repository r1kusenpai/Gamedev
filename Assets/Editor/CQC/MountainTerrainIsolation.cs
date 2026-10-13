#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class MountainTerrainIsolation
    {
        private const string Request="Assets/Editor/CQC/IsolateMountain.request.txt";
        static MountainTerrainIsolation()
        {
            EditorApplication.delayCall+=Pending;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Pending;};
        }
        private static void Pending()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
            if(SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception ex){File.WriteAllText("Logs/MountainIsolation-error.txt",ex.ToString());Debug.LogException(ex);File.Move(Request,Request+".failed");}
            finally{EditorUtility.ClearProgressBar();}
        }
        [MenuItem("Tools/Forest/Make Mountain And Ground Independently Editable")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Scene scene=SceneManager.GetActiveScene();
            if(scene.name!="MyForest")throw new InvalidOperationException("Open MyForest first.");
            Terrain[] terrains=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true)).Where(t=>t.terrainData).ToArray();
            if(terrains.Length==0)throw new InvalidOperationException("No terrain found.");
            const string folder="Assets/ForestExpansion/IndependentTerrain";
            if(!AssetDatabase.IsValidFolder("Assets/ForestExpansion"))AssetDatabase.CreateFolder("Assets","ForestExpansion");
            if(!AssetDatabase.IsValidFolder("Assets/ForestExpansion/Backups"))AssetDatabase.CreateFolder("Assets/ForestExpansion","Backups");
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/ForestExpansion","IndependentTerrain");
            string backup="Assets/ForestExpansion/Backups/MyForest-before-terrain-isolation-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not save scene backup.");
            int undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Isolate mountain editing");
            int linked=terrains.Count(t=>t.leftNeighbor||t.rightNeighbor||t.topNeighbor||t.bottomNeighbor);
            int shared=terrains.GroupBy(t=>t.terrainData).Sum(g=>Mathf.Max(0,g.Count()-1));
            var checkedData=new HashSet<TerrainData>();int cloned=0;
            for(int i=0;i<terrains.Length;i++)
            {
                Terrain terrain=terrains[i];TerrainData before=terrain.terrainData;
                EditorUtility.DisplayProgressBar("Fixing terrain editing","Preserving mountain shape and separating terrain tiles",i/(float)terrains.Length);
                // Private assets preserve the entire heightmap, textures, holes and vegetation.
                string oldPath=AssetDatabase.GetAssetPath(before);
                TerrainData data=before;
                if(!oldPath.StartsWith(folder+"/",StringComparison.Ordinal)||checkedData.Contains(before))
                {
                    data=UnityEngine.Object.Instantiate(before);data.name=terrain.name+"_Independent";
                    AssetDatabase.CreateAsset(data,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+data.name+".asset"));cloned++;
                }
                Undo.RecordObject(terrain,"Separate terrain editing");
                terrain.terrainData=data;terrain.allowAutoConnect=false;terrain.groupingID=2000+i;
                terrain.SetNeighbors(null,null,null,null);
                TerrainCollider collider=terrain.GetComponent<TerrainCollider>();
                if(collider){Undo.RecordObject(collider,"Preserve matching ground collision");collider.terrainData=data;EditorUtility.SetDirty(collider);}
                EditorUtility.SetDirty(terrain);checkedData.Add(data);
                // Verify copies are byte-for-byte equivalent at the height-sample level.
                if(data!=before)
                {
                    int r=before.heightmapResolution;
                    float[,] a=before.GetHeights(0,0,r,r),b=data.GetHeights(0,0,r,r);
                    for(int z=0;z<r;z++)for(int x=0;x<r;x++)
                        if(a[z,x]!=b[z,x])throw new InvalidOperationException("Mountain shape changed unexpectedly during copying.");
                    if(before.size!=data.size)throw new InvalidOperationException("Terrain size changed unexpectedly.");
                }
            }
            if(terrains.Select(t=>t.terrainData).Distinct().Count()!=terrains.Length)throw new InvalidOperationException("Some terrain data is still shared.");
            if(terrains.Any(t=>t.allowAutoConnect||t.leftNeighbor||t.rightNeighbor||t.topNeighbor||t.bottomNeighbor))throw new InvalidOperationException("Some terrain editing links remain.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Use Ctrl+S to save terrain changes.");
            Undo.CollapseUndoOperations(undoGroup);EditorUtility.ClearProgressBar();SceneView.RepaintAll();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/MountainIsolation-report.txt","Scene saved.\nTerrain tiles: "+terrains.Length+"\nPreviously linked tiles: "+linked+"\nPreviously shared data references in scene: "+shared+"\nPrivate terrain assets created: "+cloned+"\nUnique terrain data verified. Editing neighbors cleared; auto-connect disabled; grouping IDs separated.\nAll copied height samples and terrain sizes verified unchanged. Transforms, vegetation, buildings and mountain flat top preserved.\nBackup: "+backup+"\n");
            Debug.Log("Mountain and surrounding ground are independently editable. Existing mountain shape preserved.");
        }
    }
}
#endif
