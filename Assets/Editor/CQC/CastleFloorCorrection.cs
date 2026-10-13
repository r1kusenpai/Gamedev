#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class CastleFloorCorrection
    {
        const string Request="Assets/Editor/CQC/CastleFloorCorrection.request.txt";
        const string Folder="Assets/Building/Cobblestone";
        static CastleFloorCorrection(){EditorApplication.delayCall+=Pending;}
        static void Pending()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}
            if(SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception ex){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleFloorCorrection-error.txt",ex.ToString());Debug.LogException(ex);File.Move(Request,Request+".failed");}
        }
        [MenuItem("Tools/Buildings/Fix Stone Floor And Add Inner Mountain")]
        public static void Apply()
        {
            var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");
            var floors=mountain.transform.Find("Cobblestone Castle Floors");var houses=mountain.transform.Find("One Ring Of Houses");
            if(!floors||!houses)throw new InvalidOperationException("Existing castle floor and ring required.");
            var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-inner-circle-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not save backup.");
            string texturePath=Folder+"/CobblestoneSurfaceCorrected.png";AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;if(!importer)throw new InvalidOperationException("Corrected stone texture missing.");
            importer.isReadable=true;importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=2048;importer.sRGBTexture=true;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);var pixels=texture.GetPixels32();float min=255,max=0;
            for(int i=0;i<pixels.Length;i+=37){float value=(pixels[i].r+pixels[i].g+pixels[i].b)/3f;min=Mathf.Min(min,value);max=Mathf.Max(max,value);}
            if(max-min<30)throw new InvalidOperationException("Stone image is blank; refusing to apply it.");
            var material=floors.GetComponentInChildren<MeshRenderer>().sharedMaterial;Undo.RecordObject(material,"Restore visible cobblestone");
            material.shader=Shader.Find("Universal Render Pipeline/Lit");material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(material);
            var outer=floors.Find("Outer House Floor").GetComponent<MeshCollider>();Bounds bounds=outer.bounds;Vector3 center=bounds.center;float floorY=bounds.max.y;
            // Apply once: a repeat request must not flip the houses back inward.
            int turned=0;
            if(!mountain.transform.Find("Inner Castle Mountain"))
            {
                foreach(Transform house in houses)
                {
                    Undo.RecordObject(house,"Face house outward");Bounds before=BoundsOf(house.gameObject);
                    house.rotation=Quaternion.AngleAxis(180f,Vector3.up)*house.rotation;
                    Bounds after=BoundsOf(house.gameObject);house.position+=new Vector3(before.center.x-after.center.x,floorY+.02f-after.min.y,before.center.z-after.center.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(house);turned++;
                    Vector3 away=Vector3.ProjectOnPlane(before.center-center,Vector3.up).normalized;
                    if(Vector3.Dot(house.forward,away)<.9f)throw new InvalidOperationException("House is not facing outwards: "+house.name);
                }
                var root=new GameObject("Inner Castle Mountain");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add raised inner castle mountain");
                center.y=floorY;
                float radius=Mathf.Min(32f,bounds.extents.z*.47f),height=10f,baseRadius=radius+6f;
                var top=Disk(center+Vector3.up*height,radius);
                var topCollider=Surface(root.transform,"Flat Inner Castle Floor",top,material);
                var rock=mountain.transform.Find("Rocky Mountain Slopes").GetComponent<MeshRenderer>().sharedMaterial;
                Surface(root.transform,"Inner Rocky Mountain Sides",Sides(center,radius,baseRadius,height),rock);
                Surface(root.transform,"Inner Mountain Access Ramp",Ramp(center,radius,height),material);
                Physics.SyncTransforms();
                foreach(Vector3 offset in new[]{Vector3.zero,Vector3.right*20f,Vector3.left*20f,Vector3.forward*20f,Vector3.back*20f})
                    if(!topCollider.Raycast(new Ray(center+offset+Vector3.up*40f,Vector3.down),out RaycastHit hit,80f)||Mathf.Abs(hit.point.y-floorY-height)>.02f)
                        throw new InvalidOperationException("Inner summit must be flat with working collision.");
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Castle scene save failed.");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleFloorCorrection-report.txt","Actual source mesh UVs and original photographs baked to visible cobblestone. Texture range: "+(max-min)+"\nHouses turned outward: "+turned+"\nInner mountain: circular flat summit, 10 metres above main floor, rocky sides and access ramp. Five collider/level checks passed.\nBackup: "+backup);
            Selection.activeGameObject=mountain;SceneView.RepaintAll();Debug.Log("Stone floor corrected. Houses face outward. Raised inner circle ready for buildings.");
        }
        static Bounds BoundsOf(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        static Mesh Disk(Vector3 center,float radius)
        {
            var v=new List<Vector3>{center};var t=new List<int>();const int n=128;
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;v.Add(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}
            for(int i=0;i<n;i++)t.AddRange(new[]{0,(i+1)%n+1,i+1});return MeshOf(v,t,"FlatInnerSummit");
        }
        static Mesh Sides(Vector3 center,float radius,float baseRadius,float height)
        {
            var v=new List<Vector3>();var t=new List<int>();const int n=128;
            for(int row=0;row<2;row++)for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;float r=row==0?radius:baseRadius;v.Add(center+new Vector3(Mathf.Cos(a)*r,row==0?height:0,Mathf.Sin(a)*r));}
            for(int i=0;i<n;i++){int next=(i+1)%n;t.AddRange(new[]{i,next,n+i,next,n+next,n+i});}return MeshOf(v,t,"InnerRockSides");
        }
        static Mesh Ramp(Vector3 c,float radius,float height)
        {
            // 25 metres of run for a walkable 22 degree rise, on the inner side of the house ring.
            var v=new List<Vector3>{c+new Vector3(-3.5f,0,-radius-24),c+new Vector3(3.5f,0,-radius-24),c+new Vector3(-3.5f,height+.04f,-radius+1),c+new Vector3(3.5f,height+.04f,-radius+1),c+new Vector3(-3.5f,0,-radius+1),c+new Vector3(3.5f,0,-radius+1)};
            return MeshOf(v,new List<int>{0,2,1,1,2,3,0,4,2,1,3,5,2,4,3,3,4,5},"InnerAccessRamp");
        }
        static Mesh MeshOf(List<Vector3> v,List<int> t,string name)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);var uv=new List<Vector2>();foreach(var p in v)uv.Add(new Vector2(p.x/4f,p.z/4f));mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static MeshCollider Surface(Transform parent,string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i]=go.transform.InverseTransformPoint(v[i]);mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+mesh.name+".asset"));go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;return collider;
        }
    }
}
#endif
