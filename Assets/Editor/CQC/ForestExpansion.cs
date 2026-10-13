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
    public static class ForestExpansion
    {
        private const string Request = "Assets/Editor/CQC/RestoreGreenForest.request.txt";
        private const string RootName = "Forest Surroundings (Original Style)";
        private const string Folder = "Assets/ForestExpansion";
        static ForestExpansion()
        {
            EditorApplication.delayCall += ApplyPending;
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyPending; };
        }
        private static void ApplyPending()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += ApplyPending; return; }
            if (SceneManager.GetActiveScene().name != "MyForest") return;
            try { Build(); File.Delete(Request); }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                File.WriteAllText("Logs/ForestOriginalStyle-error.txt", ex.ToString());
                Debug.LogException(ex);
                File.Move(Request, Request + ".failed");
            }
        }
        [MenuItem("Tools/Forest/Restore Original And Fill Surroundings")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "MyForest") throw new InvalidOperationException("Open MyForest before filling its edges.");
            if (scene.GetRootGameObjects().Any(go => go.name == RootName)) return;
            Terrain source = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Terrain>())
                .FirstOrDefault(t => t.name == "UNS_Terrain");
            if (!source || !source.terrainData || source.terrainData.terrainLayers.Length == 0)
                throw new InvalidOperationException("The original forest terrain and its layers are required.");
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/InnerverseInteractive" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            GameObject[] trees = FindPrefabs(paths, "UNS_Spruce_");
            GameObject[] rocks = FindPrefabs(paths, "UNS_Standard_Rock_");
            GameObject[] cliffs = FindPrefabs(paths, "UNS_Rock_Cliff_");
            GameObject[] bushes = FindPrefabs(paths, "UNS_Bush");
            if (trees.Length == 0 || rocks.Length == 0 || cliffs.Length == 0)
                throw new InvalidOperationException("The nature pack's trees, rocks, and cliffs must finish importing first.");
            Directory.CreateDirectory("Logs");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "ForestExpansion");
            if (!AssetDatabase.IsValidFolder(Folder + "/Backups")) AssetDatabase.CreateFolder(Folder,"Backups");
            string backup = Folder + "/Backups/MyForest-before-edges-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not back up the current scene.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "ForestExpansion");
            int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Fill Forest Edges");
            GameObject oldExpansion = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Forest Edge Expansion");
            if (oldExpansion) Undo.DestroyObjectImmediate(oldExpansion);
            Undo.RecordObject(source, "Restore original forest terrain links");
            source.SetNeighbors(null,null,null,null);
            var root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Fill Forest Edges");
            var props = new GameObject("Rocks and Bushes"); props.transform.SetParent(root.transform);
            var forestTrees = new GameObject("Original Spruce Trees"); forestTrees.transform.SetParent(root.transform);
            TerrainData original = source.terrainData;
            Vector3 origin = source.transform.position, size = original.size;
            var random = new System.Random(4217);
            var tiles = new Dictionary<Vector2Int, Terrain> { [Vector2Int.zero] = source };
            int treeCount = 0, rockCount = 0, bushCount = 0;
            int ring = 2;
            GameObject originalRocks = GameObject.Find("UNS_Rocks");
            if (originalRocks)
            {
                foreach (Renderer renderer in originalRocks.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = renderer.bounds;
                    float needed = Mathf.Max((origin.x-b.min.x)/size.x, (b.max.x-origin.x-size.x)/size.x,
                        (origin.z-b.min.z)/size.z, (b.max.z-origin.z-size.z)/size.z);
                    ring = Mathf.Max(ring, Mathf.CeilToInt(needed + 0.15f));
                }
            }
            ring = Mathf.Clamp(ring,2,4);
            int width = ring * 2 + 1;
            int expectedTiles = width * width - 1;
            TerrainLayer greenGrass = AssetDatabase.FindAssets("UNS_Grass t:TerrainLayer")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TerrainLayer>).FirstOrDefault();
            if (!greenGrass) throw new InvalidOperationException("Original green grass terrain layer not found.");
            Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            Material greenMaterial = terrainShader ? new Material(terrainShader) : source.materialTemplate ? new Material(source.materialTemplate) : null;
            if (greenMaterial) AssetDatabase.CreateAsset(greenMaterial, AssetDatabase.GenerateUniqueAssetPath(Folder + "/OriginalGreenTerrain.mat"));
            int grassLayer = Array.FindIndex(original.terrainLayers, l => l && l.name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0);
            if (grassLayer < 0) grassLayer = 0;
            GameObject grass = FindPrefabs(paths, "UNS_Grass").FirstOrDefault();
            if (grass && !grass.GetComponent<MeshFilter>())
            {
                // Terrain detail meshes need the actual mesh object, not the prefab's empty LOD-group root.
                MeshFilter grassMesh = grass.GetComponentsInChildren<MeshFilter>().FirstOrDefault();
                grass = grassMesh ? grassMesh.gameObject : null;
            }
            for (int tz = -ring; tz <= ring; tz++) for (int tx = -ring; tx <= ring; tx++)
            {
                if (tx == 0 && tz == 0) continue;
                EditorUtility.DisplayProgressBar("Expanding forest", "Adding ground, trees and rocks", ((tz + ring) * width + tx + ring) / (width * width));
                Vector3 position = origin + new Vector3(tx * size.x, 0f, tz * size.z);
                var data = new TerrainData { name = "ForestGround_" + tx + "_" + tz, heightmapResolution = 129,
                    alphamapResolution = 128, baseMapResolution = 128 };
                data.size = new Vector3(size.x, Mathf.Max(size.y, 65f), size.z);
                data.terrainLayers = new[] { greenGrass };
                var heights = new float[129,129];
                for (int z = 0; z < 129; z++) for (int x = 0; x < 129; x++)
                    heights[z,x] = Height(original, origin, position.x + x * size.x / 128f, position.z + z * size.z / 128f) / data.size.y;
                data.SetHeights(0,0,heights);
                var paint = new float[128,128,data.terrainLayers.Length];
                for (int z = 0; z < 128; z++) for (int x = 0; x < 128; x++) paint[z,x,0] = 1f;
                data.SetAlphamaps(0,0,paint);
                data.treePrototypes = Array.Empty<TreePrototype>();
                var instances = new List<TreeInstance>();
                float spacing = 13f;
                for (float z = 4f; z < size.z - 3f; z += spacing) for (float x = 4f; x < size.x - 3f; x += spacing)
                {
                    float px = Mathf.Clamp(x + Range(random,-2.5f,2.5f), 2f, size.x - 2f);
                    float pz = Mathf.Clamp(z + Range(random,-2.5f,2.5f), 2f, size.z - 2f);
                    float wx = position.x + px, wz = position.z + pz;
                    // Leave intermittent clearings and wide approaches through the forest.
                    if (Mathf.PerlinNoise(wx * 0.023f + 20f, wz * 0.023f + 90f) < 0.32f || random.NextDouble() < 0.13) continue;
                    if (EdgeDistance(original, origin, wx, wz) < 2f) continue;
                    float scale = Range(random,0.78f,1.24f);
                    float y = origin.y + Height(original,origin,wx,wz);
                    // Real prefab instances use the original LOD renderers, materials and trunk colliders.
                    Spawn(trees[random.Next(trees.Length)],forestTrees.transform,new Vector3(wx,y,wz),scale,random);
                    treeCount++;
                }
                if (original.detailPrototypes.Length > 0 || grass)
                {
                    data.SetDetailResolution(128,16);
                    data.detailPrototypes = original.detailPrototypes.Length > 0 ? original.detailPrototypes : new[] {
                        new DetailPrototype { prototype = grass, usePrototypeMesh = true, useInstancing = true,
                            renderMode = DetailRenderMode.VertexLit, minWidth = 0.8f, maxWidth = 1.2f, minHeight = 0.8f, maxHeight = 1.3f,
                            healthyColor = Color.white, dryColor = Color.white, noiseSpread = 0.2f } };
                    var density = new int[128,128];
                    for (int z = 0; z < 128; z++) for (int x = 0; x < 128; x++)
                    {
                        float wx = position.x + x * size.x / 128f, wz = position.z + z * size.z / 128f;
                        density[z,x] = Mathf.PerlinNoise(wx * 0.08f + 16f, wz * 0.08f + 48f) > 0.34f ? 2 : 0;
                    }
                    data.SetDetailLayer(0,0,0,density);
                }
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + data.name + ".asset");
                AssetDatabase.CreateAsset(data,assetPath);
                GameObject tileObject = Terrain.CreateTerrainGameObject(data);
                tileObject.name = data.name; tileObject.transform.SetParent(root.transform); tileObject.transform.position = position;
                tileObject.layer = source.gameObject.layer;
                Terrain tile = tileObject.GetComponent<Terrain>(); tile.materialTemplate = greenMaterial ? greenMaterial : source.materialTemplate;
                tile.allowAutoConnect = false;
                tile.drawInstanced = true; tile.heightmapPixelError = 4f;
                tile.treeDistance = 380f; tile.treeBillboardDistance = 75f; tile.treeMaximumFullLODCount = 120;
                tile.detailObjectDistance = 100f; tile.detailObjectDensity = 0.65f;
                var colliderSettings = new SerializedObject(tileObject.GetComponent<TerrainCollider>());
                var treeCollision = colliderSettings.FindProperty("m_EnableTreeColliders");
                if (treeCollision != null) { treeCollision.boolValue = true; colliderSettings.ApplyModifiedPropertiesWithoutUndo(); }
                tiles[new Vector2Int(tx,tz)] = tile;
                for (int i = 0; i < 7; i++)
                {
                    float wx = position.x + Range(random,4f,size.x-4f), wz = position.z + Range(random,4f,size.z-4f);
                    float y = origin.y + Height(original, origin,wx,wz);
                    Spawn(rocks[random.Next(rocks.Length)],props.transform,new Vector3(wx,y-0.12f,wz), Range(random,0.85f,1.7f), random); rockCount++;
                }
                if (bushes.Length > 0) for (int i = 0; i < 10; i++)
                {
                    float wx = position.x + Range(random,3f,size.x-3f), wz = position.z + Range(random,3f,size.z-3f);
                    Spawn(bushes[random.Next(bushes.Length)],props.transform,new Vector3(wx,origin.y+Height(original,origin,wx,wz),wz),Range(random,0.8f,1.35f),random); bushCount++;
                }
            }
            foreach (var pair in tiles)
            {
                tiles.TryGetValue(pair.Key + Vector2Int.left,out Terrain left);
                tiles.TryGetValue(pair.Key + Vector2Int.up,out Terrain top);
                tiles.TryGetValue(pair.Key + Vector2Int.right,out Terrain right);
                tiles.TryGetValue(pair.Key + Vector2Int.down,out Terrain bottom);
                // Keep the original tile untouched; unequal resolutions cannot be linked as neighbors.
                if (pair.Value == source) continue;
                if (left && left.terrainData.heightmapResolution != pair.Value.terrainData.heightmapResolution) left = null;
                if (top && top.terrainData.heightmapResolution != pair.Value.terrainData.heightmapResolution) top = null;
                if (right && right.terrainData.heightmapResolution != pair.Value.terrainData.heightmapResolution) right = null;
                if (bottom && bottom.terrainData.heightmapResolution != pair.Value.terrainData.heightmapResolution) bottom = null;
                pair.Value.SetNeighbors(left,top,right,bottom);
            }
            // Preserve the original cliffs; the ground extends through their interior and just beyond them.
            EditorUtility.ClearProgressBar();
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            Physics.SyncTransforms();
            int collisionChecks = 0;
            foreach (var pair in tiles.Where(p => p.Key != Vector2Int.zero))
            {
                Vector3 test = pair.Value.transform.position + new Vector3(size.x*.5f,1000f,size.z*.5f);
                if (pair.Value.GetComponent<TerrainCollider>().Raycast(new Ray(test,Vector3.down),out RaycastHit hit,2000f)) collisionChecks++;
            }
            if (collisionChecks != expectedTiles) throw new InvalidOperationException("Some added ground did not pass collision checks.");
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Forest created but scene could not be saved. Use Ctrl+S.");
            Undo.CollapseUndoOperations(group);
            File.WriteAllText("Logs/ForestOriginalStyle-report.txt", "Saved " + scene.path + "\nBackup: " + backup + "\nRemoved previous expansion; preserved original forest and cliffs.\nAdded " + expectedTiles + " green ground tiles covering " + (size.x*width) + " x " + (size.z*width) + " metres.\nTrees: " + treeCount + "\nRocks: " + rockCount + "\nBushes: " + bushCount + "\nGround collision checks: " + collisionChecks + "/" + expectedTiles + "\n");
            Debug.Log("Forest edges filled with land, trees, grass and rocks. Scene saved. Backup: " + backup);
        }
        private static GameObject[] FindPrefabs(string[] paths,string prefix) => paths
            .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(prefix,StringComparison.Ordinal))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p=>p).ToArray();
        private static float Range(System.Random random,float min,float max) => min+(float)random.NextDouble()*(max-min);
        private static void Spawn(GameObject prefab,Transform parent,Vector3 position,float scale,System.Random random)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            go.transform.position = position; go.transform.rotation = Quaternion.Euler(0f,Range(random,0f,360f),0f);
            go.transform.localScale = prefab.transform.localScale * scale;
        }
        private static float EdgeDistance(TerrainData data,Vector3 origin,float x,float z)
        {
            float dx = Mathf.Max(origin.x-x,0f,x-origin.x-data.size.x);
            float dz = Mathf.Max(origin.z-z,0f,z-origin.z-data.size.z);
            return Mathf.Sqrt(dx*dx+dz*dz);
        }
        private static float Height(TerrainData data,Vector3 origin,float x,float z)
        {
            float edge = data.GetInterpolatedHeight(Mathf.Clamp01((x-origin.x)/data.size.x),Mathf.Clamp01((z-origin.z)/data.size.z));
            float distance = EdgeDistance(data,origin,x,z);
            float blend = Mathf.SmoothStep(0f,1f,Mathf.Clamp01(distance/22f));
            float rolling = (Mathf.PerlinNoise(x*.012f+120f,z*.012f+85f)-.42f)*5f;
            float outerRise = 0f;
            return Mathf.Max(0f,edge+(rolling+outerRise)*blend);
        }
    }
}
#endif
