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
 [InitializeOnLoad]public static class UpperBlacksmithAndHedges
 {
  const string Request="Assets/Editor/CQC/UpperBlacksmithAndHedges.request.txt";static double next;
  static UpperBlacksmithAndHedges(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/UpperBlacksmithAndHedges-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/City/Add Upper Blacksmith And Boxwood Borders")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle mountain missing.");if(mountain.transform.Find("Upper Blacksmith And Boxwood Borders"))return;
   var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();if(!floor)throw new InvalidOperationException("Upper floor missing.");var area=floor.bounds;Vector3 center=area.center;center.y=area.max.y;
   var castle=mountain.transform.Find("Central Upper Floor Building");if(!castle||!castle.gameObject.activeInHierarchy)castle=mountain.transform.Find("Central Castle (WDW)");if(!castle||!castle.gameObject.activeInHierarchy)throw new InvalidOperationException("Active central castle missing.");var castleBounds=BoundsOf(castle.gameObject);
   var wall=mountain.transform.Find("Upper Castle Walls And Towers");var protectedTransforms=wall.GetComponentsInChildren<Transform>(true).Concat(castle.GetComponentsInChildren<Transform>(true)).ToArray();var protectedMatrices=protectedTransforms.Select(t=>t.localToWorldMatrix).ToArray();var originalFloor=floor.sharedMesh;
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-blacksmith-hedges-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var building=AddMarkedTaverns.Build("UpperBlacksmithHouse");var bush=AddMarkedTaverns.Build("UpperBoxwoodBush",false);
   var root=new GameObject("Upper Blacksmith And Boxwood Borders");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add blacksmith and boxwood hedges");
   var smith=(GameObject)PrefabUtility.InstantiatePrefab(building);smith.name="Upper Courtyard Blacksmith";smith.transform.SetParent(root.transform,true);var b=BoundsOf(smith);smith.transform.localScale*=6.5f/b.size.y;
   Vector3 marked=center+new Vector3(-area.extents.x*.64f,0,area.extents.z*.54f);Vector3 chosen=Vector3.zero;Quaternion chosenRotation=Quaternion.identity;bool found=false;float best=float.PositiveInfinity;
   Physics.SyncTransforms();
   for(int ix=0;ix<=15;ix++)for(int iz=-12;iz<=6;iz++)
   {
    Vector3 target=marked+new Vector3(ix,0,iz);Vector3 outward=Vector3.ProjectOnPlane(target-center,Vector3.up).normalized;smith.transform.rotation=Quaternion.LookRotation(outward);Place(smith,target,center.y);b=BoundsOf(smith);
    if(Overlap(b,castleBounds,2))continue;var points=Corners(smith);bool supported=points.All(point=>OnFloor(floor,point,center.y)&&InsideRim(point,center,area.extents.x-3.4f,area.extents.z-3.4f));if(!supported)continue;
    float cost=Vector3.Distance(target,marked);if(cost<best){best=cost;chosen=target;chosenRotation=smith.transform.rotation;found=true;}
   }
   if(!found)throw new InvalidOperationException("No supported blacksmith lot near the red mark.");smith.transform.rotation=chosenRotation;Place(smith,chosen,center.y);var smithBounds=BoundsOf(smith);PrefabUtility.RecordPrefabInstancePropertyModifications(smith.transform);
   var wallHedges=new GameObject("Boxwood Along Inner Wall");wallHedges.transform.SetParent(root.transform,false);var castleHedges=new GameObject("Boxwood Around Castle");castleHedges.transform.SetParent(root.transform,false);int plants=0,skipped=0;
   Action<Transform,Vector3,Vector3> add=(parent,position,tangent)=>
   {
    var go=(GameObject)PrefabUtility.InstantiatePrefab(bush);go.transform.SetParent(parent,true);go.transform.rotation=Quaternion.LookRotation(tangent.normalized);var bounds=BoundsOf(go);var natural=go.GetComponentsInChildren<MeshFilter>().First().sharedMesh.bounds;
    // GLB uses local Z for the long boxwood row; preserve a low, narrow border.
    var total=BoundsOf(bush);go.transform.localScale=new Vector3(.85f/total.size.x,1.15f/total.size.y,2.6f/total.size.z);Place(go,position,center.y);bounds=BoundsOf(go);
    if(Overlap(bounds,smithBounds,.9f)||Overlap(bounds,castleBounds,.25f)||!Corners(go).All(point=>OnFloor(floor,point,center.y))){UnityEngine.Object.DestroyImmediate(go);skipped++;return;}
    go.name="Boxwood Border "+(++plants).ToString("000");PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   };
   float rx=area.extents.x-3.2f,rz=area.extents.z-3.2f,circumference=Mathf.PI*(3*(rx+rz)-Mathf.Sqrt((3*rx+rz)*(rx+3*rz)));int around=Mathf.CeilToInt(circumference/2.25f);
   for(int i=0;i<around;i++){float a=(i+.5f)*Mathf.PI*2/around;if(Mathf.Abs(Mathf.DeltaAngle(a*Mathf.Rad2Deg,180))<12)continue;Vector3 position=center+new Vector3(rx*Mathf.Cos(a),0,rz*Mathf.Sin(a));Vector3 tangent=new Vector3(-rx*Mathf.Sin(a),0,rz*Mathf.Cos(a));add(wallHedges.transform,position,tangent);}
   // Border the castle footprint, with openings on all four sides for its doors and circulation.
   float x0=castleBounds.min.x-1.05f,x1=castleBounds.max.x+1.05f,z0=castleBounds.min.z-1.05f,z1=castleBounds.max.z+1.05f;
   for(int side=0;side<4;side++)
   {
    Vector3 a=side==0?new Vector3(x0,center.y,z0):side==1?new Vector3(x1,center.y,z0):side==2?new Vector3(x1,center.y,z1):new Vector3(x0,center.y,z1);
    Vector3 q=side==0?new Vector3(x1,center.y,z0):side==1?new Vector3(x1,center.y,z1):side==2?new Vector3(x0,center.y,z1):new Vector3(x0,center.y,z0);float length=Vector3.Distance(a,q);int segments=Mathf.CeilToInt(length/2.25f);
    for(int i=0;i<segments;i++){float along=(i+.5f)*length/segments;if(Mathf.Abs(along-length*.5f)<3.5f)continue;add(castleHedges.transform,Vector3.Lerp(a,q,(i+.5f)/segments),q-a);}
   }
   if(plants<30)throw new InvalidOperationException("Too few supported boxwood border positions.");for(int i=0;i<protectedTransforms.Length;i++)if(protectedTransforms[i].localToWorldMatrix!=protectedMatrices[i])throw new InvalidOperationException("Wall or castle moved; changes rolled back.");if(floor.sharedMesh!=originalFloor)throw new InvalidOperationException("Floor geometry changed.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/UpperBlacksmithAndHedges-report.txt","Imported supplied blacksmith house and boxwood with original textures and cutout transparency. Added blacksmith near northwest red mark at "+smithBounds.center+", height "+smithBounds.size.y.ToString("F2")+"m. Added "+plants+" low boxwood border sections along upper wall and around central castle; "+skipped+" conflicting/unsupported positions skipped. Main west ramp, castle side entrances and blacksmith frontage left clear. Floor support and castle/building separation checked. Existing castle/wall/tower transforms and floor mesh verified unchanged. Bushes have no blocking colliders; blacksmith retains mesh colliders.\nBackup: "+backup);Debug.Log("Upper blacksmith and boxwood wall/castle borders added. Scene saved.");
  }
  static bool InsideRim(Vector3 p,Vector3 c,float rx,float rz){return Mathf.Pow((p.x-c.x)/rx,2)+Mathf.Pow((p.z-c.z)/rz,2)<1;}
  static bool OnFloor(MeshCollider floor,Vector3 p,float y){return floor.Raycast(new Ray(new Vector3(p.x,y+50,p.z),Vector3.down),out RaycastHit hit,100);}
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.02f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Visible model missing.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static List<Vector3> Corners(GameObject go){var result=new List<Vector3>();foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)result.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}return result;}
 }
}
#endif
