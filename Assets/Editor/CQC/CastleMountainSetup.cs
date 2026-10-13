#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class CastleMountainSetup
    {
        private const string Request="Assets/Editor/CQC/CreateCastleMountain.request.txt";
        private const string RootName="Castle Mountain";
        private const string Folder="Assets/ForestExpansion/CastleMountain";
        private const float RX=130f,RZ=100f,TX=90f,TZ=68f,H=40f;
        static CastleMountainSetup()
        {
            EditorApplication.delayCall+=Pending;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Pending;};
        }
        private static void Pending()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isUpdating||EditorApplication.isCompiling){EditorApplication.delayCall+=Pending;return;}
            if(SceneManager.GetActiveScene().name!="MyForest")return;
            try{Build();AssetDatabase.DeleteAsset(Request);}
            catch(Exception ex){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleMountain-error.txt",ex.ToString());Debug.LogException(ex);File.Move(Request,Request+".failed");}
        }
        [MenuItem("Tools/Forest/Create Flat Top Castle Mountain")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Scene scene=SceneManager.GetActiveScene();if(scene.name!="MyForest")throw new InvalidOperationException("Open MyForest first.");
            if(scene.GetRootGameObjects().Any(g=>g.name==RootName))return;
            Terrain[] terrains=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true)).Where(t=>t.terrainData).ToArray();
            if(terrains.Length==0)throw new InvalidOperationException("No forest ground found.");
            Vector3 center=ChooseCenter(terrains);
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/ForestExpansion","CastleMountain");
            string backup="Assets/ForestExpansion/Backups/MyForest-before-castle-mountain-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not save backup.");
            int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Create castle mountain");
            var root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=center;
            Undo.RegisterCreatedObjectUndo(root,"Create castle mountain");
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("URP Lit shader not ready.");
            Material top=new Material(shader){name="CastleMountain_Grass"};top.SetColor("_BaseColor",Color.white);top.SetFloat("_Smoothness",0f);
            string grassPath=AssetDatabase.FindAssets("UNS_Terrain_Grass t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if(grassPath!=null)top.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(grassPath));
            Material sides=new Material(shader){name="CastleMountain_Rock"};sides.SetColor("_BaseColor",new Color(.32f,.36f,.29f));sides.SetFloat("_Smoothness",0f);
            Material path=new Material(shader){name="CastleMountain_Path"};path.SetColor("_BaseColor",new Color(.68f,.61f,.44f));path.SetFloat("_Smoothness",0f);
            foreach(Material material in new[]{top,sides,path})AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+material.name+".mat"));
            Mesh summit=Top();Mesh rock=Sides(center,terrains);Mesh access=Ramp(center,terrains);
            MeshCollider summitCollider=Surface(root.transform,"Flat House Building Area",summit,top);
            Surface(root.transform,"Rocky Mountain Slopes",rock,sides);
            MeshCollider rampCollider=Surface(root.transform,"Mountain Access Ramp",access,path);
            Surface(root.transform,"Ramp Rock Embankment",RampSupport(center,terrains),sides);
            int hidden=ClearBuriedTrees(scene,center);
            Physics.SyncTransforms();
            foreach(Vector3 point in new[]{Vector3.zero,new Vector3(40,0,0),new Vector3(-40,0,0),new Vector3(0,0,30),new Vector3(0,0,-30)})
            {
                Ray ray=new Ray(center+point+Vector3.up*(H+100f),Vector3.down);
                if(!summitCollider.Raycast(ray,out RaycastHit hit,150f)||Mathf.Abs(hit.point.y-(center.y+H))>.02f)
                    throw new InvalidOperationException("Flat mountain top collision check failed.");
            }
            if(!rampCollider.Raycast(new Ray(center+new Vector3(0,H+100f,-140),Vector3.down),out RaycastHit rampHit,200f))
                throw new InvalidOperationException("Access ramp collision check failed.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Use Ctrl+S to save mountain.");
            Undo.CollapseUndoOperations(undo);Selection.activeGameObject=root;SceneView.RepaintAll();
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleMountain-report.txt","Castle Mountain saved as a separate editable mesh object.\nPosition: "+center+"\nHeight: 40m. Flat oval summit: 180m x 136m. Base: 260m x 200m.\nIncludes rocky slopes and a 16m-wide gentle access ramp.\nFlat summit collision checks: 5/5. Ramp collision check passed.\nTrees hidden under mountain footprint: "+hidden+"\nUnderlying terrain data, houses/tower placements and combat untouched.\nBackup: "+backup+"\n");
            Debug.Log("Castle mountain ready. Place houses on Flat House Building Area under Castle Mountain.");
        }
        private static Vector3 ChooseCenter(Terrain[] terrains)
        {
            // Approximate the outlined area in the supplied scene-view reference.
            SceneView view=SceneView.lastActiveSceneView;
            Vector3 p=new Vector3(-120f,0f,40f);
            if(view&&view.camera)
            {
                Ray ray=view.camera.ViewportPointToRay(new Vector3(.36f,.50f,0));float closest=float.PositiveInfinity;
                foreach(Terrain terrain in terrains)
                {
                    TerrainCollider collider=terrain.GetComponent<TerrainCollider>();
                    if(collider&&collider.Raycast(ray,out RaycastHit hit,4000f)&&hit.distance<closest){p=hit.point;closest=hit.distance;}
                }
            }
            p.y=Ground(terrains,p.x,p.z);return p;
        }
        private static float Ground(Terrain[] terrains,float x,float z)
        {
            foreach(Terrain terrain in terrains)
            {
                Vector3 origin=terrain.transform.position,size=terrain.terrainData.size;
                if(x>=origin.x&&x<=origin.x+size.x&&z>=origin.z&&z<=origin.z+size.z)
                    return origin.y+terrain.terrainData.GetInterpolatedHeight((x-origin.x)/size.x,(z-origin.z)/size.z);
            }
            return 10f;
        }
        private static Mesh Top()
        {
            const int n=80;var vertices=new List<Vector3>{new Vector3(0,H,0)};var uv=new List<Vector2>{Vector2.zero};var indices=new List<int>();
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;Vector3 p=new Vector3(Mathf.Cos(a)*TX,H,Mathf.Sin(a)*TZ);vertices.Add(p);uv.Add(new Vector2(p.x/2f,p.z/2f));}
            for(int i=0;i<n;i++){indices.Add(0);indices.Add((i+1)%n+1);indices.Add(i+1);}
            return Mesh("CastleMountain_Summit",vertices,uv,indices);
        }
        private static Mesh Sides(Vector3 center,Terrain[] terrains)
        {
            const int n=80,rings=7;var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int row=0;row<rings;row++)for(int i=0;i<n;i++)
            {
                float t=row/(float)(rings-1),a=i*Mathf.PI*2/n;
                float noise=row==0?0f:Mathf.Sin(i*2.17f)*1.7f*t;
                float x=Mathf.Cos(a)*(Mathf.Lerp(TX,RX,t)+noise),z=Mathf.Sin(a)*(Mathf.Lerp(TZ,RZ,t)+noise);
                float floor=Ground(terrains,center.x+x,center.z+z)-center.y-.35f;
                float y=Mathf.Lerp(H,floor,Mathf.Pow(t,.85f));
                vertices.Add(new Vector3(x,y,z));uv.Add(new Vector2(i/(float)n,t));
            }
            for(int row=0;row<rings-1;row++)for(int i=0;i<n;i++)
            {
                int a=row*n+i,b=row*n+(i+1)%n,c=(row+1)*n+i,d=(row+1)*n+(i+1)%n;
                triangles.AddRange(new[]{a,b,c,b,d,c});
            }
            return Mesh("CastleMountain_Slopes",vertices,uv,triangles);
        }
        private static Mesh Ramp(Vector3 center,Terrain[] terrains)
        {
            const int rows=30;float start=-220f,end=-TZ+3f;var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            float startHeight=Ground(terrains,center.x,center.z+start)-center.y+.08f;
            for(int row=0;row<=rows;row++)
            {
                float t=row/(float)rows,z=Mathf.Lerp(start,end,t),y=Mathf.Lerp(startHeight,H,t)+.03f;
                v.Add(new Vector3(-8f,y,z));v.Add(new Vector3(8f,y,z));uv.Add(new Vector2(0,t*15));uv.Add(new Vector2(1,t*15));
                if(row<rows){int a=row*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
            }
            return Mesh("CastleMountain_Access",v,uv,triangles);
        }
        private static Mesh RampSupport(Vector3 center,Terrain[] terrains)
        {
            const int rows=30;var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            float startHeight=Ground(terrains,center.x,center.z-220f)-center.y+.08f;
            for(int row=0;row<=rows;row++)
            {
                float t=row/(float)rows,z=Mathf.Lerp(-220f,-TZ+3f,t),y=Mathf.Lerp(startHeight,H,t);
                v.Add(new Vector3(-8f,y,z));v.Add(new Vector3(8f,y,z));
                v.Add(new Vector3(-8f,Ground(terrains,center.x-8f,center.z+z)-center.y-.2f,z));
                v.Add(new Vector3(8f,Ground(terrains,center.x+8f,center.z+z)-center.y-.2f,z));
                for(int j=0;j<4;j++)uv.Add(new Vector2(j*.33f,t));
                if(row<rows){int a=row*4;triangles.AddRange(new[]{a,a+2,a+4,a+4,a+2,a+6,a+1,a+5,a+3,a+5,a+7,a+3});}
            }
            triangles.AddRange(new[]{0,1,2,1,3,2});int end=rows*4;triangles.AddRange(new[]{end,end+2,end+1,end+1,end+2,end+3});
            return Mesh("CastleMountain_RampSupport",v,uv,triangles);
        }
        private static Mesh Mesh(string name,List<Vector3> vertices,List<Vector2> uv,List<int> triangles)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+name+".asset"));return mesh;
        }
        private static MeshCollider Surface(Transform parent,string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=material;MeshCollider collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;return collider;
        }
        private static int ClearBuriedTrees(Scene scene,Vector3 center)
        {
            int count=0;
            foreach(Transform container in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="UNS_Trees"||t.name=="Original Spruce Trees"))
                foreach(Transform tree in container)
                {
                    Vector3 p=tree.position-center;float r=p.x*p.x/(RX*RX)+p.z*p.z/(RZ*RZ);
                    if(r<.94f&&tree.gameObject.activeSelf){Undo.RecordObject(tree.gameObject,"Clear mountain building footprint");tree.gameObject.SetActive(false);count++;}
                }
            return count;
        }
    }
}
#endif
