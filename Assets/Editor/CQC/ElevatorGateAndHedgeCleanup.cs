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
 [InitializeOnLoad]public static class ElevatorGateAndHedgeCleanup
 {
  const string Request="Assets/Editor/CQC/ElevatorGateAndHedgeCleanup.request.txt";static double next;
  static ElevatorGateAndHedgeCleanup(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ElevatorGateAndHedgeCleanup-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/City/Clear Castle Bushes And Add Elevator Gate Towers")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");var towerGroup=mountain.transform.Find("Upper Castle Walls And Towers/Eight Edge Towers");var elevator=mountain.transform.Find("Castle Scaffold Elevator");var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();if(!towerGroup||!elevator||!floor)throw new InvalidOperationException("Upper tower group, elevator or floor missing.");var bridge=elevator.Find("Upper Landing Bridge");if(!bridge)throw new InvalidOperationException("Elevator upper landing missing.");
   var elevatorMatrix=elevator.localToWorldMatrix;var floorMesh=floor.sharedMesh;var area=floor.bounds;Vector3 center=area.center;center.y=area.max.y;var landing=bridge.GetComponent<Collider>()?bridge.GetComponent<Collider>().bounds:BoundsOf(bridge.gameObject);float rx=area.extents.x-.25f,rz=area.extents.z-.25f;
   float angle=Mathf.Atan2((landing.center.z-center.z)/rz,(landing.center.x-center.x)/rx);Vector3 tangent=new Vector3(-rx*Mathf.Sin(angle),0,rz*Mathf.Cos(angle)).normalized;if(Vector3.Dot(tangent,elevator.right)<0)tangent=-tangent;
   var prefab=AddMarkedTaverns.Build("KickelhahnTower");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-elevator-gate-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var hedgeRoot=mountain.transform.Find("Upper Blacksmith And Boxwood Borders");int removed=0;if(hedgeRoot){var castleHedges=hedgeRoot.Find("Boxwood Around Castle");if(castleHedges){removed+=castleHedges.childCount;Undo.DestroyObjectImmediate(castleHedges.gameObject);}}
   var existing=towerGroup.Cast<Transform>().Where(t=>t.GetComponentInChildren<Renderer>()).ToList();var used=new HashSet<Transform>();var gateBounds=new List<Bounds>();int reused=0,added=0;
   // Only the upper part of the scaffold matters for upper-floor entrance clearance.
   var frame=elevator.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.bounds.max.y>=center.y).ToArray();float frameSpan=frame.Length>0?ProjectedSpan(frame.Select(r=>r.bounds),tangent):6.7f;float passageHalf=Mathf.Max(3.35f,frameSpan*.5f)+.4f;
   for(int side=-1;side<=1;side+=2)
   {
    string name=side<0?"Elevator Gate Tower Left":"Elevator Gate Tower Right";var already=towerGroup.Find(name);
    float offset=passageHalf+3.5f;Vector3 approx=landing.center+tangent*offset*side;float theta=Mathf.Atan2((approx.z-center.z)/rz,(approx.x-center.x)/rx);Vector3 target=center+new Vector3(rx*Mathf.Cos(theta),0,rz*Mathf.Sin(theta));
    Transform tower=already;bool fresh=false;if(!tower){tower=existing.Where(t=>!used.Contains(t)).OrderBy(t=>FlatDistance(BoundsOf(t.gameObject).center,target)).FirstOrDefault();if(tower&&FlatDistance(BoundsOf(tower.gameObject).center,target)>10)tower=null;}
    if(!tower){var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(towerGroup,true);tower=go.transform;Undo.RegisterCreatedObjectUndo(go,"Add elevator gate tower");fresh=true;added++;}else{Undo.RecordObject(tower,"Position elevator gate tower");Undo.RecordObject(tower.gameObject,"Name elevator gate tower");reused++;}
    tower.name=name;used.Add(tower);Vector3 outward=Vector3.ProjectOnPlane(target-center,Vector3.up).normalized;tower.rotation=Quaternion.LookRotation(outward);
    if(fresh){var b=BoundsOf(tower.gameObject);tower.localScale*=14f/b.size.y;b=BoundsOf(tower.gameObject);tower.localScale*=Mathf.Min(1,7f/Mathf.Max(b.size.x,b.size.z));}
    var bounds=BoundsOf(tower.gameObject);tower.position+=new Vector3(target.x-bounds.center.x,center.y+.01f-bounds.min.y,target.z-bounds.center.z);bounds=BoundsOf(tower.gameObject);
    if(!floor.Raycast(new Ray(target+Vector3.up*50,Vector3.down),out RaycastHit hit,100))throw new InvalidOperationException("Gate tower base center not on upper floor.");
    foreach(var r in frame)if(FlatOverlap(bounds,r.bounds,.15f))throw new InvalidOperationException("Elevator gate tower intersects scaffold; original scene retained.");gateBounds.Add(bounds);PrefabUtility.RecordPrefabInstancePropertyModifications(tower);
   }
   // Retain other wall bushes, but clear the gate-tower footprints and landing approach.
   if(hedgeRoot){var border=hedgeRoot.Find("Boxwood Along Inner Wall");if(border)foreach(var bush in border.Cast<Transform>().ToArray()){var bounds=BoundsOf(bush.gameObject);bool near=gateBounds.Any(b=>FlatOverlap(bounds,b,.6f))||FlatDistance(bounds.center,landing.center)<7;if(near){Undo.DestroyObjectImmediate(bush.gameObject);removed++;}}}
   if(elevator.localToWorldMatrix!=elevatorMatrix||floor.sharedMesh!=floorMesh)throw new InvalidOperationException("Elevator or floor changed; action rolled back.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ElevatorGateAndHedgeCleanup-report.txt","Removed "+removed+" boxwood sections around the central castle and beside the elevator gate. Added matching left/right elevator entrance towers on the upper wall curve ("+reused+" nearby towers reused, "+added+" new). Tower floor support and scaffold clearance checked. Elevator transform, landing, transport scripts and upper floor mesh retained. Other wall hedges, blacksmith and funicular preserved.\nBackup: "+backup);Debug.Log("Castle bushes cleared and two elevator gate towers aligned. Scene saved.");
  }
  static float FlatDistance(Vector3 a,Vector3 b){return Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));}
  static bool FlatOverlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>().Where(x=>x.enabled).ToArray();if(r.Length==0)throw new InvalidOperationException("Visible model missing.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static float ProjectedSpan(IEnumerable<Bounds> bounds,Vector3 axis){float lo=float.PositiveInfinity,hi=float.NegativeInfinity;foreach(var b in bounds)for(int x=0;x<2;x++)for(int z=0;z<2;z++){float v=Vector3.Dot(new Vector3(x==0?b.min.x:b.max.x,b.center.y,z==0?b.min.z:b.max.z),axis);lo=Mathf.Min(lo,v);hi=Mathf.Max(hi,v);}return hi-lo;}
 }
}
#endif
