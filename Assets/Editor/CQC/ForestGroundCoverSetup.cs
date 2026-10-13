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
    public static class ForestGroundCoverSetup
    {
        private const string Request = "Assets/Editor/CQC/MatchOriginalGrass.request.txt";
        private const string Name = "Original Dense Grass Flowers And Bushes";
        static ForestGroundCoverSetup()
        {
            EditorApplication.delayCall += Pending;
            EditorApplication.playModeStateChanged += s => { if(s == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Pending; };
        }
        private static void Pending()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isUpdating || EditorApplication.isCompiling) { EditorApplication.delayCall += Pending; return; }
            if (SceneManager.GetActiveScene().name != "MyForest") return;
            try { Apply(); AssetDatabase.DeleteAsset(Request); }
            catch (Exception ex) { File.WriteAllText("Logs/ForestGrass-error.txt",ex.ToString()); Debug.LogException(ex); File.Move(Request,Request+".failed"); }
            finally { EditorUtility.ClearProgressBar(); }
        }
        private class Plant
        { public Mesh Mesh, Low; public Material Material; public int Submesh; public Matrix4x4 Matrix; }
        private class Group
        { public Plant Plant; public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(); public Bounds Bounds; public bool HasBounds; }
        [MenuItem("Tools/Forest/Match Original Dense Grass")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            GameObject extension = scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Forest Surroundings (Original Style)");
            Terrain original = GameObject.Find("UNS_Terrain")?.GetComponent<Terrain>();
            if (!extension || !original) throw new InvalidOperationException("Open MyForest with the green surroundings already added.");
            Transform oldCover = extension.transform.Find(Name);
            Transform environment = original.transform.parent;
            if (!environment) throw new InvalidOperationException("The original environment hierarchy is required.");
            var plants = new List<Plant>(); int originalPlants = 0, originalGrassClumps = 0;
            foreach (string groupName in new[] { "UNS_Grass", "UNS_Flowers", "UNS_Bushes", "UNS_Mushrooms" })
            {
                // Prefab instances share their names with their container. Never use GameObject.Find here.
                Transform container = environment.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == groupName).OrderByDescending(t => t.childCount).FirstOrDefault();
                GameObject group = container ? container.gameObject : null; if (!group) continue;
                if (groupName == "UNS_Grass")
                {
                    originalGrassClumps = group.GetComponentsInChildren<LODGroup>().Length;
                    if (originalGrassClumps < 100) throw new InvalidOperationException("Selected grass source is not the dense original grass container.");
                }
                var used = new HashSet<MeshRenderer>();
                foreach (LODGroup lod in group.GetComponentsInChildren<LODGroup>())
                {
                    LOD[] levels = lod.GetLODs(); if (levels.Length == 0) continue;
                    originalPlants++;
                    var high = levels[0].renderers;
                    var low = levels[levels.Length-1].renderers;
                    for (int r=0;r<high.Length;r++)
                    {
                        MeshRenderer renderer = high[r] as MeshRenderer; if (!renderer) continue;
                        foreach (var level in levels) foreach (Renderer other in level.renderers)
                            if(other is MeshRenderer mr) used.Add(mr);
                        MeshFilter distant = r<low.Length && low[r] ? low[r].GetComponent<MeshFilter>() : null;
                        Add(renderer,distant?distant.sharedMesh:null,plants);
                    }
                }
                foreach (MeshRenderer renderer in group.GetComponentsInChildren<MeshRenderer>())
                    if (!used.Contains(renderer)) { Add(renderer,null,plants); originalPlants++; }
            }
            if (plants.Count == 0) throw new InvalidOperationException("No original grass or flowers found to copy.");
            string backup = "Assets/ForestExpansion/Backups/MyForest-before-dense-grass-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if (!EditorSceneManager.SaveScene(scene,backup,true)) throw new IOException("Could not back up scene.");
            var groups = new Dictionary<string,Group>();
            Terrain[] tiles = extension.GetComponentsInChildren<Terrain>();
            float[,,] originalPaint = original.terrainData.GetAlphamaps(0,0,original.terrainData.alphamapWidth,original.terrainData.alphamapHeight);
            foreach (Terrain tile in tiles)
            {
                EditorUtility.DisplayProgressBar("Matching original forest","Copying original grass, flowers and ground textures",Array.IndexOf(tiles,tile)/(float)tiles.Length);
                Vector3 offset = tile.transform.position - original.transform.position;
                foreach (Plant plant in plants)
                {
                    Vector3 p = plant.Matrix.GetColumn(3);
                    Vector3 q = p + offset;
                    float sourceGround = original.SampleHeight(p) + original.transform.position.y;
                    float newGround = tile.SampleHeight(q) + tile.transform.position.y;
                    Vector3 translation = offset + Vector3.up * (newGround-sourceGround-offset.y);
                    Matrix4x4 matrix = Matrix4x4.Translate(translation) * plant.Matrix;
                    Vector3 center = matrix.MultiplyPoint3x4(plant.Mesh.bounds.center);
                    string key = plant.Mesh.GetEntityId()+":"+plant.Material.GetEntityId()+":"+plant.Submesh+":"+Mathf.FloorToInt(center.x/20f)+":"+Mathf.FloorToInt(center.z/20f);
                    if (!groups.TryGetValue(key,out Group batch)) { batch=new Group {Plant=plant}; groups.Add(key,batch); }
                    batch.Matrices.Add(matrix);
                    Vector3 ext = plant.Mesh.bounds.extents;
                    Vector3 a = matrix.MultiplyVector(new Vector3(ext.x,0,0)), b=matrix.MultiplyVector(new Vector3(0,ext.y,0)), c=matrix.MultiplyVector(new Vector3(0,0,ext.z));
                    Vector3 worldExt = new Vector3(Mathf.Abs(a.x)+Mathf.Abs(b.x)+Mathf.Abs(c.x),Mathf.Abs(a.y)+Mathf.Abs(b.y)+Mathf.Abs(c.y),Mathf.Abs(a.z)+Mathf.Abs(b.z)+Mathf.Abs(c.z));
                    Bounds bounds = new Bounds(center,worldExt*2f);
                    if (batch.HasBounds) batch.Bounds.Encapsulate(bounds); else { batch.Bounds=bounds; batch.HasBounds=true; }
                }
                Undo.RecordObject(tile,"Match original ground cover");
                tile.detailObjectDensity = 0f; // Avoid drawing the sparse replacement over the copied original plants.
                TerrainData ground = tile.terrainData;
                Undo.RegisterCompleteObjectUndo(ground,"Match original ground textures");
                ground.terrainLayers = original.terrainData.terrainLayers;
                int resolution = ground.alphamapResolution, layerCount = ground.terrainLayers.Length;
                var paint = new float[resolution,resolution,layerCount];
                for(int z=0;z<resolution;z++) for(int x=0;x<resolution;x++) for(int l=0;l<layerCount;l++)
                    paint[z,x,l] = originalPaint[Mathf.RoundToInt(z/(float)(resolution-1)*(originalPaint.GetLength(0)-1)),Mathf.RoundToInt(x/(float)(resolution-1)*(originalPaint.GetLength(1)-1)),l];
                ground.SetAlphamaps(0,0,paint); EditorUtility.SetDirty(ground);
            }
            var data = ScriptableObject.CreateInstance<ForestGroundCoverData>();
            var batches = new List<ForestGroundCoverData.Batch>();
            foreach (Group group in groups.Values)
            {
                for (int start=0;start<group.Matrices.Count;start+=256)
                    batches.Add(new ForestGroundCoverData.Batch { Mesh=group.Plant.Mesh,DistantMesh=group.Plant.Low,Material=group.Plant.Material,
                        Submesh=group.Plant.Submesh,Bounds=group.Bounds,Matrices=group.Matrices.Skip(start).Take(256).ToArray() });
            }
            data.Batches=batches.ToArray(); data.OriginalPlantsPerTile=originalPlants; data.TotalPlants=originalPlants*tiles.Length;
            AssetDatabase.CreateAsset(data,AssetDatabase.GenerateUniqueAssetPath("Assets/ForestExpansion/OriginalGroundCover.asset"));
            // Remove only the previous generated cover after the corrected source data is ready.
            if (oldCover) Undo.DestroyObjectImmediate(oldCover.gameObject);
            Transform oldMeshes = extension.transform.Find("Visible Original Grass");
            if (oldMeshes) Undo.DestroyObjectImmediate(oldMeshes.gameObject);
            var holder = new GameObject(Name); holder.transform.SetParent(extension.transform);
            Undo.RegisterCreatedObjectUndo(holder,"Copy original dense forest plants");
            var cover = holder.AddComponent<ForestGroundCover>(); cover.Data=data; cover.gameObject.layer=original.gameObject.layer;
            EditorUtility.ClearProgressBar(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Ground cover added; use Ctrl+S to save.");
            File.WriteAllText("Logs/ForestGrass-report.txt","Corrected container selection: individual prefab objects have the same names as their containers.\nOriginal grass clumps per tile: "+originalGrassClumps+"\nAdded grass clumps: "+(originalGrassClumps*tiles.Length)+"\nOriginal plants per tile: "+originalPlants+"\nAdded tiles: "+tiles.Length+"\nAdded plants: "+data.TotalPlants+"\nRender batches: "+batches.Count+"\nCopied original meshes/materials/scales/layout and ground paint. Generated incomplete cover replaced. Original central forest unchanged.\nBackup: "+backup+"\nScene saved.\n");
            SceneView.RepaintAll(); Debug.Log("Original dense forest ground cover applied and saved.");
        }
        private static void Add(MeshRenderer renderer,Mesh low,List<Plant> plants)
        {
            MeshFilter filter=renderer.GetComponent<MeshFilter>(); if(!filter || !filter.sharedMesh) return;
            Material[] materials=renderer.sharedMaterials;
            for(int sub=0;sub<Mathf.Min(materials.Length,filter.sharedMesh.subMeshCount);sub++)
            {
                if(!materials[sub] || !materials[sub].enableInstancing) continue;
                plants.Add(new Plant {Mesh=filter.sharedMesh,Low=low,Material=materials[sub],Submesh=sub,Matrix=filter.transform.localToWorldMatrix});
            }
        }
    }
}
#endif

