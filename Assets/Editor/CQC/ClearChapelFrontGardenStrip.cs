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
 [InitializeOnLoad]public static class ClearChapelFrontGardenStrip
 {
  const string Request="Assets/Editor/CQC/ClearChapelFrontGardenStrip.request.txt";static double next;
  static ClearChapelFrontGardenStrip(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ClearChapelFrontGardenStrip-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/City/Remove Marked Garden Strip In Front Of Chapel")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");if(mountain.transform.Find("Chapel Front Garden Strip Cleared"))return;var chapel=mountain.transform.Find("Large Upper Castle Church");var plates=mountain.transform.Find("Two Inner Mixed Grass Garden Plates");if(!chapel||!plates)throw new InvalidOperationException("Chapel and garden plates required.");var b=BoundsOf(chapel.gameObject);var front=Vector3.ProjectOnPlane(-chapel.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,front).normalized;float depth=Mathf.Abs(front.x)*b.extents.x+Mathf.Abs(front.z)*b.extents.z;var origin=b.center+front*(depth-.3f);float halfWidth=7f;var region=new[]{Flat(origin-right*halfWidth),Flat(origin+right*halfWidth),Flat(origin+right*halfWidth+front*14),Flat(origin-right*halfWidth+front*14)};
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-clear-chapel-front-strip-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");string folder="Assets/CityExpansion/ClearChapelFront-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();int removed=0,plants=0;
   foreach(var filter in plates.GetComponentsInChildren<MeshFilter>())
   {
    if(filter.name!="First Curved Garden Plate"&&filter.name!="Opposite Curved Garden Plate"&&filter.name!="Low Stone Plate Borders")continue;var mesh=filter.sharedMesh;var v=mesh.vertices;var ids=mesh.triangles;var kept=new List<int>();int cut=0;
    for(int i=0;i<ids.Length;i+=3){var triangle=new[]{Flat(filter.transform.TransformPoint(v[ids[i]])),Flat(filter.transform.TransformPoint(v[ids[i+1]])),Flat(filter.transform.TransformPoint(v[ids[i+2]]))};if(Intersects(triangle,region)){cut++;continue;}kept.AddRange(new[]{ids[i],ids[i+1],ids[i+2]});}
    if(cut==0)continue;var copy=UnityEngine.Object.Instantiate(mesh);copy.name=mesh.name+" Front Entrance Cleared";copy.SetTriangles(kept,0);copy.RecalculateBounds();AssetDatabase.CreateAsset(copy,folder+"/"+filter.name.Replace(" ","")+".asset");Undo.RecordObject(filter,"Cut marked chapel-front garden strip");filter.sharedMesh=copy;var collider=filter.GetComponent<MeshCollider>();if(collider){Undo.RecordObject(collider,"Update cleared garden collision");collider.sharedMesh=copy;}removed+=cut;
   }
   foreach(var t in plates.GetComponentsInChildren<Transform>()){if(!t.name.StartsWith("Grass Clump",StringComparison.Ordinal)&&!t.name.StartsWith("Flowers ",StringComparison.Ordinal))continue;if(!InRegion(t.position,origin,front,right,halfWidth))continue;Undo.RecordObject(t.gameObject,"Remove grass and flowers from marked chapel frontage");t.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);plants++;}
   var gardens=mountain.transform.Find("Courtyard Gardens On Both Sides");if(gardens)foreach(var t in gardens.GetComponentsInChildren<Transform>()){if(!t.name.StartsWith("Courtyard Boxwood",StringComparison.Ordinal))continue;if(!InRegion(BoundsOf(t.gameObject).center,origin,front,right,halfWidth))continue;Undo.RecordObject(t.gameObject,"Clear frontage shrub");t.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);plants++;}
   if(removed==0)throw new InvalidOperationException("No garden surface found in marked chapel-front area; existing scene retained.");var marker=new GameObject("Chapel Front Garden Strip Cleared");marker.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(marker,"Remember chapel-front cleanup");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ClearChapelFrontGardenStrip-report.txt","Removed marked narrow garden projection from central chapel frontage. Cut "+removed+" garden/border triangles and cleared "+plants+" grass/flower/shrub objects in 14m-wide front approach. Updated plate collider meshes to reveal original paved floor; main side plates retained. Chapel, castle, fountain, roads and other models retained in place.\nBackup: "+backup);Debug.Log("Marked front garden strip removed; chapel approach is paved and open. Scene saved.");
  }
  static bool InRegion(Vector3 p,Vector3 origin,Vector3 front,Vector3 right,float width){var d=p-origin;float forward=Vector3.Dot(d,front);return forward>=-.4f&&forward<=14.4f&&Mathf.Abs(Vector3.Dot(d,right))<=width+.4f;}
  static Vector2 Flat(Vector3 p){return new Vector2(p.x,p.z);}
  static bool Intersects(Vector2[] a,Vector2[] b){foreach(var polygon in new[]{a,b})for(int i=0;i<polygon.Length;i++){var edge=polygon[(i+1)%polygon.Length]-polygon[i];if(edge.sqrMagnitude<.00001f)continue;var axis=new Vector2(-edge.y,edge.x).normalized;float amin=float.PositiveInfinity,amax=float.NegativeInfinity,bmin=amin,bmax=amax;foreach(var p in a){float v=Vector2.Dot(axis,p);amin=Mathf.Min(amin,v);amax=Mathf.Max(amax,v);}foreach(var p in b){float v=Vector2.Dot(axis,p);bmin=Mathf.Min(bmin,v);bmax=Mathf.Max(bmax,v);}if(amax<bmin||bmax<amin)return false;}return true;}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
