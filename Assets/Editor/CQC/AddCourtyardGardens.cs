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
 [InitializeOnLoad]public static class AddCourtyardGardens
 {
  const string Request="Assets/Editor/CQC/AddCourtyardGardens.request.txt";static double next;
  static AddCourtyardGardens(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddCourtyardGardens-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/City/Add Courtyard Bushes And Trees On Both Sides")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");if(mountain.transform.Find("Courtyard Gardens On Both Sides"))return;
   var floor=mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>();if(!floor)throw new InvalidOperationException("Courtyard floor missing.");Physics.SyncTransforms();var area=floor.bounds;var center=area.center;center.y=area.max.y;
   var protectedTransforms=mountain.GetComponentsInChildren<Transform>(true);var matrices=protectedTransforms.Select(t=>t.localToWorldMatrix).ToArray();var floorMesh=floor.sharedMesh;
   var obstacles=new List<Bounds>();foreach(string path in new[]{"Central Upper Floor Building","Central Castle (WDW)","Large Upper Castle Church","Large Courtyard Rook Tower","Upper Blacksmith And Boxwood Borders/Upper Courtyard Blacksmith","Castle Scaffold Elevator/Upper Landing Bridge"}){var t=mountain.transform.Find(path);if(t&&t.gameObject.activeInHierarchy)obstacles.Add(BoundsOf(t.gameObject));}
   var towers=mountain.transform.Find("Upper Castle Walls And Towers/Eight Edge Towers");if(towers)foreach(Transform t in towers)obstacles.Add(BoundsOf(t.gameObject));
   const string nature="Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Trees/Fir/Prefabs/";
   var treeA=AssetDatabase.LoadAssetAtPath<GameObject>(nature+"UNS_Spruce_01.prefab");var treeB=AssetDatabase.LoadAssetAtPath<GameObject>(nature+"UNS_Spruce_02.prefab");if(!treeA||!treeB)throw new InvalidOperationException("Existing spruce tree assets missing.");var bush=AddMarkedTaverns.Build("UpperBoxwoodBush",false);
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-courtyard-gardens-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var root=new GameObject("Courtyard Gardens On Both Sides");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add courtyard gardens");var placed=new List<Bounds>();int trees=0,bushes=0;var counts=new int[2];
   for(int side=0;side<2;side++)
   {
    var group=new GameObject(side==0?"South Courtyard Garden":"North Courtyard Garden");group.transform.SetParent(root.transform,false);float middle=side==0?270:90;
    // Two curved garden strips, with open ends and gaps between hedge sections.
    for(int i=0;i<25;i++)
    {
     bool isTree=i%6==2;float angle=(middle-44+i*88f/24)*Mathf.Deg2Rad;bool accepted=false;
     foreach(float radial in new[]{.79f,.84f,.74f,.88f})
     {
      var prefab=isTree?(i%2==0?treeA:treeB):bush;var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(group.transform,true);go.transform.rotation=Quaternion.Euler(0,isTree?i*47+side*31:-angle*Mathf.Rad2Deg,0);var b=BoundsOf(go);
      if(isTree)go.transform.localScale*= (7f+(i%3)*.6f)/b.size.y;
      else go.transform.localScale=Vector3.Scale(go.transform.localScale,new Vector3(1f/b.size.x,1.1f/b.size.y,2.8f/b.size.z));
      Vector3 target=center+new Vector3(area.extents.x*radial*Mathf.Cos(angle),0,area.extents.z*radial*Mathf.Sin(angle));Place(go,target,center.y);b=BoundsOf(go);
      if(obstacles.Any(o=>Overlap(b,o,isTree?2f:1.5f))||placed.Any(o=>Overlap(b,o,.35f))||!Supported(floor,b,center,area)){UnityEngine.Object.DestroyImmediate(go);continue;}
      go.name=isTree?"Courtyard Spruce "+(++trees).ToString("00"):"Courtyard Boxwood "+(++bushes).ToString("00");PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);placed.Add(b);counts[side]++;accepted=true;break;
     }
     if(!accepted)continue;
    }
   }
   if(counts[0]<3||counts[1]<3||trees<2)throw new InvalidOperationException("Insufficient open space for gardens on both sides; scene retained.");
   for(int i=0;i<protectedTransforms.Length;i++)if(protectedTransforms[i].localToWorldMatrix!=matrices[i])throw new InvalidOperationException("An existing object moved; scene retained.");if(floor.sharedMesh!=floorMesh)throw new InvalidOperationException("Floor geometry changed.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddCourtyardGardens-report.txt","Added "+bushes+" boxwood sections and "+trees+" spruce trees to two curved courtyard strips. South garden: "+counts[0]+" plants; opposite north garden: "+counts[1]+" plants. Original boxwood textures and UNS spruce materials/LODs reused. Building, rook, tower and elevator clearance checked; plant bases supported by existing floor. Existing structures and floor preserved.\nBackup: "+backup);Debug.Log("Courtyard bushes and trees added on both sides. Scene saved.");
  }
  static bool Supported(MeshCollider floor,Bounds b,Vector3 center,Bounds area){float radius=Mathf.Max(b.extents.x,b.extents.z);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Vector3 p=new Vector3(b.center.x+Mathf.Cos(a)*radius,center.y,b.center.z+Mathf.Sin(a)*radius);if(Mathf.Pow((p.x-center.x)/(area.extents.x-3),2)+Mathf.Pow((p.z-center.z)/(area.extents.z-3),2)>=1||!floor.Raycast(new Ray(p+Vector3.up*60,Vector3.down),out RaycastHit hit,120))return false;}return true;}
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.02f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Plant model has no visible geometry.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
