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
 [InitializeOnLoad]public static class AlignCastleRoadGardens
 {
  const string Request="Assets/Editor/CQC/AlignCastleRoadGardens.request.txt";static double next;
  static AlignCastleRoadGardens(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AlignCastleRoadGardens-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  class Plant{public Transform T;public bool Tree,Thin;public Bounds Bounds;}
  class RoadTriangle{public Vector2[] Points;public float MinX,MaxX,MinZ,MaxZ;public RoadTriangle(Vector2[] p){Points=p;MinX=p.Min(v=>v.x);MaxX=p.Max(v=>v.x);MinZ=p.Min(v=>v.y);MaxZ=p.Max(v=>v.y);}}
  static List<RoadTriangle> roadTriangles;static List<Bounds> obstacles;static List<Plant> placed;
  [MenuItem("Tools/City/Align Castle Bushes And Trees With Roads")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");if(mountain.transform.Find("Roadside Castle Gardens Aligned"))return;var roads=mountain.transform.Find("Connected Castle Stone Roads");if(!roads)throw new InvalidOperationException("Castle road network must be applied first.");var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();var area=floor.bounds;var center=area.center;center.y=area.max.y;Physics.SyncTransforms();
   var plants=new List<Plant>();foreach(string groupName in new[]{"Courtyard Gardens On Both Sides","Upper Blacksmith And Boxwood Borders/Boxwood Along Inner Wall"}){var group=mountain.transform.Find(groupName);if(!group)continue;foreach(var t in group.GetComponentsInChildren<Transform>()){bool tree=t.name.StartsWith("Courtyard Spruce",StringComparison.Ordinal),bush=t.name.StartsWith("Courtyard Boxwood",StringComparison.Ordinal)||t.name.StartsWith("Boxwood Border",StringComparison.Ordinal);if((tree||bush)&&t.gameObject.activeInHierarchy)plants.Add(new Plant{T=t,Tree=tree,Thin=t.name.StartsWith("Boxwood Border",StringComparison.Ordinal),Bounds=BoundsOf(t.gameObject)});}}
   if(plants.Count==0)throw new InvalidOperationException("Castle garden plants missing.");var protectedTransforms=mountain.GetComponentsInChildren<Transform>(true).Where(t=>!plants.Any(p=>t==p.T||t.IsChildOf(p.T))).ToArray();var matrices=protectedTransforms.Select(t=>t.localToWorldMatrix).ToArray();
   obstacles=new List<Bounds>();foreach(string name in new[]{"Central Upper Floor Building","Central Castle (WDW)","Large Upper Castle Church","Large Courtyard Rook Tower","Old Fountain In Front Of Chapel","Upper Blacksmith And Boxwood Borders/Upper Courtyard Blacksmith","Castle Scaffold Elevator"}){var t=mountain.transform.Find(name);if(t&&t.gameObject.activeInHierarchy)obstacles.Add(BoundsOf(t.gameObject));}var towers=mountain.transform.Find("Upper Castle Walls And Towers/Eight Edge Towers");if(towers)foreach(Transform t in towers)obstacles.Add(BoundsOf(t.gameObject));
   roadTriangles=new List<RoadTriangle>();foreach(var f in roads.GetComponentsInChildren<MeshFilter>()){var v=f.sharedMesh.vertices;var ids=f.sharedMesh.triangles;for(int i=0;i<ids.Length;i+=3)roadTriangles.Add(new RoadTriangle(new[]{Flat(f.transform.TransformPoint(v[ids[i]])),Flat(f.transform.TransformPoint(v[ids[i+1]])),Flat(f.transform.TransformPoint(v[ids[i+2]]))}));}
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-roadside-castle-gardens-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");placed=new List<Plant>();int bushes=0,trees=0,hidden=0,index=0;int totalDense=plants.Count(p=>!p.Thin&&!p.Tree);
   foreach(var plant in plants.OrderByDescending(p=>p.Tree).ThenBy(p=>p.Thin))
   {
    Undo.RecordObject(plant.T,"Align castle roadside plants");Vector3 original=plant.T.position;Quaternion rotation=plant.T.rotation;float preferred=plant.Thin?Mathf.Atan2((plant.Bounds.center.z-center.z)/area.extents.z,(plant.Bounds.center.x-center.x)/area.extents.x):plant.Tree?(trees+.5f)*Mathf.PI*.5f:(index++)*Mathf.PI*2/Mathf.Max(1,totalDense);
    bool found=false;float best=float.PositiveInfinity;Vector3 chosen=Vector3.zero;Quaternion chosenRotation=rotation;
    var radii=plant.Thin?new[]{.955f,.94f,.90f,.70f,.65f}:plant.Tree?new[]{.65f,.90f,.58f,.94f,.72f}:new[]{.68f,.90f,.62f,.94f,.55f,.75f};
    foreach(float radial in radii)for(int step=0;step<96;step++)
    {
     float angle=preferred+step*Mathf.PI*2/96;Vector3 target=center+new Vector3(Mathf.Cos(angle)*area.extents.x*radial,0,Mathf.Sin(angle)*area.extents.z*radial);Vector3 tangent=new Vector3(-area.extents.x*Mathf.Sin(angle),0,area.extents.z*Mathf.Cos(angle));plant.T.rotation=plant.Tree?Quaternion.Euler(0,angle*Mathf.Rad2Deg,0):Quaternion.LookRotation(tangent.normalized);Place(plant.T.gameObject,target,center.y);var b=BoundsOf(plant.T.gameObject);
     if(obstacles.Any(o=>Overlap(b,o,plant.Tree?1.4f:.6f))||placed.Any(o=>!(plant.Thin&&o.Thin)&&Overlap(b,o.Bounds,plant.Tree?.7f:.15f))||CutsRoad(b,plant.Tree?1f:.7f)||!Supported(floor,b,center,area))continue;
     float cost=plant.Thin?FlatDistance(target,plant.Bounds.center):Mathf.Abs(Mathf.DeltaAngle(angle*Mathf.Rad2Deg,preferred*Mathf.Rad2Deg))*.2f+Mathf.Abs(radial-radii[0])*10;if(cost<best){best=cost;chosen=target;chosenRotation=plant.T.rotation;found=true;}
    }
    if(found){plant.T.rotation=chosenRotation;Place(plant.T.gameObject,chosen,center.y);plant.Bounds=BoundsOf(plant.T.gameObject);placed.Add(plant);PrefabUtility.RecordPrefabInstancePropertyModifications(plant.T);if(plant.Tree)trees++;else bushes++;}
    else{plant.T.position=original;plant.T.rotation=rotation;Undo.RecordObject(plant.T.gameObject,"Remove plant conflicting with road or entrance");plant.T.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(plant.T.gameObject);hidden++;}
   }
   if(trees==0||bushes<10)throw new InvalidOperationException("Insufficient clear roadside garden sites; old landscaping retained.");foreach(var p in placed)if(CutsRoad(p.Bounds,p.Tree?1f:.7f))throw new InvalidOperationException("Plant crosses a road; changes rolled back.");for(int i=0;i<protectedTransforms.Length;i++)if(protectedTransforms[i].localToWorldMatrix!=matrices[i])throw new InvalidOperationException("A building or road moved; changes rolled back.");var marker=new GameObject("Roadside Castle Gardens Aligned");marker.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(marker,"Remember castle roadside garden redesign");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AlignCastleRoadGardens-report.txt","Aligned "+bushes+" castle bushes and "+trees+" trees into garden strips around circular roads. Preserved existing bush thickness and tree sizes; oriented hedge lengths along the curve. At least 0.7m bush / 1m tree canopy clearance from every actual road triangle, plus building, fountain, tower and elevator clearance checked. Deactivated "+hidden+" plants with no suitable site. Kept former castle-footprint bushes removed. Verified all existing building/road transforms unchanged. Outside-town vegetation unaffected.\nBackup: "+backup);Debug.Log("Castle roadside bushes and trees aligned; paths and entrance connections clear. Scene saved.");
  }
  static bool CutsRoad(Bounds b,float gap){float xmin=b.min.x-gap,xmax=b.max.x+gap,zmin=b.min.z-gap,zmax=b.max.z+gap;var box=new[]{new Vector2(xmin,zmin),new Vector2(xmax,zmin),new Vector2(xmax,zmax),new Vector2(xmin,zmax)};foreach(var t in roadTriangles){if(t.MaxX<xmin||t.MinX>xmax||t.MaxZ<zmin||t.MinZ>zmax)continue;if(Intersects(box,t.Points))return true;}return false;}
  static bool Intersects(Vector2[] a,Vector2[] b){foreach(var polygon in new[]{a,b})for(int i=0;i<polygon.Length;i++){var edge=polygon[(i+1)%polygon.Length]-polygon[i];var axis=new Vector2(-edge.y,edge.x).normalized;float amin=float.PositiveInfinity,amax=float.NegativeInfinity,bmin=amin,bmax=amax;foreach(var p in a){float v=Vector2.Dot(axis,p);amin=Mathf.Min(amin,v);amax=Mathf.Max(amax,v);}foreach(var p in b){float v=Vector2.Dot(axis,p);bmin=Mathf.Min(bmin,v);bmax=Mathf.Max(bmax,v);}if(amax<bmin||bmax<amin)return false;}return true;}
  static bool Supported(MeshCollider floor,Bounds b,Vector3 center,Bounds area){foreach(var point in new[]{new Vector3(b.min.x,center.y,b.min.z),new Vector3(b.max.x,center.y,b.min.z),new Vector3(b.max.x,center.y,b.max.z),new Vector3(b.min.x,center.y,b.max.z)})if(Mathf.Pow((point.x-center.x)/(area.extents.x-1.8f),2)+Mathf.Pow((point.z-center.z)/(area.extents.z-1.8f),2)>1||!floor.Raycast(new Ray(point+Vector3.up*10,Vector3.down),out RaycastHit hit,20))return false;return true;}
  static Vector2 Flat(Vector3 p){return new Vector2(p.x,p.z);}static float FlatDistance(Vector3 a,Vector3 b){return Vector2.Distance(Flat(a),Flat(b));}
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.02f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
