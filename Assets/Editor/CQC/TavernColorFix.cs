#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class TavernColorFix
    {
        private const string Request="Assets/Editor/CQC/FixTavernColor.request.txt";
        private const string Textures="Assets/Building/TavernTexturesRecovered/";
        private const string Model="Assets/Building/ImageToStl.com_tavern(1).fbx";
        static TavernColorFix(){EditorApplication.delayCall+=Pending;}
        private static void Pending()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception ex){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TavernColor-error.txt",ex.ToString());Debug.LogException(ex);File.Move(Request,Request+".failed");}
        }
        [MenuItem("Tools/Buildings/Restore Tavern Original Colors")]
        public static void Apply()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("URP Lit shader not ready.");
            ModelImporter model=AssetImporter.GetAtPath(Model) as ModelImporter;if(!model)throw new InvalidOperationException("Tavern model not found.");
            string[] names={"M_010","M_021","M_032"};string report="Recovered embedded texture images from converted FBX and restored original tavern colors.\n";
            for(int i=1;i<=3;i++)
            {
                string prefix="M_0"+i;
                Texture2D color=Load(prefix+"_baseColor.png",TextureImporterType.Default,true);
                Texture2D normal=Load(prefix+"_normal.png",TextureImporterType.NormalMap,false);
                Texture2D metallic=Load(prefix+"_UnityMetallicSmoothness.png",TextureImporterType.Default,false);
                string path="Assets/Building/TavernMaterials/"+names[i-1]+".mat";
                Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
                Undo.RecordObject(material,"Restore tavern color textures");material.shader=shader;
                material.SetTexture("_BaseMap",color);material.SetColor("_BaseColor",Color.white);
                material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",1f);material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap",metallic);material.SetFloat("_Smoothness",1f);material.SetFloat("_Metallic",1f);
                material.SetFloat("_SmoothnessTextureChannel",0f);material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");material.enableInstancing=true;
                model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),names[i-1]),material);
                EditorUtility.SetDirty(material);
                if(material.GetTexture("_BaseMap")!=color)throw new InvalidOperationException("Color texture assignment failed.");
                report+=names[i-1]+": base color, normal, and converted metallic/smoothness textures assigned.\n";
            }
            AssetDatabase.SaveAssets();model.SaveAndReimport();
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TavernColor-report.txt",report+"All three imported materials remapped. Building transform and mesh unchanged.\n");
            SceneView.RepaintAll();Debug.Log("Tavern original color textures recovered and connected.");
            EditorApplication.delayCall+=Preview;
        }
        private static Texture2D Load(string name,TextureImporterType type,bool srgb)
        {
            string path=Textures+name;
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Texture import not ready: "+name);
            importer.textureType=type;importer.sRGBTexture=srgb;importer.maxTextureSize=4096;importer.mipmapEnabled=true;
            importer.SaveAndReimport();
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!texture)throw new InvalidOperationException("Texture not loaded: "+name);return texture;
        }
        private static void Preview()
        {
            PreviewRenderUtility preview=null;GameObject go=null;Texture2D capture=null;
            try
            {
                GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(Model);if(!model)return;
                preview=new PreviewRenderUtility();go=UnityEngine.Object.Instantiate(model);go.transform.localScale=Vector3.one*1000f;preview.AddSingleGO(go);
                Renderer[] renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
                Bounds bounds=renderers[0].bounds;foreach(Renderer renderer in renderers)bounds.Encapsulate(renderer.bounds);
                float extent=Mathf.Max(bounds.extents.magnitude,.01f);
                preview.camera.transform.position=bounds.center+new Vector3(1f,.6f,-1.2f).normalized*extent*2.9f;
                preview.camera.transform.LookAt(bounds.center);preview.camera.nearClipPlane=extent*.005f;preview.camera.farClipPlane=extent*20f;
                preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35f,35f,0f);preview.lights[1].intensity=.8f;
                preview.BeginPreview(new Rect(0,0,800,600),GUIStyle.none);preview.Render(true);Texture result=preview.EndPreview();
                RenderTexture previous=RenderTexture.active;RenderTexture.active=result as RenderTexture;
                capture=new Texture2D(800,600,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,800,600),0,0);capture.Apply();RenderTexture.active=previous;
                File.WriteAllBytes("Logs/Tavern-color-preview.png",capture.EncodeToPNG());
            }
            catch(Exception ex){File.WriteAllText("Logs/Tavern-preview-note.txt",ex.Message);}
            finally{if(capture)UnityEngine.Object.DestroyImmediate(capture);if(preview!=null)preview.Cleanup();else if(go)UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
