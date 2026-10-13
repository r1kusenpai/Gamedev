#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class ReplaceSlopeWindmillsRetry
 {
  const string Request="Assets/Editor/CQC/ReplaceSlopeWindmillsRetry.request.txt";static double next;
  static ReplaceSlopeWindmillsRetry(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ReplaceSlopeWindmillsRetry-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/City/Replace Slope Windmills Using Mesh Parts")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Mountain missing.");var old=mountain.transform.Find("Five Slope Windmills");if(!old)throw new InvalidOperationException("Existing five windmills missing.");var slopes=mountain.transform.Find("Rocky Mountain Slopes").GetComponent<MeshCollider>();if(!slopes)throw new InvalidOperationException("Mountain slope collider missing.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-replace-windmills-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   string folder="Assets/CityExpansion/ReplacedWindmills-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();var prefab=AddMarkedTaverns.Build("WaterWindmillOnly");
   var model=JsonUtility.FromJson<AddMarkedTaverns.Model>(File.ReadAllText("Assets/Building/ImportedTaverns/WaterWindmillOnly/Model.json"));
   int basePart=Array.FindIndex(model.primitives,pr=>model.materials[pr.material].name=="MillBase"),bladePart=Array.FindIndex(model.primitives,pr=>model.materials[pr.material].name=="MillSpokes");
   if(basePart<0||bladePart<0)throw new InvalidOperationException("Source manifest must contain windmill body and blade parts.");
   if(!prefab.transform.Find("Model Part "+basePart)||!prefab.transform.Find("Model Part "+bladePart))throw new InvalidOperationException("Windmill prefab mesh parts do not match source manifest.");
   var group=new GameObject("Wall Embedded Windmills");group.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(group,"Replace windmills");Physics.SyncTransforms();var area=slopes.bounds;Vector3 center=area.center;center.y=area.min.y+area.size.y*.42f;float range=Mathf.Max(area.size.x,area.size.z)*2;int count=0;
   foreach(float angle in new float[]{-42,-22,17,34,50})
   {
    Vector3 outward=Quaternion.Euler(0,angle,0)*Vector3.back;if(!slopes.Raycast(new Ray(center+outward*range,-outward),out RaycastHit hit,range*2))throw new InvalidOperationException("Missing slope hit for windmill "+angle);
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(group.transform,true);go.name="Wall Windmill "+(++count).ToString("00");
    var baseRenderer=go.transform.Find("Model Part "+basePart).GetComponent<MeshRenderer>();var spokes=go.transform.Find("Model Part "+bladePart).GetComponent<MeshRenderer>();
    Bounds all=BoundsOf(go);go.transform.localScale*=18f/all.size.y;
    Vector3 heading=Vector3.ProjectOnPlane(spokes.bounds.center-baseRenderer.bounds.center,Vector3.up);if(heading.sqrMagnitude<.001f)throw new InvalidOperationException("Cannot determine blade-facing direction.");go.transform.rotation=Quaternion.FromToRotation(heading.normalized,outward)*go.transform.rotation;
    var body=baseRenderer.bounds;float halfDepth=Mathf.Abs(outward.x)*body.extents.x+Mathf.Abs(outward.z)*body.extents.z;
    Vector3 target=hit.point+outward*halfDepth*.25f;go.transform.position+=new Vector3(target.x-body.center.x,target.y-body.min.y,target.z-body.center.z);
    // Recenter the blade mesh at its hub so only the blades rotate.
    var fan=spokes.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(fan.sharedMesh);Vector3 pivot=mesh.bounds.center;var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]-=pivot;mesh.vertices=vertices;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Rotor-"+count+".asset");fan.sharedMesh=mesh;fan.transform.localPosition+=pivot;var collision=fan.GetComponent<Collider>();if(collision)UnityEngine.Object.DestroyImmediate(collision);fan.gameObject.AddComponent<CastleWorld.WindmillRotor>();
    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   }
   // Remove all previous ledges and brackets with the old windmill group.
   Undo.DestroyObjectImmediate(old.gameObject);group.name="Five Slope Windmills";AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ReplaceSlopeWindmillsRetry-report.txt","Replaced five slope windmills with isolated windmill_near_water model. Normalized to Y-up, height 18m, original textures retained, blades rotate. Windmill bases overlap the mountain wall; original walkway excluded, and all previous floor ledges and support blocks removed. Funicular, roads, castle and town retained.\nBackup: "+backup);Debug.Log("Five isolated windmills embedded into the mountain wall; no added floors. Scene saved.");
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();Bounds b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
