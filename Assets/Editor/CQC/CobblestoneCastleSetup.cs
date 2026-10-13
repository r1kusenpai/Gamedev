#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class CobblestoneCastleSetup
    {
        const string Request="Assets/Editor/CQC/CobblestoneCastle.request.txt",Folder="Assets/Building/Cobblestone",Name="Cobblestone Castle Floors";
        static CobblestoneCastleSetup(){EditorApplication.delayCall+=Pending;}
        static void Pending()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
            if(SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception ex){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CobblestoneCastle-error.txt",ex.ToString());Debug.LogException(ex);File.Move(Request,Request+".failed");}
        }
        [MenuItem("Tools/Buildings/Cobblestone Floor And Outer House Ring")]
        public static void Apply()
        {
            GameObject mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain is required.");
            if(mountain.transform.Find(Name))return;
            Transform summit=mountain.transform.Find("Flat House Building Area"),houses=mountain.transform.Find("One Ring Of Houses");
            if(!summit||!houses)throw new InvalidOperationException("Mountain summit and house ring are required.");
            Bounds area=summit.GetComponent<Renderer>().bounds;Vector3 center=area.center;float floorY=area.max.y+.12f;
            Scene scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-cobblestone-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not save backup.");
            Texture2D stone=BakeStone();Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            var material=new Material(shader){name="OriginalCobblestoneFloor"};material.SetTexture("_BaseMap",stone);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.08f);
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Folder+"/OriginalCobblestoneFloor.mat"));
            var root=new GameObject(Name);root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add cobblestone castle floors");
            MeshCollider ring=Floor(root.transform,"Outer House Floor",center,floorY,area.extents.x,area.extents.z,.64f,material);
            MeshCollider inside=Floor(root.transform,"Level Inner Courtyard Floor",center,floorY+.03f,area.extents.x*.64f,area.extents.z*.64f,0f,material);
            Renderer old=summit.GetComponent<Renderer>();Undo.RecordObject(old,"Replace grass block with stone paving");old.enabled=false;
            int count=houses.childCount,checks=0;
            for(int i=0;i<count;i++)
            {
                Transform house=houses.GetChild(i);Undo.RecordObject(house,"Move house to outside ring");
                float angle=(i+.5f)*Mathf.PI*2/count;
                Vector3 position=center+new Vector3(Mathf.Cos(angle)*area.extents.x*.85f,0,Mathf.Sin(angle)*area.extents.z*.85f);
                house.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(center-position,Vector3.up));house.position=position;
                Bounds b=BoundsOf(house.gameObject);float maxWidth=Mathf.Min(14f,area.extents.z*.22f);
                float factor=Mathf.Min(1f,maxWidth/Mathf.Max(b.size.x,b.size.z));house.localScale*=factor;
                b=BoundsOf(house.gameObject);house.position+=new Vector3(position.x-b.center.x,floorY-b.min.y+.02f,position.z-b.center.z);
                b=BoundsOf(house.gameObject);
                foreach(Vector3 p in new[]{new Vector3(b.min.x,floorY+100,b.min.z),new Vector3(b.max.x,floorY+100,b.min.z),new Vector3(b.min.x,floorY+100,b.max.z),new Vector3(b.max.x,floorY+100,b.max.z)})
                {if(!ring.Raycast(new Ray(p,Vector3.down),out RaycastHit hit,150f))throw new InvalidOperationException("House footprint misses outside floor.");checks++;}
                PrefabUtility.RecordPrefabInstancePropertyModifications(house);
            }
            foreach(Vector3 offset in new[]{Vector3.zero,new Vector3(20,0,0),new Vector3(-20,0,0),new Vector3(0,0,20),new Vector3(0,0,-20)})
                if(!inside.Raycast(new Ray(center+offset+Vector3.up*100f,Vector3.down),out RaycastHit hit,150f)||Mathf.Abs(hit.point.y-(floorY+.03f))>.02f)
                    throw new InvalidOperationException("Inner floor is not level.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Use Ctrl+S to save castle.");
            Selection.activeGameObject=root;SceneView.RepaintAll();Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CobblestoneCastle-report.txt","Cobblestone surface projected from provided FBX with its three original color textures.\nHouses moved to outer ring: "+count+"\nHouse footprint checks passed: "+checks+"\nSeparate inner courtyard is horizontal. Five level/collision checks passed.\nRocky mountain sides unchanged. Scene saved.\nBackup: "+backup+"\n");
            Debug.Log("Cobblestone top floor ready. Houses moved outside; centre floor is level.");
        }
        static Texture2D BakeStone()
        {
            string modelPath=Folder+"/cobblestone.fbx";AssetDatabase.ImportAsset(modelPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;if(!importer)throw new InvalidOperationException("Cobblestone import not ready.");
            Shader lit=Shader.Find("Universal Render Pipeline/Lit");
            for(int i=0;i<3;i++)
            {
                string imagePath=Folder+"/"+i+".jpg";AssetDatabase.ImportAsset(imagePath,ImportAssetOptions.ForceSynchronousImport);
                Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);if(!texture)throw new InvalidOperationException("Stone texture missing: "+i);
                string name=i==0?"Material":"Material.00"+i;
                var material=new Material(lit){name="Cobblestone_Source_"+i};material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
                AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+material.name+".mat"));
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),material);
            }
            importer.SaveAndReimport();GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);if(!prefab)throw new InvalidOperationException("Provided stone FBX could not import.");
            PreviewRenderUtility preview=null;Texture2D capture=null;var temporary=new System.Collections.Generic.List<Material>();
            try
            {
                preview=new PreviewRenderUtility();GameObject go=UnityEngine.Object.Instantiate(prefab);preview.AddSingleGO(go);
                Renderer[] renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new InvalidOperationException("Stone has no mesh renderers.");
                Bounds b=renderers[0].bounds;
                foreach(Renderer renderer in renderers)
                {
                    b.Encapsulate(renderer.bounds);Material[] mats=renderer.sharedMaterials;
                    for(int i=0;i<mats.Length;i++)
                    {
                        if(!mats[i]||!mats[i].GetTexture("_BaseMap"))throw new InvalidOperationException("Cobblestone source material has no color texture.");
                        var unlit=new Material(Shader.Find("Universal Render Pipeline/Unlit"));unlit.SetTexture("_BaseMap",mats[i].GetTexture("_BaseMap"));unlit.SetColor("_BaseColor",Color.white);temporary.Add(unlit);mats[i]=unlit;
                    }
                    renderer.sharedMaterials=mats;
                }
                float span=Mathf.Max(b.extents.x,b.extents.z);
                preview.camera.orthographic=true;preview.camera.orthographicSize=span*.88f;
                preview.camera.transform.position=b.center+Vector3.up*Mathf.Max(span*4f,10f);preview.camera.transform.LookAt(b.center,Vector3.forward);
                preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=Mathf.Max(span*10f,100f);preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.35f,.32f,.27f);
                preview.BeginPreview(new Rect(0,0,2048,2048),GUIStyle.none);preview.Render(true);Texture result=preview.EndPreview();
                if(!(result is RenderTexture target))throw new InvalidOperationException("Stone surface render failed.");
                RenderTexture previous=RenderTexture.active;RenderTexture.active=target;
                capture=new Texture2D(2048,2048,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,2048,2048),0,0);capture.Apply();RenderTexture.active=previous;
                string path=Folder+"/CobblestoneSurface.png";File.WriteAllBytes(path,capture.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var texImporter=AssetImporter.GetAtPath(path) as TextureImporter;texImporter.wrapMode=TextureWrapMode.Repeat;texImporter.sRGBTexture=true;texImporter.maxTextureSize=2048;texImporter.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            finally{if(capture)UnityEngine.Object.DestroyImmediate(capture);if(preview!=null)preview.Cleanup();foreach(Material mat in temporary)UnityEngine.Object.DestroyImmediate(mat);}
        }
        static MeshCollider Floor(Transform parent,string name,Vector3 center,float y,float rx,float rz,float hole,Material mat)
        {
            const int n=120;var vertices=new System.Collections.Generic.List<Vector3>();var uv=new System.Collections.Generic.List<Vector2>();var triangles=new System.Collections.Generic.List<int>();
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            if(hole==0f){vertices.Add(go.transform.InverseTransformPoint(new Vector3(center.x,y,center.z)));uv.Add(new Vector2(center.x/4f,center.z/4f));}
            int rings=hole==0?1:2;
            for(int row=0;row<rings;row++)for(int i=0;i<n;i++)
            {
                float r=row==0?1f:hole,a=i*Mathf.PI*2/n;Vector3 world=new Vector3(center.x+Mathf.Cos(a)*rx*r,y,center.z+Mathf.Sin(a)*rz*r);
                vertices.Add(go.transform.InverseTransformPoint(world));uv.Add(new Vector2(world.x/4f,world.z/4f));
            }
            for(int i=0;i<n;i++)
            {
                int next=(i+1)%n;
                if(hole==0)triangles.AddRange(new[]{0,next+1,i+1});
                else triangles.AddRange(new[]{i,n+i,next,next,n+i,n+next});
            }
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+name.Replace(" ","_")+".asset"));
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;MeshCollider collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;return collider;
        }
        static Bounds BoundsOf(GameObject go){Renderer[] r=go.GetComponentsInChildren<Renderer>();Bounds b=r[0].bounds;foreach(Renderer renderer in r)b.Encapsulate(renderer.bounds);return b;}
    }
}
#endif
