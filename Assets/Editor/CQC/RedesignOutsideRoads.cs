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
 [InitializeOnLoad]public static class RedesignOutsideRoads
 {
  const string Request="Assets/Editor/CQC/RedesignOutsideRoads-seven-local-v7.request.txt";static double next;
  static RedesignOutsideRoads(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RedesignOutsideRoads-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  sealed class Geometry
  {
   public readonly List<Vector3> Vertices=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<int> triangles=new List<int>();
   public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector2 u,Vector2 v,Vector2 w){int n=Vertices.Count;Vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{u,v,w});triangles.AddRange(new[]{n,n+1,n+2});}
   public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float run){Triangle(a,b,c,new Vector2(run,0),new Vector2(run,1),new Vector2(run+Vector3.Distance(a,c),0));Triangle(b,d,c,new Vector2(run,1),new Vector2(run+Vector3.Distance(b,d),1),new Vector2(run+Vector3.Distance(a,c),0));}
   public Mesh Mesh(string name){var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(Vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}
  }
  [MenuItem("Tools/City/Redesign Outside Road Junctions And Edges")]
  public static void Apply()
  {
   var city=GameObject.Find("Lower Castle City");if(!city)throw new InvalidOperationException("Outside town missing.");var roads=city.transform.Find("Winding Roads and Market Square");if(!roads)throw new InvalidOperationException("Outside roads missing.");if(roads.Find("Seven Local Blue Mark Cuts"))return;
   const string baseline="Assets/CityExpansion/RoadRedesign-20261014-221921/";
   const string original="Assets/CityExpansion/ContinuousRoads-20261013-164404/";
   var guides=roads.GetComponentsInChildren<MeshFilter>(true).Where(IsOriginalGuide).ToArray();if(guides.Length!=13)throw new InvalidOperationException("Expected all 13 saved road guides.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-seven-local-road-cuts-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");string folder="Assets/CityExpansion/RoadLocalCuts-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   // Restore the exact pre-v6 guides, without changing any building or transport transform.
   foreach(var guide in guides){int index=guide.name.StartsWith("Neighborhood Loop")?int.Parse(guide.name.Substring("Neighborhood Loop ".Length))-1:guide.name.StartsWith("Market Street")?int.Parse(guide.name.Substring("Market Street ".Length))+3:12;string modified=baseline+"Guide-"+guide.name.Replace(" ","_")+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(modified);if(!mesh)mesh=AssetDatabase.LoadAssetAtPath<Mesh>(original+"Road-"+index+".asset");if(!mesh)throw new InvalidOperationException("Saved guide unavailable: "+guide.name);Undo.RecordObject(guide,"Restore unmarked road connections");guide.sharedMesh=mesh;Undo.RecordObject(guide.gameObject,"Restore pre-removal road availability");guide.gameObject.SetActive(guide.name!="Market Street 3"&&guide.name!="Market Street 4");}
   var masks=new List<LocalCut>();var inner=guides.First(g=>g.name=="Neighborhood Loop 1");var collector=guides.First(g=>g.name=="Neighborhood Loop 2");
   var innerPoints=Centers(inner);var collectorPoints=Centers(collector);
   // Arc distances come from surveyed intersections on the original saved guides.
   // Stop before each unmarked radial junction; remove only the marked arc between them.
   AddLocalCut(masks,innerPoints,9.4f,23.4f,2.8f,"plaza northeast curve");
   AddLocalCut(masks,innerPoints,74.5f,85.3f,2.8f,"plaza northwest curve");
   AddLocalCut(masks,innerPoints,93.3f,105.9f,2.8f,"plaza southwest curve");
   AddLocalCut(masks,innerPoints,142.4f,156.4f,2.8f,"plaza southeast curve");
   AddLocalCut(masks,innerPoints,164.4f,177.2f,2.8f,"plaza east curve");
   AddLocalCut(masks,collectorPoints,109.0f,129.0f,3.1f,"station north short curve");
   AddLocalCut(masks,collectorPoints,146.2f,155.3251f,3.1f,"station west short tip");
   if(masks.Count!=7)throw new InvalidOperationException("Seven individual cuts required.");
   RemoveLocalGuidePieces(inner,innerPoints,masks,folder);RemoveLocalGuidePieces(collector,collectorPoints,masks,folder);
   foreach(string oldName in new[]{"Redesigned Joined Roads","Simplified Five Access Road Layout","Blue Marked Road Cuts And Red Repairs","Removed Blue Plaza Road Pieces"}){var old=roads.Find(oldName);if(old){Undo.RecordObject(old.gameObject,"Restore unmarked layout and replace only local pieces");old.gameObject.SetActive(false);}}
   var root=new GameObject("Seven Local Blue Mark Cuts");Undo.RegisterCreatedObjectUndo(root,"Remove seven marked road pieces");root.transform.SetParent(roads,false);
   var counts=new int[7];ClipSavedRoad(baseline+"Joined_Crowned_Paving.asset",baseline+"RoadStone.mat","Joined Crowned Paving",root.transform,folder,masks,0,true,counts);
   ClipSavedRoad(baseline+"Continuous_Stone_Curb_Edges.asset",baseline+"CurbStone.mat","Continuous Stone Curb Edges",root.transform,folder,masks,.15f,false,null);
   ClipSavedRoad(baseline+"Sloped_Dirt_Gravel_Shoulders.asset",baseline+"Shoulders.mat","Sloped Dirt Gravel Shoulders",root.transform,folder,masks,.85f,false,null);
   for(int i=0;i<counts.Length;i++)if(counts[i]<5)throw new InvalidOperationException("Marked cut did not reach saved road paving: "+masks[i].Name);
   foreach(var guide in guides){var renderer=guide.GetComponent<Renderer>();if(renderer){Undo.RecordObject(renderer,"Use restored local-cut paving");renderer.enabled=false;}var collider=guide.GetComponent<Collider>();if(collider){Undo.RecordObject(collider,"Use restored local-cut collision");collider.enabled=false;}}
   var square=roads.Find("Central Market Square");var squareRenderer=square.GetComponent<Renderer>();Undo.RecordObject(squareRenderer,"Use exact saved plaza paving");squareRenderer.enabled=false;var squareCollider=square.GetComponent<Collider>();if(squareCollider){Undo.RecordObject(squareCollider,"Use exact saved plaza collision");squareCollider.enabled=false;}
   AssetDatabase.SaveAssets();RedistributeCastleCrowd.Apply();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RedesignOutsideRoads-report.txt","Restored the exact pre-v6 paving, plaza footprint and unmarked road connections from "+baseline+". Removed only seven local blue-X arc pieces. No global ring removal, radial truncation, plaza resizing, road widening or building changes. Curbs, shoulders and NPC guides cut only at those same locations; navigation regenerated with role counts retained.\n"+string.Join("\n",masks.Select((cut,i)=>cut.Name+": "+counts[i]+" affected paving triangles"))+"\nBackup: "+backup);Debug.Log("Restored unmarked roads and original plaza; seven local blue-X road pieces removed. Scene saved.");SceneView.RepaintAll();
  }
  sealed class LocalCut
  {
   public string Name;public float Half;public List<Vector2> Points=new List<Vector2>();
   public float Distance(Vector2 point,float expand=0){float best=float.MaxValue;for(int i=1;i<Points.Count;i++){var a=Points[i-1];var delta=Points[i]-a;float t=delta.sqrMagnitude<.00001f?0:Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude);best=Mathf.Min(best,Vector2.Distance(point,a+delta*t));}return best-Half-expand;}
  }
  static void AddLocalCut(List<LocalCut> cuts,List<Vector3> path,float start,float end,float half,string name)
  {
   var cut=new LocalCut{Name=name,Half=half};float total=0;for(int i=1;i<path.Count;i++)total+=Vector3.ProjectOnPlane(path[i]-path[i-1],Vector3.up).magnitude;
   // Loop 1 is closed: the east marked piece crosses its stored seam.
   float run=0;for(int pass=0;pass<(end>total?2:1);pass++){for(int i=1;i<path.Count;i++){var a=path[i-1];var b=path[i];float length=Vector3.ProjectOnPlane(b-a,Vector3.up).magnitude;float next=run+length;if(next>=start&&run<=end&&length<=5){float lo=Mathf.Clamp01((start-run)/length),hi=Mathf.Clamp01((end-run)/length);var first=Vector3.Lerp(a,b,lo);var last=Vector3.Lerp(a,b,hi);var p=new Vector2(first.x,first.z);if(cut.Points.Count==0||Vector2.Distance(cut.Points.Last(),p)>.001f)cut.Points.Add(p);cut.Points.Add(new Vector2(last.x,last.z));}run=next;}}
   if(cut.Points.Count<2)throw new InvalidOperationException("Cannot map local cut: "+name);cuts.Add(cut);
  }
  static void RemoveLocalGuidePieces(MeshFilter guide,List<Vector3> points,List<LocalCut> cuts,string folder)
  {
   // Keep separate boundary samples on each side of a cut. Never bridge a deleted arc.
   var retained=new List<Vector3>();for(int i=1;i<points.Count;i++){var a=points[i-1];var b=points[i];float length=Vector3.Distance(a,b);int steps=length>5?1:Mathf.Max(1,Mathf.CeilToInt(length/.35f));for(int j=(i==1?0:1);j<=steps;j++){var p=Vector3.Lerp(a,b,(float)j/steps);if(cuts.All(c=>c.Distance(new Vector2(p.x,p.z))>.02f))retained.Add(p);}}
   if(retained.Count<2)throw new InvalidOperationException("Local cuts removed entire guide unexpectedly.");ReplaceGuide(guide,retained,WidthOf(guide),folder);
  }
  struct SavedVertex{public Vector3 P;public Vector2 UV;public float D;}
  static float CutDistance(Vector3 p,List<LocalCut> cuts,float expand){float best=float.MaxValue;foreach(var cut in cuts)best=Mathf.Min(best,cut.Distance(new Vector2(p.x,p.z),expand));return best;}
  static void ClipSavedRoad(string meshPath,string materialPath,string name,Transform parent,string folder,List<LocalCut> cuts,float expand,bool collision,int[] counts)
  {
   var source=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!source||!material)throw new InvalidOperationException("Saved pre-removal paving unavailable: "+meshPath);var positions=source.vertices;var uv=source.uv;var triangles=source.triangles;var geometry=new Geometry();
   for(int i=0;i<triangles.Length;i+=3){var polygon=new List<SavedVertex>();for(int j=0;j<3;j++){int index=triangles[i+j];polygon.Add(new SavedVertex{P=positions[index],UV=uv.Length>index?uv[index]:Vector2.zero,D=CutDistance(positions[index],cuts,expand)});}if(counts!=null){for(int c=0;c<cuts.Count;c++)if(polygon.Any(v=>cuts[c].Distance(new Vector2(v.P.x,v.P.z),expand)<0))counts[c]++;}
    var clipped=new List<SavedVertex>();for(int j=0;j<polygon.Count;j++){var a=polygon[j];var b=polygon[(j+1)%polygon.Count];if(a.D>=0)clipped.Add(a);if((a.D<0)!=(b.D<0)){float t=a.D/(a.D-b.D);clipped.Add(new SavedVertex{P=Vector3.Lerp(a.P,b.P,t),UV=Vector2.Lerp(a.UV,b.UV,t),D=0});}}for(int j=1;j+1<clipped.Count;j++)geometry.Triangle(clipped[0].P,clipped[j].P,clipped[j+1].P,clipped[0].UV,clipped[j].UV,clipped[j+1].UV);
   }
   if(geometry.Vertices.Count<1000)throw new InvalidOperationException("Saved geometry clipped too broadly: "+name);Save(geometry,name,material,parent,folder,collision);
  }
  static bool IsOriginalGuide(MeshFilter f)=>f.name.StartsWith("Neighborhood Loop")||f.name.StartsWith("Market Street")||f.name=="Funicular Approach Road";
  static float WidthOf(MeshFilter guide){var v=guide.sharedMesh.vertices;return Vector3.Distance(guide.transform.TransformPoint(v[0]),guide.transform.TransformPoint(v[1]));}
  static List<Vector3> Centers(MeshFilter guide){var v=guide.sharedMesh.vertices;var result=new List<Vector3>();for(int i=0;i+1<v.Length;i+=2)result.Add(guide.transform.TransformPoint((v[i]+v[i+1])*.5f));return result;}
  static void ReplaceGuide(MeshFilter guide,List<Vector3> points,float width,string folder){var vertices=new List<Vector3>();var triangles=new List<int>();for(int i=0;i<points.Count;i++){var tangent=Vector3.ProjectOnPlane(points[Mathf.Min(points.Count-1,i+1)]-points[Mathf.Max(0,i-1)],Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,tangent)*width*.5f;vertices.Add(guide.transform.InverseTransformPoint(points[i]-side));vertices.Add(guide.transform.InverseTransformPoint(points[i]+side));if(i+1<points.Count){int a=i*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}}var mesh=new Mesh{name=guide.name+" Simplified Guide",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Guide-"+guide.name.Replace(" ","_")+".asset");Undo.RecordObject(guide,"Trim or reconnect road guide");guide.sharedMesh=mesh;}
  static void Save(Geometry g,string name,Material material,Transform parent,string folder,bool collision){var mesh=g.Mesh(name);AssetDatabase.CreateAsset(mesh,folder+"/"+name.Replace(" ","_")+".asset");var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;}

 }
}
#endif
