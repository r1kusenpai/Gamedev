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
 [InitializeOnLoad]public static class AddCourtyardRookTower
 {
  const string Request="Assets/Editor/CQC/AddCourtyardRookTower.request.txt";static double next;
  static AddCourtyardRookTower(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddCourtyardRookTower-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/City/Fit Rook Tower To Courtyard Corner")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Mountain missing.");var existingRook=mountain.transform.Find("Large Courtyard Rook Tower");
   var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();if(!floor)throw new InvalidOperationException("Upper floor missing.");Bounds area=floor.bounds;Vector3 center=area.center;center.y=area.max.y;
   var castle=mountain.transform.Find("Central Upper Floor Building");if(!castle||!castle.gameObject.activeInHierarchy)castle=mountain.transform.Find("Central Castle (WDW)");if(!castle||!castle.gameObject.activeInHierarchy)throw new InvalidOperationException("Existing castle missing.");var castleBounds=BoundsOf(castle.gameObject);
   var church=mountain.transform.Find("Large Upper Castle Church");if(!church)throw new InvalidOperationException("Church missing; cannot locate marked courtyard.");var churchBounds=BoundsOf(church.gameObject);
   var existingTransforms=mountain.GetComponentsInChildren<Transform>(true).Where(t=>!existingRook||(t!=existingRook&&!t.IsChildOf(existingRook))).ToArray();var existingMatrices=existingTransforms.Select(t=>t.localToWorldMatrix).ToArray();var originalFloor=floor.sharedMesh;
   var obstacles=new List<Bounds>{castleBounds,churchBounds};var smith=mountain.transform.Find("Upper Blacksmith And Boxwood Borders/Upper Courtyard Blacksmith");if(smith)obstacles.Add(BoundsOf(smith.gameObject));var towers=mountain.transform.Find("Upper Castle Walls And Towers/Eight Edge Towers");if(towers)foreach(Transform tower in towers)obstacles.Add(BoundsOf(tower.gameObject));
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-rook-tower-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   GameObject go;
   if(existingRook){go=existingRook.gameObject;Undo.RecordObject(go.transform,"Fit rook tower to courtyard corner");}
   else{var prefab=AddMarkedTaverns.Build("CourtyardRookTower");go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Large Courtyard Rook Tower";go.transform.SetParent(mountain.transform,true);Undo.RegisterCreatedObjectUndo(go,"Add corner rook tower");}
   go.transform.rotation=Quaternion.identity;
   Vector3 originalScale=go.transform.localScale;var originalBounds=BoundsOf(go);float originalHeight=originalBounds.size.y;float originalWidth=Mathf.Max(originalBounds.size.x,originalBounds.size.z);
   // The marked front corner is on the church's southern courtyard side.
   Vector3 marked=center+new Vector3(Mathf.Sign(churchBounds.center.x-center.x)*area.extents.x*.40f,0,-area.extents.z*.70f);
   bool found=false;Vector3 bestPosition=Vector3.zero;Vector3 bestScale=originalScale;
   Physics.SyncTransforms();
   foreach(Vector2 dimensions in new[]{new Vector2(18,10),new Vector2(18,8),new Vector2(16,7),new Vector2(14,6)})
   {
    Vector3 scale=Vector3.Scale(originalScale,new Vector3(dimensions.y/originalWidth,dimensions.x/originalHeight,dimensions.y/originalWidth));go.transform.localScale=scale;float best=float.PositiveInfinity;
    for(float x=area.min.x+4;x<=area.max.x-4;x+=1.5f)for(float z=area.min.z+4;z<=area.max.z-4;z+=1.5f)
    {
     Vector3 target=new Vector3(x,center.y,z);Place(go,target,center.y);var bounds=BoundsOf(go);
     if(obstacles.Any(b=>Overlap(bounds,b,2)))continue;
     // Validate the footprint on the actual floor, with a reserve for the wall.
     bool fits=true;
     for(int i=0;i<16;i++){float angle=i*Mathf.PI/8;var point=new Vector3(bounds.center.x+Mathf.Cos(angle)*(bounds.extents.x+1.5f),center.y,bounds.center.z+Mathf.Sin(angle)*(bounds.extents.z+1.5f));if(!Inside(point,center,area.extents.x-2,area.extents.z-2)||!floor.Raycast(new Ray(point+Vector3.up*80,Vector3.down),out RaycastHit hit,130)){fits=false;break;}}
     if(!fits)continue;
     float cost=(target-marked).sqrMagnitude;
     if(cost<best){best=cost;bestPosition=target;bestScale=scale;found=true;}
    }
    if(found)break;
   }
   if(!found)throw new InvalidOperationException("No supported courtyard corner is free for the rook tower. Existing scene retained.");
   go.transform.localScale=bestScale;Place(go,bestPosition,center.y);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   for(int i=0;i<existingTransforms.Length;i++)if(existingTransforms[i].localToWorldMatrix!=existingMatrices[i])throw new InvalidOperationException("An existing castle/wall/mountain object moved; action rolled back.");if(floor.sharedMesh!=originalFloor)throw new InvalidOperationException("Upper floor geometry changed.");
   Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();var final=BoundsOf(go);Directory.CreateDirectory("Logs");if(File.Exists("Logs/AddCourtyardRookTower-error.txt"))File.Delete("Logs/AddCourtyardRookTower-error.txt");File.WriteAllText("Logs/AddCourtyardRookTower-report.txt","Imported chess_rook.glb with original material and texture. Added one Large Courtyard Rook Tower at the open courtyard corner, height "+final.size.y.ToString("F2")+"m, footprint "+final.size.x.ToString("F2")+" x "+final.size.z.ToString("F2")+"m, center "+final.center+". Floor support and wall reserve checked; 2m clearance from existing church/castle/building/tower bounding boxes. Existing church, castle, walls, elevator, mountain and floor geometry preserved. Static mesh colliders included.\nBackup: "+backup);Debug.Log("Large courtyard rook tower added at the open courtyard corner. Scene saved.");
  }
  static bool Inside(Vector3 p,Vector3 c,float rx,float rz){return Mathf.Pow((p.x-c.x)/rx,2)+Mathf.Pow((p.z-c.z)/rz,2)<1;}
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.03f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Rook model has no visible geometry.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static List<Vector3> Corners(GameObject go){var points=new List<Vector3>();foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)points.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}return points;}
 }
}
#endif
