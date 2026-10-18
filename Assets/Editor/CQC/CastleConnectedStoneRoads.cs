#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class CastleConnectedStoneRoads
 {
  const string Request="Assets/Editor/CQC/CastleConnectedStoneRoads.request.txt";static double next;
  static CastleConnectedStoneRoads(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleConnectedStoneRoads-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/City/Add Castle Circle Roads And Connections Without Moving Models")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");var city=GameObject.Find("Lower Castle City");if(!mountain||!city)throw new InvalidOperationException("Castle and outside town required.");if(mountain.transform.Find("Connected Castle Stone Roads"))return;
   var outside=city.transform.Find("Winding Roads and Market Square");var source=outside.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.sharedMaterial);if(!source)throw new InvalidOperationException("Outside road material missing.");var material=source.sharedMaterial;
   var lower=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshCollider>();var upper=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();var elevator=mountain.transform.Find("Castle Scaffold Elevator");if(!lower||!upper||!elevator)throw new InvalidOperationException("Castle floors and elevator required.");var bridge=elevator.Find("Upper Landing Bridge").GetComponent<Collider>();
   var original=mountain.GetComponentsInChildren<Transform>(true);var matrices=original.Select(t=>t.localToWorldMatrix).ToArray();var lowerMesh=lower.sharedMesh;var upperMesh=upper.sharedMesh;var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-connected-castle-roads-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");string folder="Assets/CityExpansion/CastleRoads-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();Physics.SyncTransforms();
   var root=new GameObject("Connected Castle Stone Roads");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add castle circular roads and links");var lo=new RoadMesh(root.transform,lower,null);var hi=new RoadMesh(root.transform,upper,bridge);Bounds lb=lower.bounds,ub=upper.bounds;float lrx=lb.extents.x*.91f,lrz=lb.extents.z*.91f,urx=ub.extents.x*.80f,urz=ub.extents.z*.80f;lo.Ribbon(Loop(lb.center,lrx,lrz),2.8f);hi.Ribbon(Loop(ub.center,urx,urz),3.2f);int houses=0;
   foreach(string name in new[]{"One Ring Of Houses","Added Marked Taverns"}){var group=mountain.transform.Find(name);if(!group)continue;foreach(Transform house in group){if(!house.gameObject.activeInHierarchy||house.GetComponentsInChildren<Renderer>().Length==0)continue;Bounds b=BoundsOf(house.gameObject);Vector3 join=OnLoop(b.center,lb.center,lrx,lrz);Vector3 endpoint=b.ClosestPoint(new Vector3(join.x,b.center.y,join.z));lo.Link(join,endpoint,2.2f);houses++;}}
   Vector3 lowerLift=elevator.position;lo.Link(OnLoop(lowerLift,lb.center,lrx,lrz),lowerLift-elevator.forward*1.5f,3f);Vector3 upperLift=bridge.bounds.center-elevator.forward*.9f;hi.Link(OnLoop(upperLift,ub.center,urx,urz),upperLift,3f);
   int important=0;foreach(string name in new[]{"Large Upper Castle Church","Central Upper Floor Building","Large Courtyard Rook Tower","Upper Blacksmith And Boxwood Borders/Upper Courtyard Blacksmith","Old Fountain In Front Of Chapel"})
   {
    var target=mountain.transform.Find(name);if(!target||!target.gameObject.activeInHierarchy)continue;Bounds b=BoundsOf(target.gameObject);Vector3 join=OnLoop(b.center,ub.center,urx,urz),end=b.ClosestPoint(new Vector3(join.x,b.center.y,join.z));
    if(name=="Large Upper Castle Church"){Vector3 front=Vector3.ProjectOnPlane(-target.forward,Vector3.up).normalized;float depth=Mathf.Abs(front.x)*b.extents.x+Mathf.Abs(front.z)*b.extents.z;end=b.center+front*(depth-.4f);join=OnLoop(end,ub.center,urx,urz);}
    else if(name=="Central Upper Floor Building"){var doors=target.GetComponentsInChildren<Renderer>().Where(r=>r.gameObject.activeInHierarchy&&r.name.StartsWith("front door",StringComparison.OrdinalIgnoreCase)).ToArray();if(doors.Length>0){end=doors.OrderBy(r=>r.bounds.min.y).First().bounds.center;join=OnLoop(end,ub.center,urx,urz);}}
    hi.Link(join,end,name.Contains("Church")||name.Contains("Central")?3f:2.2f);important++;
   }
   int lowerTriangles=lo.Save("Lower Castle House Circle And Links",material,folder+"/LowerRoadNetwork.asset"),upperTriangles=hi.Save("Upper Courtyard Circle And Links",material,folder+"/UpperRoadNetwork.asset");if(lowerTriangles==0||upperTriangles==0)throw new InvalidOperationException("Road meshes have no supported surface.");
   for(int i=0;i<original.Length;i++)if(original[i].localToWorldMatrix!=matrices[i])throw new InvalidOperationException("An existing model moved; roads rolled back.");if(lower.sharedMesh!=lowerMesh||upper.sharedMesh!=upperMesh)throw new InvalidOperationException("Existing floor mesh changed.");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleConnectedStoneRoads-report.txt","Added two circular stone-road loops: castle house ring and upper courtyard. Connected "+houses+" house fronts, both elevator landings and "+important+" chapel/castle/rook/blacksmith/fountain destinations. Reused exact outside road material and world UV scale (3m repeat). Road branches overlap their circles with identical texture coordinates for continuous joins. Surface overlays sit 6cm above existing floors/upper bridge; unsupported areas omitted. Existing walkable floor/bridge colliders retained; moving elevator deck untouched. Verified every existing castle model transform and both floor meshes unchanged.\nBackup: "+backup);Debug.Log("Castle circular roads and house/chapel/castle/elevator connections added. No existing models moved.");
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var item in r)b.Encapsulate(item.bounds);return b;}
  static List<Vector3> Loop(Vector3 center,float rx,float rz){var points=new List<Vector3>();for(int i=0;i<=192;i++){float a=i*Mathf.PI*2/192;points.Add(center+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz));}return points;}
  static Vector3 OnLoop(Vector3 p,Vector3 center,float rx,float rz){float a=Mathf.Atan2((p.z-center.z)/rz,(p.x-center.x)/rx);return center+new Vector3(rx*Mathf.Cos(a),0,rz*Mathf.Sin(a));}
  class RoadMesh
  {
   Transform parent;MeshCollider floor;Collider bridge;List<Vector3> vertices=new List<Vector3>();List<Vector2> uv=new List<Vector2>();List<int> triangles=new List<int>();public RoadMesh(Transform p,MeshCollider f,Collider b){parent=p;floor=f;bridge=b;}
   bool Surface(Vector3 p,out Vector3 result){float y=floor.bounds.max.y;var ray=new Ray(new Vector3(p.x,y+4,p.z),Vector3.down);if(floor.Raycast(ray,out RaycastHit hit,10)||bridge&&bridge.Raycast(ray,out hit,10)){result=new Vector3(p.x,hit.point.y+.06f,p.z);return true;}result=p;return false;}
   public void Link(Vector3 a,Vector3 b,float width){a.y=0;b.y=0;int count=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(a,b)/.8f));var points=new List<Vector3>();for(int i=0;i<=count;i++)points.Add(Vector3.Lerp(a,b,(float)i/count));Ribbon(points,width);}
   public void Ribbon(List<Vector3> points,float width){int offset=vertices.Count;var supported=new List<bool>();for(int i=0;i<points.Count;i++){Vector3 tangent=Vector3.ProjectOnPlane(points[Mathf.Min(points.Count-1,i+1)]-points[Mathf.Max(0,i-1)],Vector3.up).normalized;if(i==0&&Vector3.Distance(points[0],points[points.Count-1])<.1f)tangent=Vector3.ProjectOnPlane(points[1]-points[points.Count-2],Vector3.up).normalized;if(i==points.Count-1&&Vector3.Distance(points[0],points[i])<.1f)tangent=Vector3.ProjectOnPlane(points[1]-points[i-1],Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,tangent);bool left=Surface(points[i]-side*width*.5f,out Vector3 a),right=Surface(points[i]+side*width*.5f,out Vector3 b);supported.Add(left&&right);foreach(var p in new[]{a,b}){vertices.Add(parent.InverseTransformPoint(p));uv.Add(new Vector2(p.x/3,p.z/3));}}
    for(int i=0;i<points.Count-1;i++){if(!supported[i]||!supported[i+1])continue;int a=offset+i*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}}
   public int Save(string name,Material material,string path){var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return triangles.Count/3;}
  }
 }
}
#endif
