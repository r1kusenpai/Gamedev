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
 [InitializeOnLoad]public static class AlignUpperWallToEdge
 {
  const string Request="Assets/Editor/CQC/AlignUpperWallToEdge.request.txt";static double next;
  static AlignUpperWallToEdge(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AlignUpperWallToEdge-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Align Only Upper Walls And Towers To Floor Edge")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");var root=mountain.transform.Find("Upper Castle Walls And Towers");if(!root)throw new InvalidOperationException("Inner wall group missing.");var wallGroup=root.Find("Perimeter Wall Sections");var towerGroup=root.Find("Eight Edge Towers");var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();if(!wallGroup||!towerGroup||!floor)throw new InvalidOperationException("Inner walls/towers/floor missing.");
   var area=floor.bounds;Vector3 center=area.center;center.y=area.max.y;
   var protectedTransforms=mountain.GetComponentsInChildren<Transform>(true).Where(t=>!t.IsChildOf(root)&&t!=root).ToArray();var matrices=protectedTransforms.Select(t=>t.localToWorldMatrix).ToArray();var floorMesh=floor.sharedMesh;
   var wallList=wallGroup.Cast<Transform>().Where(t=>t.GetComponentInChildren<Renderer>()).ToArray();var towers=towerGroup.Cast<Transform>().Where(t=>t.GetComponentInChildren<Renderer>()).ToArray();if(wallList.Length<20||towers.Length<4)throw new InvalidOperationException("Inner perimeter is incomplete.");
   float[] thickness=wallList.Select(t=>{var outwards=Vector3.ProjectOnPlane(BoundsOf(t.gameObject).center-center,Vector3.up).normalized;return Span(t.gameObject,outwards);}).OrderBy(x=>x).ToArray();float inset=Mathf.Max(.25f,thickness[thickness.Length/2]*.5f+.12f);if(inset>2)throw new InvalidOperationException("Unexpected upper wall thickness.");float rx=area.extents.x-inset,rz=area.extents.z-inset;
   float gateHalf=7f*Mathf.Deg2Rad,start=Mathf.PI+gateHalf,sweep=2*Mathf.PI-2*gateHalf;
   wallList=wallList.OrderBy(t=>{float a=Angle(BoundsOf(t.gameObject).center,center,area.extents.x,area.extents.z);return a<start?a+2*Mathf.PI:a;}).ToArray();
   var towerAngles=towers.Select(t=>Angle(BoundsOf(t.gameObject).center,center,area.extents.x,area.extents.z)).ToArray();
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-upper-wall-edge-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");var joints=new List<Vector3>();
   for(int i=0;i<wallList.Length;i++)
   {
    var wall=wallList[i];Undo.RecordObject(wall,"Align upper wall to floor edge");float a=start+sweep*i/wallList.Length,b=start+sweep*(i+1)/wallList.Length;Vector3 p=Edge(center,rx,rz,a),q=Edge(center,rx,rz,b),direction=(q-p).normalized,target=(p+q)*.5f;
    wall.rotation=Quaternion.LookRotation(Vector3.Cross(direction,Vector3.up));float length=Span(wall.gameObject,direction);var scale=wall.localScale;scale.x*=((q-p).magnitude+.75f)/length;wall.localScale=scale;Place(wall.gameObject,target,center.y);joints.Add(q);PrefabUtility.RecordPrefabInstancePropertyModifications(wall);
   }
   for(int i=0;i<towers.Length;i++)
   {
    var tower=towers[i];Undo.RecordObject(tower,"Align upper tower to edge");float a=towerAngles[i];Vector3 target=Edge(center,rx,rz,a),outwards=new Vector3(Mathf.Cos(a)/rx,0,Mathf.Sin(a)/rz).normalized;tower.rotation=Quaternion.LookRotation(outwards);Place(tower.gameObject,target,center.y);PrefabUtility.RecordPrefabInstancePropertyModifications(tower);
   }
   Physics.SyncTransforms();int checks=0;for(int i=0;i<wallList.Length-1;i++){Vector3 outward=Vector3.ProjectOnPlane(joints[i]-center,Vector3.up).normalized;Ray ray=new Ray(joints[i]+Vector3.up*2+outward*10,-outward);bool closed=wallList[i].GetComponentsInChildren<MeshCollider>().Concat(wallList[i+1].GetComponentsInChildren<MeshCollider>()).Any(c=>c.Raycast(ray,out RaycastHit hit,20));if(!closed)throw new InvalidOperationException("Upper wall gap at join "+(i+1)+"; alignment rolled back.");checks++;}
   for(int i=0;i<protectedTransforms.Length;i++)if(protectedTransforms[i].localToWorldMatrix!=matrices[i])throw new InvalidOperationException("A protected mountain/building object moved; changes rolled back.");if(floor.sharedMesh!=floorMesh)throw new InvalidOperationException("Inner floor geometry changed.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AlignUpperWallToEdge-report.txt","Moved only "+wallList.Length+" existing upper wall sections and "+towers.Length+" existing upper towers to the raised-floor edge, retaining "+inset.ToString("F2")+"m inset for wall thickness. Wall panels overlap by 0.75m; "+checks+" join raycasts verified. Existing west ramp opening retained. Floor geometry and "+protectedTransforms.Length+" protected mountain/castle/house/rock/windmill/outer-perimeter transforms verified unchanged. No new towers, floors or terrain created.\nBackup: "+backup);Debug.Log("Inner wall and towers aligned to the upper floor edge. Scene saved.");
  }
  static float Angle(Vector3 p,Vector3 c,float rx,float rz){float a=Mathf.Atan2((p.z-c.z)/rz,(p.x-c.x)/rx);return a<0?a+2*Mathf.PI:a;}
  static Vector3 Edge(Vector3 c,float rx,float rz,float a){return c+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.01f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static float Span(GameObject go,Vector3 axis){float low=float.PositiveInfinity,high=float.NegativeInfinity;foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++){Vector3 point=f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z));float d=Vector3.Dot(point,axis);low=Mathf.Min(low,d);high=Mathf.Max(high,d);}}return high-low;}
 }
}
#endif
