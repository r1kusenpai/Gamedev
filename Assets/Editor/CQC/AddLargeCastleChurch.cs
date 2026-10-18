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
 [InitializeOnLoad]public static class AddLargeCastleChurch
 {
  const string Request="Assets/Editor/CQC/AddLargeCastleChurch.request.txt";static double next;
  static AddLargeCastleChurch(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddLargeCastleChurch-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/City/Add Large Castle Church At Upper Courtyard Mark")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Mountain missing.");if(mountain.transform.Find("Large Upper Castle Church"))return;
   var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();if(!floor)throw new InvalidOperationException("Upper floor missing.");Bounds area=floor.bounds;Vector3 center=area.center;center.y=area.max.y;
   var castle=mountain.transform.Find("Central Upper Floor Building");if(!castle||!castle.gameObject.activeInHierarchy)castle=mountain.transform.Find("Central Castle (WDW)");if(!castle||!castle.gameObject.activeInHierarchy)throw new InvalidOperationException("Existing castle missing.");var castleBounds=BoundsOf(castle.gameObject);
   var existingTransforms=mountain.GetComponentsInChildren<Transform>(true);var existingMatrices=existingTransforms.Select(t=>t.localToWorldMatrix).ToArray();var originalFloor=floor.sharedMesh;
   var obstacles=new List<Bounds>{castleBounds};var smith=mountain.transform.Find("Upper Blacksmith And Boxwood Borders/Upper Courtyard Blacksmith");if(smith)obstacles.Add(BoundsOf(smith.gameObject));var towers=mountain.transform.Find("Upper Castle Walls And Towers/Eight Edge Towers");if(towers)foreach(Transform tower in towers)obstacles.Add(BoundsOf(tower.gameObject));
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-large-church-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var prefab=AddMarkedTaverns.Build("UpperCastleChurch");var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Large Upper Castle Church";go.transform.SetParent(mountain.transform,true);Undo.RegisterCreatedObjectUndo(go,"Add large castle church");
   Vector3 originalScale=go.transform.localScale;float originalHeight=BoundsOf(go).size.y;Vector3 marked=center+Vector3.right*area.extents.x*.43f;bool found=false;Vector3 bestPosition=Vector3.zero;Quaternion bestRotation=Quaternion.identity;float chosenHeight=0;
   Physics.SyncTransforms();
   foreach(float height in new[]{40f,38f,36f,34f,32f})
   {
    go.transform.localScale=originalScale*(height/originalHeight);float best=float.PositiveInfinity;
    for(int x=-3;x<=10;x++)for(int z=-7;z<=7;z++)
    {
     Vector3 target=marked+new Vector3(x*2f,0,z*2f);Vector3 facing=Vector3.ProjectOnPlane(target-castleBounds.center,Vector3.up).normalized;go.transform.rotation=Quaternion.LookRotation(facing);Place(go,target,center.y);var bounds=BoundsOf(go);
     if(obstacles.Any(b=>Overlap(bounds,b,4)))continue;
     var points=Corners(go);bool fits=points.All(point=>Inside(point,center,area.extents.x-3.4f,area.extents.z-3.4f)&&floor.Raycast(new Ray(new Vector3(point.x,center.y+80,point.z),Vector3.down),out RaycastHit hit,130));if(!fits)continue;
     float cost=Vector3.Distance(target,marked);if(cost<best){best=cost;bestPosition=target;bestRotation=go.transform.rotation;chosenHeight=height;found=true;}
    }
    if(found)break;
   }
   if(!found)throw new InvalidOperationException("Large church cannot fit the marked side with clear paths; old scene retained.");go.transform.localScale=originalScale*(chosenHeight/originalHeight);go.transform.rotation=bestRotation;Place(go,bestPosition,center.y);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   for(int i=0;i<existingTransforms.Length;i++)if(existingTransforms[i].localToWorldMatrix!=existingMatrices[i])throw new InvalidOperationException("An existing castle/wall/mountain object moved; action rolled back.");if(floor.sharedMesh!=originalFloor)throw new InvalidOperationException("Upper floor geometry changed.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();var final=BoundsOf(go);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddLargeCastleChurch-report.txt","Imported castle_church.glb with original materials/textures and transparency. Added Large Upper Castle Church on marked right/east side of raised courtyard, height "+final.size.y.ToString("F2")+"m, footprint "+final.size.x.ToString("F2")+" x "+final.size.z.ToString("F2")+"m, center "+final.center+". All model bounding corners supported by floor and inside wall reserve; 4m clearance from existing castle/building/tower bounding boxes checked. Existing castle, blacksmith, towers, walls, elevator, mountain and floor geometry preserved. Static mesh colliders included.\nBackup: "+backup);Debug.Log("Large castle church added beside existing upper castle. Scene saved.");
  }
  static bool Inside(Vector3 p,Vector3 c,float rx,float rz){return Mathf.Pow((p.x-c.x)/rx,2)+Mathf.Pow((p.z-c.z)/rz,2)<1;}
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.03f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Church model has no visible geometry.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static List<Vector3> Corners(GameObject go){var points=new List<Vector3>();foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)points.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}return points;}
 }
}
#endif
