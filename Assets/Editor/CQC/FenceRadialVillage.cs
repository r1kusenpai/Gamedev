#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad] public static class FenceRadialVillage
    {
        const string Request="Assets/Editor/CQC/FenceRadialVillage-v1.request.txt";
        const string Marker="Wooden Village House Yards";
        static double next;
        [Serializable] class Layout {public Lot[] lots;public Road[] roads;}
        [Serializable] class Lot {public string name;public Vector2 center,front;public float ground,width,depth;}
        [Serializable] class Road {public string name;public float width;public Vector2[] points;}
        struct Panel {public Vector3 a,b;public Lot lot;}
        static FenceRadialVillage(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Fence village house yards");
            try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}
            catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FenceRadialVillage-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}
        }
        [MenuItem("Tools/City/Fence Current Village House Yards")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();var city=GameObject.Find("Lower Castle City");
            if(scene.name!="MyForest"||!city||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Open MyForest outside Play mode.");
            if(city.transform.Find(Marker))return;
            var homes=city.transform.Find("House Neighborhoods");var roads=city.transform.Find("Winding Roads and Market Square");
            if(!homes||!roads||!File.Exists("Logs/RadialTown-layout.json"))throw new InvalidOperationException("Current radial town layout missing.");
            var plan=JsonUtility.FromJson<Layout>(File.ReadAllText("Logs/RadialTown-layout.json"));
            var lots=plan.lots.Where(l=>homes.Cast<Transform>().Any(t=>t.name==l.name&&t.gameObject.activeInHierarchy)).ToArray();
            if(lots.Length!=50)throw new InvalidOperationException("Expected 50 active village houses; found "+lots.Length);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Building/ImportedTaverns/OutsideWoodFence/OutsideWoodFence.prefab");
            var filter=prefab?prefab.GetComponentInChildren<MeshFilter>():null;var renderer=prefab?prefab.GetComponentInChildren<MeshRenderer>():null;
            if(!filter||!renderer)throw new InvalidOperationException("Existing wooden fence panel missing.");
            var source=filter.sharedMesh;var native=source.bounds;
            var panels=new List<Panel>();var notes=new List<string>();int skipped=0;
            foreach(var lot in lots)
            {
                var front=lot.front.normalized;var right=new Vector2(front.y,-front.x);
                float x=lot.width*.5f+1.4f,z=lot.depth*.5f+1.4f;
                var corners=new[]{lot.center-right*x-front*z,lot.center+right*x-front*z,lot.center+right*x+front*z,lot.center-right*x+front*z};
                int before=panels.Count;
                for(int side=0;side<4;side++)
                {
                    var a=corners[side];var b=corners[(side+1)%4];float length=Vector2.Distance(a,b);int count=Mathf.CeilToInt(length/1.8f);
                    for(int i=0;i<count;i++)
                    {
                        Vector2 p=Vector2.Lerp(a,b,(float)i/count),q=Vector2.Lerp(a,b,(float)(i+1)/count);
                        // The full entrance corridor stays open, including panel ends.
                        if(side==2&&Mathf.Min(Vector2.Dot(p-lot.center,right),Vector2.Dot(q-lot.center,right))<2.1f&&Mathf.Max(Vector2.Dot(p-lot.center,right),Vector2.Dot(q-lot.center,right))>-2.1f){skipped++;continue;}
                        bool clear=true;float y0=Ground(p),y1=Ground(q);
                        if(float.IsNaN(y0)||float.IsNaN(y1)||Mathf.Abs(y1-y0)>.45f)clear=false;
                        for(int sample=0;sample<=6&&clear;sample++)
                        {
                            Vector2 v=Vector2.Lerp(p,q,sample/6f);
                            if(RoadDistance(v,plan.roads)<.85f||plan.lots.Any(l=>l!=lot&&InLot(v,l,.6f)))clear=false;
                            // Keep every house's door access unobstructed, even where yards meet.
                            foreach(var other in plan.lots)
                            {
                                var f=other.front.normalized;var r=new Vector2(f.y,-f.x);var d=v-other.center;
                                if(Mathf.Abs(Vector2.Dot(d,r))<2.1f&&Vector2.Dot(d,f)>other.depth*.5f-.1f&&Vector2.Dot(d,f)<other.depth*.5f+8f){clear=false;break;}
                            }
                        }
                        if(!clear){skipped++;continue;}
                        panels.Add(new Panel{a=new Vector3(p.x,y0,p.y),b=new Vector3(q.x,y1,q.y),lot=lot});
                    }
                }
                if(panels.Count==before)throw new InvalidOperationException("No clear fence fits "+lot.name+". Existing scene retained.");
                notes.Add(lot.name+": "+(panels.Count-before)+" panels; front entrance open.");
            }
            string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),folder="Assets/CityExpansion/VillageFences-"+stamp;
            string backup="Assets/ForestExpansion/Backups/MyForest-before-village-fences-"+stamp+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var root=new GameObject(Marker);Undo.RegisterCreatedObjectUndo(root,"Add village fences");root.transform.SetParent(city.transform,false);
            var positions=homes.Cast<Transform>().ToDictionary(t=>t,t=>t.localToWorldMatrix);
            var vertex=source.vertices;var normal=source.normals;var uv=source.uv;var triangles=source.triangles;int number=0;
            foreach(var lot in lots)
            {
                var yard=new GameObject(lot.name+" Fence Yard");yard.transform.SetParent(root.transform,false);
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var indices=new List<int>();
                foreach(var panel in panels.Where(p=>p.lot==lot))
                {
                    var delta=panel.b-panel.a;var along=new Vector3(delta.x,0,delta.z).normalized;
                    var rotation=Quaternion.LookRotation(Vector3.Cross(along,Vector3.up),Vector3.up);
                    var scale=new Vector3(new Vector2(delta.x,delta.z).magnitude/native.size.x,1.25f/native.size.y,.16f/Mathf.Max(.001f,native.size.z));
                    int start=vertices.Count;
                    for(int i=0;i<vertex.Length;i++)
                    {
                        Vector3 v=vertex[i]-new Vector3(native.center.x,native.min.y,native.center.z);float t=(vertex[i].x-native.min.x)/native.size.x;
                        vertices.Add((panel.a+panel.b)*.5f+rotation*Vector3.Scale(v,scale)+Vector3.up*(Mathf.Lerp(panel.a.y,panel.b.y,t)-(panel.a.y+panel.b.y)*.5f-.025f));
                        normals.Add((rotation*Vector3.Scale(normal[i],new Vector3(1/scale.x,1/scale.y,1/scale.z))).normalized);uvs.Add(uv[i]);
                    }
                    foreach(int index in triangles)indices.Add(start+index);
                    var obstacle=new GameObject("Fence collision");obstacle.transform.SetParent(yard.transform,false);obstacle.transform.SetPositionAndRotation((panel.a+panel.b)*.5f+Vector3.up*.6f,rotation);
                    var box=obstacle.AddComponent<BoxCollider>();box.size=new Vector3(new Vector2(delta.x,delta.z).magnitude,1.25f+Mathf.Abs(delta.y),.16f);
                }
                var mesh=new Mesh{name=yard.name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
                AssetDatabase.CreateAsset(mesh,folder+"/Yard"+(++number).ToString("000")+".asset");yard.AddComponent<MeshFilter>().sharedMesh=mesh;yard.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            }
            foreach(var pair in positions)if(pair.Key.localToWorldMatrix!=pair.Value)throw new InvalidOperationException("A house moved unexpectedly.");
            CaptureYard(homes,root.transform,lots[lots.Length/2]);
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FenceRadialVillage-report.txt","Added wooden fence yards around "+lots.Length+" active outside village houses. "+panels.Count+" ground-following panels combined into 50 meshes, using the original supplied wooden fence material. Front openings at least 4.2m; road edge clearance at least 0.85m; neighboring building and entrance corridors checked. Omitted "+skipped+" conflicting/entrance panels. Houses, roads, terrain, castle and transport retained.\n"+string.Join("\n",notes)+"\nBackup: "+backup+"\nAssets: "+folder);
            Selection.activeGameObject=root;SceneView.RepaintAll();Debug.Log("Wooden fences added around all 50 village house yards with open entrances. Scene saved.");
        }
        static bool InLot(Vector2 p,Lot lot,float pad){var f=lot.front.normalized;var r=new Vector2(f.y,-f.x);var d=p-lot.center;return Mathf.Abs(Vector2.Dot(d,r))<lot.width*.5f+pad&&Mathf.Abs(Vector2.Dot(d,f))<lot.depth*.5f+pad;}
        static void CaptureYard(Transform homes,Transform yards,Lot lot)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var house=UnityEngine.Object.Instantiate(homes.Cast<Transform>().First(t=>t.name==lot.name).gameObject);preview.AddSingleGO(house);
                var fence=UnityEngine.Object.Instantiate(yards.Find(lot.name+" Fence Yard").gameObject);preview.AddSingleGO(fence);
                foreach(var behaviour in house.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                Vector3 center=new Vector3(lot.center.x,lot.ground,lot.center.y);float span=Mathf.Max(lot.width,lot.depth)+9;
                Vector3 front=new Vector3(lot.front.x,0,lot.front.y);
                preview.camera.transform.position=center+(front+Vector3.up*1.8f)*span;
                preview.camera.transform.LookAt(center+Vector3.up*3);preview.camera.fieldOfView=42;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=200;
                preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(45,lot.front.x*180,0);preview.lights[1].intensity=1;
                Directory.CreateDirectory("Logs");FitSwordShoulder.Capture(preview,"Logs/VillageFence-preview.png",house);
            }
            finally{preview.Cleanup();}
        }
        static float RoadDistance(Vector2 p,Road[] roads){float best=float.PositiveInfinity;foreach(var road in roads)for(int i=1;i<road.points.Length;i++){var a=road.points[i-1];var d=road.points[i]-a;best=Mathf.Min(best,(p-a-d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude))).magnitude-road.width*.5f);}return best;}
        static float Ground(Vector2 p){foreach(var t in Terrain.activeTerrains){var a=t.transform.position;var s=t.terrainData.size;if(p.x>=a.x&&p.x<=a.x+s.x&&p.y>=a.z&&p.y<=a.z+s.z)return a.y+t.terrainData.GetInterpolatedHeight((p.x-a.x)/s.x,(p.y-a.z)/s.z);}return float.NaN;}
    }
}
#endif
