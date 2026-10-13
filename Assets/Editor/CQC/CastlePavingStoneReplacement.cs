#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class CastlePavingStoneReplacement
    {
        const string Request="Assets/Editor/CQC/CastlePavingStoneReplacement.request.txt";
        const string Textures="Assets/PavingStone/Textures/PavingStone-01/PavingStone-01-Perfect/PavingStone-01-Perfect-";
        const string MaterialPath="Assets/Building/Cobblestone/CastlePavingStone01.mat";
        static CastlePavingStoneReplacement(){EditorApplication.delayCall+=Pending;}
        static void Pending()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
            if(SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception ex){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastlePavingStoneReplacement-error.txt",ex.ToString());Debug.LogException(ex);File.Move(Request,Request+".failed");}
        }
        [MenuItem("Tools/Buildings/Use Imported Paving Stone 01")]
        public static void Apply()
        {
            var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");
            var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-paving-stone-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up castle scene.");
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("URP Lit shader missing.");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(!mat){mat=new Material(shader){name="Castle Paving Stone 01"};AssetDatabase.CreateAsset(mat,MaterialPath);}else Undo.RecordObject(mat,"Replace castle paving");
            mat.shader=shader;mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_WorkflowMode",0);mat.EnableKeyword("_SPECULAR_SETUP");
            mat.SetTexture("_BaseMap",Texture("Albedo"));mat.SetTexture("_BumpMap",Texture("NormalMap"));mat.SetFloat("_BumpScale",1);mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_SpecGlossMap",Texture("Specular"));mat.SetColor("_SpecColor",new Color(.2f,.2f,.2f));mat.SetFloat("_Smoothness",.35f);mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_OcclusionMap",Texture("Occlusion"));mat.SetFloat("_OcclusionStrength",1);mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetTexture("_ParallaxMap",Texture("HeightMap"));mat.SetFloat("_Parallax",.015f);mat.EnableKeyword("_PARALLAXMAP");
            mat.enableInstancing=true;EditorUtility.SetDirty(mat);
            int count=0;
            foreach(string groupName in new[]{"Cobblestone Castle Floors","Inner Castle Mountain"})
            {
                Transform group=mountain.transform.Find(groupName);if(!group)continue;
                foreach(var renderer in group.GetComponentsInChildren<MeshRenderer>())
                {
                    if(renderer.name=="Inner Rocky Mountain Sides")continue;
                    var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)throw new InvalidOperationException("Floor mesh missing.");
                    Undo.RecordObject(renderer,"Apply imported paving stone");renderer.sharedMaterial=mat;EditorUtility.SetDirty(renderer);
                    var mesh=filter.sharedMesh;Undo.RecordObject(mesh,"Prepare floor for stone normal map");mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
                    if(renderer.sharedMaterial.GetTexture("_BaseMap")!=mat.GetTexture("_BaseMap"))throw new InvalidOperationException("Floor texture assignment failed.");
                    count++;
                }
            }
            if(count<2)throw new InvalidOperationException("Castle floor surfaces were not found.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastlePavingStoneReplacement-report.txt","Imported Paving Stone 01 Perfect applied to "+count+" floor/ramp surfaces.\nURP material with albedo, normal, specular, occlusion and height maps. Floor tangents rebuilt.\nHouse placements and rocky sides preserved.\nBackup: "+backup);
            SceneView.RepaintAll();Debug.Log("Castle floors now use imported Paving Stone 01.");
        }
        static Texture2D Texture(string suffix)
        {
            string path=Textures+suffix+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(!texture)throw new InvalidOperationException("Paving Stone texture missing: "+path);return texture;
        }
    }
}
#endif
