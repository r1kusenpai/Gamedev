#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class ForestVisibleGrass
    {
        private const string Request="Assets/Editor/CQC/AddVisibleGrass.request.txt";
        private const string RootName="Visible Original Grass";
        static ForestVisibleGrass()
        {
            EditorApplication.delayCall+=Pending;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Pending;};
        }
        private static void Pending()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception ex){File.WriteAllText("Logs/VisibleGrass-error.txt",ex.ToString());Debug.LogException(ex);File.Move(Request,Request+".failed");}
            finally{EditorUtility.ClearProgressBar();}
        }
        private class Group
        {
            public Material Material;
            public readonly List<CombineInstance> High=new List<CombineInstance>();
            public readonly List<CombineInstance> Low=new List<CombineInstance>();
        }
        [MenuItem("Tools/Forest/Add Visible Original Grass")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var cover=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestGroundCover>(true)).FirstOrDefault();
            if(!cover||!cover.Data||cover.Data.Batches==null)throw new InvalidOperationException("Original ground-cover data is required.");
            if(cover.Data.OriginalPlantsPerTile>2000)
            {
                // The complete source has hundreds of thousands of clumps; keep shared instanced meshes.
                Undo.RecordObject(cover,"Enable complete original grass");cover.enabled=true;EditorUtility.SetDirty(cover);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);SceneView.RepaintAll();
                Debug.Log("Complete original grass is enabled. No incomplete combined cover is needed.");return;
            }
            Transform parent=cover.transform.parent;
            if(parent.Find(RootName))return;
            string backup="Assets/ForestExpansion/Backups/MyForest-before-visible-grass-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up scene.");
            string folder="Assets/ForestExpansion/VisibleGrass";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/ForestExpansion","VisibleGrass");
            var readable=new Dictionary<string,Mesh>();
            var groups=new Dictionary<string,Group>();
            int instances=0;
            try
            {
                foreach(var batch in cover.Data.Batches)
                {
                    if(!batch.Mesh||!batch.Material||batch.Matrices==null)continue;
                    Mesh high=Read(batch.Mesh,batch.Submesh,readable);
                    Mesh low=batch.DistantMesh&&batch.Submesh<batch.DistantMesh.subMeshCount?Read(batch.DistantMesh,batch.Submesh,readable):high;
                    foreach(Matrix4x4 matrix in batch.Matrices)
                    {
                        Vector3 p=matrix.GetColumn(3);
                        string key=batch.Material.GetEntityId()+":"+Mathf.FloorToInt((p.x+50f)/100f)+":"+Mathf.FloorToInt((p.z+50f)/100f);
                        if(!groups.TryGetValue(key,out Group group)){group=new Group{Material=batch.Material};groups.Add(key,group);}
                        group.High.Add(new CombineInstance{mesh=high,subMeshIndex=0,transform=matrix});
                        group.Low.Add(new CombineInstance{mesh=low,subMeshIndex=0,transform=matrix});
                        instances++;
                    }
                }
                var root=new GameObject(RootName);root.transform.SetParent(parent);
                Undo.RegisterCreatedObjectUndo(root,"Add visible original grass");
                int index=0, renderers=0;
                foreach(var group in groups.Values)
                {
                    EditorUtility.DisplayProgressBar("Adding visible grass","Creating original grass meshes",index/(float)groups.Count);
                    var patch=new GameObject("Grass and Plants Patch "+index);patch.transform.SetParent(root.transform);
                    Mesh high=Combine(group.High,"GrassPatch_"+index+"_Near",folder);
                    Mesh low=Combine(group.Low,"GrassPatch_"+index+"_Far",folder);
                    MeshRenderer near=Renderer(patch.transform,high,group.Material,"Near");
                    MeshRenderer distant=Renderer(patch.transform,low,group.Material,"Far");
                    var lod=patch.AddComponent<LODGroup>();
                    lod.SetLODs(new[]{new LOD(.45f,new UnityEngine.Renderer[]{near}),new LOD(.002f,new UnityEngine.Renderer[]{distant})});
                    lod.RecalculateBounds();index++;renderers+=2;
                }
                if(renderers==0)throw new InvalidOperationException("No visible grass geometry could be created.");
                Undo.RecordObject(cover,"Replace custom grass renderer");cover.enabled=false;EditorUtility.SetDirty(cover);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Use Ctrl+S to save grass.");
                File.WriteAllText("Logs/VisibleGrass-report.txt","Created real visible mesh renderers for original grass/flowers/bushes/mushrooms.\nSource plant/submesh instances: "+instances+"\nMesh patches: "+index+"\nVisible renderers: "+renderers+"\nCustom rendering disabled. Scene saved.\nBackup: "+backup+"\n");
                SceneView.RepaintAll();Debug.Log("Visible original grass meshes added and saved.");
            }
            finally{foreach(Mesh mesh in readable.Values)UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.ClearProgressBar();}
        }
        private static MeshRenderer Renderer(Transform parent,Mesh mesh,Material material,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.receiveShadows=true;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
            return renderer;
        }
        private static Mesh Combine(List<CombineInstance> inputs,string name,string folder)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(inputs.ToArray(),true,true,false);mesh.RecalculateBounds();
            if(mesh.vertexCount==0)throw new InvalidOperationException("Grass patch has no vertices.");
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+".asset"));
            mesh.UploadMeshData(true);return mesh;
        }
        private static Mesh Read(Mesh source,int submesh,Dictionary<string,Mesh> cache)
        {
            string key=source.GetEntityId()+":"+submesh;
            if(cache.TryGetValue(key,out Mesh result))return result;
            using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(source))
            {
                var data=snapshot[0];int count=data.vertexCount;
                result=new Mesh{name=source.name+"_Readable",indexFormat=IndexFormat.UInt32};
                using(var vertices=new NativeArray<Vector3>(count,Allocator.Temp)){data.GetVertices(vertices);result.vertices=vertices.ToArray();}
                if(data.HasVertexAttribute(VertexAttribute.Normal))using(var normals=new NativeArray<Vector3>(count,Allocator.Temp)){data.GetNormals(normals);result.normals=normals.ToArray();}
                if(data.HasVertexAttribute(VertexAttribute.Tangent))using(var tangents=new NativeArray<Vector4>(count,Allocator.Temp)){data.GetTangents(tangents);result.tangents=tangents.ToArray();}
                if(data.HasVertexAttribute(VertexAttribute.TexCoord0))using(var uv=new NativeArray<Vector2>(count,Allocator.Temp)){data.GetUVs(0,uv);result.uv=uv.ToArray();}
                if(data.HasVertexAttribute(VertexAttribute.Color))using(var colors=new NativeArray<Color32>(count,Allocator.Temp)){data.GetColors(colors);result.colors32=colors.ToArray();}
                int indices=data.GetSubMesh(submesh).indexCount;
                using(var triangles=new NativeArray<int>(indices,Allocator.Temp)){data.GetIndices(triangles,submesh,true);result.SetIndices(triangles.ToArray(),MeshTopology.Triangles,0);}
                result.RecalculateBounds();cache[key]=result;return result;
            }
        }
    }
}
#endif
