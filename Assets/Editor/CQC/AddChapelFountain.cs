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
 [InitializeOnLoad]public static class AddChapelFountain
 {
  const string Request="Assets/Editor/CQC/AddChapelFountain.request.txt";static double next;
  static AddChapelFountain(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddChapelFountain-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/City/Add Fountain In Front Of Chapel")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Mountain missing.");var existingRook=mountain.transform.Find("Old Fountain In Front Of Chapel");
   var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();if(!floor)throw new InvalidOperationException("Upper floor missing.");Bounds area=floor.bounds;Vector3 center=area.center;center.y=area.max.y;
   var castle=mountain.transform.Find("Central Upper Floor Building");if(!castle||!castle.gameObject.activeInHierarchy)castle=mountain.transform.Find("Central Castle (WDW)");if(!castle||!castle.gameObject.activeInHierarchy)throw new InvalidOperationException("Existing castle missing.");var castleBounds=BoundsOf(castle.gameObject);
   var church=mountain.transform.Find("Large Upper Castle Church");if(!church)throw new InvalidOperationException("Church missing; cannot locate marked courtyard.");var churchBounds=BoundsOf(church.gameObject);
   var existingTransforms=mountain.GetComponentsInChildren<Transform>(true).Where(t=>!existingRook||(t!=existingRook&&!t.IsChildOf(existingRook))).ToArray();var existingMatrices=existingTransforms.Select(t=>t.localToWorldMatrix).ToArray();var originalFloor=floor.sharedMesh;
   var obstacles=new List<Bounds>{castleBounds,churchBounds};var rook=mountain.transform.Find("Large Courtyard Rook Tower");if(rook)obstacles.Add(BoundsOf(rook.gameObject));var gardens=mountain.transform.Find("Courtyard Gardens On Both Sides");if(gardens)foreach(Transform side in gardens)foreach(Transform plant in side)obstacles.Add(BoundsOf(plant.gameObject));var smith=mountain.transform.Find("Upper Blacksmith And Boxwood Borders/Upper Courtyard Blacksmith");if(smith)obstacles.Add(BoundsOf(smith.gameObject));var towers=mountain.transform.Find("Upper Castle Walls And Towers/Eight Edge Towers");if(towers)foreach(Transform tower in towers)obstacles.Add(BoundsOf(tower.gameObject));
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-chapel-fountain-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   GameObject go;
   if(existingRook){go=existingRook.gameObject;Undo.RecordObject(go.transform,"Fit chapel fountain to courtyard corner");}
   else{var prefab=AddMarkedTaverns.Build("ChapelOldFountain");go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Old Fountain In Front Of Chapel";go.transform.SetParent(mountain.transform,true);Undo.RegisterCreatedObjectUndo(go,"Add corner chapel fountain");}
   go.transform.rotation=Quaternion.identity;
   Vector3 originalScale=go.transform.localScale;var originalBounds=BoundsOf(go);float originalHeight=originalBounds.size.y;float originalWidth=Mathf.Max(originalBounds.size.x,originalBounds.size.z);
   // Imported chapel front faces local -Z. Position the fountain in its approach courtyard.
   Vector3 front=Vector3.ProjectOnPlane(-church.forward,Vector3.up).normalized;float churchDepth=Mathf.Abs(front.x)*churchBounds.extents.x+Mathf.Abs(front.z)*churchBounds.extents.z;
   Vector3 marked=churchBounds.center+front*(churchDepth+6);marked.y=center.y;
   bool found=false;Vector3 bestPosition=Vector3.zero;Vector3 bestScale=originalScale;
   Physics.SyncTransforms();
   foreach(float width in new[]{6f,5f,4f})
   {
    Vector3 scale=originalScale*(width/originalWidth);go.transform.localScale=scale;float best=float.PositiveInfinity;
    for(float x=area.min.x+4;x<=area.max.x-4;x+=1.5f)for(float z=area.min.z+4;z<=area.max.z-4;z+=1.5f)
    {
     Vector3 target=new Vector3(x,center.y,z);if(Vector3.Dot(target-churchBounds.center,front)<churchDepth+width*.5f+2)continue;Place(go,target,center.y);var bounds=BoundsOf(go);
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
   if(!found)throw new InvalidOperationException("No supported space is free in the chapel approach for the fountain. Existing scene retained.");
   go.transform.localScale=bestScale;Place(go,bestPosition,center.y);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   for(int i=0;i<existingTransforms.Length;i++)if(existingTransforms[i].localToWorldMatrix!=existingMatrices[i])throw new InvalidOperationException("An existing castle/wall/mountain object moved; action rolled back.");if(floor.sharedMesh!=originalFloor)throw new InvalidOperationException("Upper floor geometry changed.");
   Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();var final=BoundsOf(go);Directory.CreateDirectory("Logs");if(File.Exists("Logs/AddChapelFountain-error.txt"))File.Delete("Logs/AddChapelFountain-error.txt");File.WriteAllText("Logs/AddChapelFountain-report.txt","Imported the_old_fountain_free.glb with original stone/metal textures. Added Old Fountain In Front Of Chapel in the chapel approach, height "+final.size.y.ToString("F2")+"m, footprint "+final.size.x.ToString("F2")+" x "+final.size.z.ToString("F2")+"m, center "+final.center+". Floor support and wall reserve checked; 2m clearance from existing church/castle/building/tower bounding boxes. Existing chapel, castle, rook, gardens, walls, elevator, mountain and floor geometry preserved. Static mesh colliders included.\nBackup: "+backup);Debug.Log("Fountain added in front of chapel. Scene saved.");
  }
  static bool Inside(Vector3 p,Vector3 c,float rx,float rz){return Mathf.Pow((p.x-c.x)/rx,2)+Mathf.Pow((p.z-c.z)/rz,2)<1;}
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.03f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Fountain model has no visible geometry.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static List<Vector3> Corners(GameObject go){var points=new List<Vector3>();foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)points.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}return points;}
 }
}
#endif
