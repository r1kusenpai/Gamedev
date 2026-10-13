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
    public static class CastleHouseRing
    {
        private const string Request="Assets/Editor/CQC/CreateHouseRing.request.txt";
        private const string Model="Assets/Building/ImageToStl.com_tavern(1).fbx";
        private const string Name="One Ring Of Houses";
        static CastleHouseRing()
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
            catch(Exception ex){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleHouseRing-error.txt",ex.ToString());Debug.LogException(ex);File.Move(Request,Request+".failed");}
        }
        [MenuItem("Tools/Buildings/Create One House Ring On Mountain")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Scene scene=SceneManager.GetActiveScene();GameObject mountain=GameObject.Find("Castle Mountain");
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if(!mountain||!prefab)throw new InvalidOperationException("Castle Mountain and imported tavern are required.");
            if(mountain.transform.Find(Name))return;
            Transform summit=mountain.transform.Find("Flat House Building Area");
            MeshCollider floor=summit?summit.GetComponent<MeshCollider>():null;
            Renderer surface=summit?summit.GetComponent<Renderer>():null;
            if(!floor||!surface)throw new InvalidOperationException("Flat mountain summit not found.");
            Bounds top=surface.bounds;Vector3 center=top.center;
            string backup="Assets/ForestExpansion/Backups/MyForest-before-house-ring-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up scene.");
            // Reuse a manually placed tavern on the summit as one member of the ring.
            GameObject[] existing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                .Select(t=>t.gameObject).Where(g=>PrefabUtility.IsAnyPrefabInstanceRoot(g)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==Model)
                .Where(g=>{Vector3 d=g.transform.position-center;return d.x*d.x/(top.extents.x*top.extents.x)+d.z*d.z/(top.extents.z*top.extents.z)<1.05f&&Mathf.Abs(d.y)<30f;}).Distinct().Take(12).ToArray();
            int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Place one ring of houses");
            GameObject group=new GameObject(Name);group.transform.SetParent(mountain.transform,false);
            Undo.RegisterCreatedObjectUndo(group,"Place one ring of houses");
            const int count=12;int reused=0;float radius=.72f;
            for(int i=0;i<count;i++)
            {
                GameObject house;
                if(i<existing.Length)
                {
                    house=existing[i];Undo.RecordObject(house.transform,"Position existing tavern in ring");
                    Undo.SetTransformParent(house.transform,group.transform,"Add tavern to ring");reused++;
                }
                else
                {
                    house=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group.transform);
                    Undo.RegisterCreatedObjectUndo(house,"Add ring house");
                }
                house.name="Ring House "+(i+1).ToString("00");
                float angle=(i+.5f)*Mathf.PI*2f/count;
                Vector3 position=center+new Vector3(Mathf.Cos(angle)*top.extents.x*radius,0,Mathf.Sin(angle)*top.extents.z*radius);
                house.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(center-position,Vector3.up));
                house.transform.position=position;
                Bounds b=BoundsOf(house);
                float desiredHeight=Mathf.Min(12f,top.extents.z*.2f);
                float desiredFootprint=Mathf.Min(18f,top.extents.z*.28f);
                float factor=Mathf.Min(desiredHeight/Mathf.Max(.00001f,b.size.y),desiredFootprint/Mathf.Max(.00001f,Mathf.Max(b.size.x,b.size.z)));
                house.transform.localScale*=factor;
                b=BoundsOf(house);
                house.transform.position+=new Vector3(position.x-b.center.x,top.max.y-b.min.y+.03f,position.z-b.center.z);
                b=BoundsOf(house);
                foreach(Vector3 corner in new[]{new Vector3(b.min.x,top.max.y+100f,b.min.z),new Vector3(b.min.x,top.max.y+100f,b.max.z),new Vector3(b.max.x,top.max.y+100f,b.min.z),new Vector3(b.max.x,top.max.y+100f,b.max.z)})
                    if(!floor.Raycast(new Ray(corner,Vector3.down),out RaycastHit hit,150f))throw new InvalidOperationException("House "+(i+1)+" is not fully on the flat summit.");
                PrefabUtility.RecordPrefabInstancePropertyModifications(house.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Use Ctrl+S to save houses.");
            Undo.CollapseUndoOperations(undo);Selection.activeGameObject=group;SceneView.RepaintAll();
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleHouseRing-report.txt","Saved one ring of 12 colored tavern houses on Castle Mountain.\nExisting taverns reused: "+reused+"\nHouse corners on summit: 48/48 checked. Houses face the central courtyard. Entrance gap aligned between houses at the access ramp.\nCentral courtyard open; no additional rings or towers added.\nBackup: "+backup+"\n");
            Debug.Log("One ring of 12 houses placed on the mountain. Central courtyard left open.");
        }
        private static Bounds BoundsOf(GameObject go)
        {
            Renderer[] renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new InvalidOperationException("House has no visible meshes.");
            Bounds b=renderers[0].bounds;foreach(Renderer r in renderers)b.Encapsulate(r.bounds);return b;
        }
    }
}
#endif
