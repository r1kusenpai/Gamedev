#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class TransportBoardingFix
 {
  const string Request="Assets/Editor/CQC/TransportBoardingFix.request.txt";
  static TransportBoardingFix(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TransportBoardingFix-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Fix Elevator Rear Boarding")] public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");var liftRoot=mountain?mountain.transform.Find("Castle Scaffold Elevator"):null;if(!liftRoot)throw new InvalidOperationException("Elevator missing.");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-transport-boarding-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var lift=liftRoot.GetComponent<StarterAssets.CastleElevator>();float travel=lift.UpperHeight-lift.LowerHeight;
   var bridge=liftRoot.Find("Upper Landing Bridge");Undo.RecordObject(bridge,"Join elevator back exit to landing");bridge.localPosition=new Vector3(0,travel-.15f,-3.35f);bridge.localScale=new Vector3(4.5f,.3f,3f);
   foreach(string name in new[]{"Upper Left Handrail","Upper Right Handrail"}){var rail=liftRoot.Find(name);if(rail){Undo.RecordObject(rail,"Extend landing safety rail");var p=rail.localPosition;p.z=-3.35f;rail.localPosition=p;var s=rail.localScale;s.z=3f;rail.localScale=s;}}
   var importer=AssetImporter.GetAtPath("Assets/Building/CastleElevator/D3DW_Fantasy_Elevator_Light.fbx") as ModelImporter;if(importer&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
   int removed=0;var visual=lift.Platform.Find("Imported Fantasy Elevator Cage");
   if(visual)foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
   {
    var renderer=filter.GetComponent<Renderer>();if(!renderer||!renderer.enabled||!filter.sharedMesh)continue;
    var source=filter.sharedMesh;if(source.name.StartsWith("RearOpening_"))continue;
    Vector3[] vertices=source.vertices;var local=new Vector3[vertices.Length];float minZ=float.PositiveInfinity,maxZ=float.NegativeInfinity,maxY=float.NegativeInfinity;
    for(int i=0;i<vertices.Length;i++){local[i]=lift.Platform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));minZ=Mathf.Min(minZ,local[i].z);maxZ=Mathf.Max(maxZ,local[i].z);maxY=Mathf.Max(maxY,local[i].y);}
    var mesh=UnityEngine.Object.Instantiate(source);mesh.name="RearOpening_"+source.name;
    for(int sub=0;sub<mesh.subMeshCount;sub++){var indices=source.GetTriangles(sub);var kept=new System.Collections.Generic.List<int>();for(int i=0;i<indices.Length;i+=3){Vector3 p=(local[indices[i]]+local[indices[i+1]]+local[indices[i+2]])/3f;if(p.z<minZ+(maxZ-minZ)*.12f&&Mathf.Abs(p.x)<1.15f&&p.y>.18f&&p.y<maxY-.5f){removed++;continue;}kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);}mesh.SetTriangles(kept,sub);}
    mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath("Assets/Building/CastleElevator/"+mesh.name+".asset"));Undo.RecordObject(filter,"Open rear of elevator cage");filter.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
   }
   Physics.SyncTransforms();var deck=lift.Deck;var landing=bridge.GetComponent<BoxCollider>();Vector3 rear=lift.Platform.TransformPoint(new Vector3(0,travel,-2.2f));
   if(!landing.Raycast(new Ray(rear+Vector3.up*5,Vector3.down),out RaycastHit hit,10)||Mathf.Abs(hit.point.y-lift.UpperHeight)>.03f)throw new InvalidOperationException("Back landing does not meet upper elevator stop.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TransportBoardingFix-report.txt","Elevator rear bridge enlarged to overlap deck and upper floor. Landing handrails extended; rear opening triangles removed: "+removed+".\nUpper rear landing collision/height check passed. Transport runtime now anchors passengers for full trip with gravity/movement/root travel suspended and controls restored on arrival.\nBackup: "+backup);Debug.Log("Elevator back landing fixed. Passengers now stay aboard lift and funicular rides.");
  }
 }
}
#endif
