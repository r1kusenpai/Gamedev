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
 [InitializeOnLoad]public static class CastleMerchantStores
 {
  const string Request="Assets/Editor/CQC/CastleMerchantStores.request.txt";static double next;
  static CastleMerchantStores(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleMerchantStores-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/City/Add Merchant Stores Inside Outer Castle Wall")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle mountain missing.");if(mountain.transform.Find("Outer Wall Merchant Stores"))return;
   var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshCollider>();if(!floor)throw new InvalidOperationException("Outer walkway floor missing.");var area=floor.bounds;Vector3 center=area.center;center.y=area.max.y;
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-merchants-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   string[] keys={"StockedMerchantStall","FlatMerchantTent","ArchedMerchantTent"};var prefabs=keys.Select(k=>AddMarkedTaverns.Build(k)).ToArray();
   var obstacles=new List<Bounds>();foreach(string path in new[]{"One Ring Of Houses","Added Marked Taverns","Outer Castle Walls And Towers/Eight Edge Towers"}){var group=mountain.transform.Find(path);if(group)foreach(Transform child in group)if(child.GetComponentInChildren<Renderer>())obstacles.Add(BoundsOf(child.gameObject));}
   var root=new GameObject("Outer Wall Merchant Stores");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add merchant stores");int count=0,checks=0;var kinds=new int[3];
   // The blue marks follow the inside of the outside wall, beyond the existing house row.
   float rx=area.extents.x-6.3f,rz=area.extents.z-6.3f;const int candidates=64;Physics.SyncTransforms();
   for(int i=0;i<candidates;i++)
   {
    float angle=(i+.5f)*Mathf.PI*2/candidates,degree=angle*Mathf.Rad2Deg;
    if(Mathf.Abs(Mathf.DeltaAngle(degree,270))<15||Mathf.Abs(Mathf.DeltaAngle(degree,180))<8)continue;
    Vector3 outward=new Vector3(Mathf.Cos(angle)/rx,0,Mathf.Sin(angle)/rz).normalized;
    Vector3 target=center+new Vector3(Mathf.Cos(angle)*rx,0,Mathf.Sin(angle)*rz);
    int kind=count%3;var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind]);go.transform.SetParent(root.transform,true);go.transform.rotation=Quaternion.LookRotation(outward);var b=BoundsOf(go);
    float scale=Mathf.Min(3.6f/b.size.y,5.8f/Mathf.Max(b.size.x,b.size.z));go.transform.localScale*=scale;b=BoundsOf(go);
    go.transform.position+=new Vector3(target.x-b.center.x,center.y+.03f-b.min.y,target.z-b.center.z);b=BoundsOf(go);
    if(obstacles.Any(other=>Overlap(b,other,.65f))){UnityEngine.Object.DestroyImmediate(go);continue;}
    var corners=Corners(go);bool supported=true;foreach(var corner in corners){if(!floor.Raycast(new Ray(new Vector3(corner.x,center.y+80,corner.z),Vector3.down),out RaycastHit hit,120)){supported=false;break;}}
    if(!supported){UnityEngine.Object.DestroyImmediate(go);continue;}
    // Keep the complete footprint inside the parapet, with a small rear gap.
    bool wallClear=true;foreach(var point in corners){float x=(point.x-center.x)/(area.extents.x-4f),z=(point.z-center.z)/(area.extents.z-4f);if(x*x+z*z>1){wallClear=false;break;}}
    if(!wallClear){UnityEngine.Object.DestroyImmediate(go);continue;}
    go.name=(kind==0?"Stocked Merchant ":kind==1?"Flat Tent Merchant ":"Arched Tent Merchant ")+(++count).ToString("00");obstacles.Add(b);kinds[kind]++;checks+=corners.Count;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   }
   if(count<8||kinds.Any(n=>n==0))throw new InvalidOperationException("Not enough clear merchant positions; changes rolled back.");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleMerchantStores-report.txt","Imported supplied stocked merchant stall and separated flat/arched medieval tent designs with original textures and URP materials. Added "+count+" merchant stores along inside edge of outer castle wall (stocked/flat/arched: "+string.Join(", ",kinds)+"). Storefronts face inward toward walkway. " +checks+" floor-support corner checks passed; house/store/tower separation checked. South funicular entrance and west elevator gap preserved. No additional terrain or floors created. Scene saved.\nBackup: "+backup);Debug.Log("Merchant stores added along the inside edge of the castle wall. Scene saved.");
  }
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Merchant model missing.");Bounds b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static List<Vector3> Corners(GameObject go){var result=new List<Vector3>();foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)result.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}return result;}
 }
}
#endif
