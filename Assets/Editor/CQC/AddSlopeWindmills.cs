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
 [InitializeOnLoad]public static class AddSlopeWindmills
 {
  const string Request="Assets/Editor/CQC/AddSlopeWindmills.request.txt";static double next;
  static AddSlopeWindmills(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddSlopeWindmills-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/City/Add Five Marked Slope Windmills")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Mountain missing.");if(mountain.transform.Find("Five Slope Windmills"))return;
   var sides=mountain.transform.Find("Rocky Mountain Slopes").GetComponent<MeshCollider>();if(!sides)throw new InvalidOperationException("Slope collider missing.");
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polygrade/Top_Down Medieval City/Windmill_Free/Prefabs/Medieval_Windmill.prefab");var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Polygrade/Top_Down Medieval City/Windmill_Free/Fbxs_Textures_Mats/WindmillAtlas.tga");if(!prefab||!atlas)throw new InvalidOperationException("Imported windmill missing.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-slope-windmills-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   string folder="Assets/CityExpansion/SlopeWindmills-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("URP shader missing.");
   var material=new Material(shader){name="Windmill Atlas URP"};material.SetTexture("_BaseMap",atlas);material.SetColor("_BaseColor",Color.white);material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.5f);material.EnableKeyword("_ALPHATEST_ON");material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=2450;material.SetFloat("_Cull",0);material.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(material,folder+"/Windmill.mat");
   var stone=new Material(shader){name="Windmill Ledge Stone"};stone.SetColor("_BaseColor",new Color(.32f,.31f,.27f));AssetDatabase.CreateAsset(stone,folder+"/Ledge.mat");
   var group=new GameObject("Five Slope Windmills");group.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(group,"Add slope windmills");Physics.SyncTransforms();
   var bounds=sides.bounds;Vector3 center=bounds.center;center.y=bounds.min.y+(bounds.max.y-bounds.min.y)*.42f;float range=Mathf.Max(bounds.size.x,bounds.size.z)*2;
   float[] angles={-42,-22,17,34,50};int count=0;
   foreach(float angle in angles)
   {
    Vector3 outward=Quaternion.Euler(0,angle,0)*Vector3.back;
    if(!sides.Raycast(new Ray(center+outward*range,-outward),out RaycastHit hit,range*2))throw new InvalidOperationException("No slope surface at marked windmill angle "+angle);
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Slope Windmill "+(++count).ToString("00");go.transform.SetParent(group.transform,true);foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>material).ToArray();
    var b=BoundsOf(go);go.transform.localScale*=18f/b.size.y;b=BoundsOf(go);
    var fan=go.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Fan");Vector3 heading=fan?Vector3.ProjectOnPlane(BoundsOf(fan.gameObject).center-b.center,Vector3.up):Vector3.back;if(heading.sqrMagnitude<.01f)heading=Vector3.back;go.transform.rotation=Quaternion.FromToRotation(heading.normalized,outward)*go.transform.rotation;b=BoundsOf(go);
    float depth=Mathf.Abs(outward.x)*b.extents.x+Mathf.Abs(outward.z)*b.extents.z;Vector3 target=hit.point+outward*(depth+.55f);go.transform.position+=new Vector3(target.x-b.center.x,target.y+.08f-b.min.y,target.z-b.center.z);b=BoundsOf(go);
    var ledge=GameObject.CreatePrimitive(PrimitiveType.Cube);ledge.name="Level Stone Windmill Ledge "+count;ledge.transform.SetParent(group.transform,true);ledge.transform.position=new Vector3(b.center.x,target.y-.45f,b.center.z);ledge.transform.localScale=new Vector3(b.size.x+.9f,1f,b.size.z+.9f);ledge.GetComponent<Renderer>().sharedMaterial=stone;
    // A solid bracket makes the level ledge visibly supported by the mountain.
    var bracket=GameObject.CreatePrimitive(PrimitiveType.Cube);bracket.name="Mountain Ledge Support "+count;bracket.transform.SetParent(group.transform,true);bracket.transform.position=Vector3.Lerp(hit.point,target,.5f)-Vector3.up*2;bracket.transform.rotation=Quaternion.LookRotation(outward);bracket.transform.localScale=new Vector3(Mathf.Max(2,b.size.x*.65f),4,depth+1.5f);bracket.GetComponent<Renderer>().sharedMaterial=stone;
    foreach(var filter in go.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh&&!filter.GetComponent<Collider>())filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddSlopeWindmills-report.txt","Added five imported medieval windmills on level supported ledges along the front castle slope, two left and three right of the funicular. Model height 18m, original atlas retained with compatible URP cutout material; mesh colliders added. Slope hit tests passed 5/5. Castle, roads, houses and funicular retained.\nBackup: "+backup);Debug.Log("Five medieval windmills added along marked castle slope. Scene saved.");
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Windmill mesh missing.");Bounds b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
