#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class CastleElevatorSetup
 {
  const string Folder="Assets/Building/CastleElevator",Request="Assets/Editor/CQC/CastleElevatorSetup.request.txt";
  static CastleElevatorSetup(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleElevatorSetup-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Add Working Castle Elevator")] public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");if(mountain.transform.Find("Castle Scaffold Elevator"))return;
   var inner=mountain.transform.Find("Inner Castle Mountain");Bounds wall=inner.Find("Inner Rocky Mountain Sides").GetComponent<Renderer>().bounds;
   var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshCollider>();Bounds area=floor.bounds;
   float lower=area.max.y,upper=inner.Find("Flat Inner Castle Floor").GetComponent<Renderer>().bounds.max.y;
   string modelPath=Folder+"/D3DW_Fantasy_Elevator_Light.fbx";AssetDatabase.ImportAsset(modelPath,ImportAssetOptions.ForceSynchronousImport);
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);if(!prefab)throw new InvalidOperationException("Elevator FBX has not imported.");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-scaffold-elevator-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var oldRamp=inner.Find("Inner Mountain Access Ramp");if(oldRamp){foreach(var renderer in oldRamp.GetComponentsInChildren<Renderer>()){Undo.RecordObject(renderer,"Replace ramp access with elevator");renderer.enabled=false;}foreach(var collider in oldRamp.GetComponentsInChildren<Collider>()){Undo.RecordObject(collider,"Clear elevator shaft");collider.enabled=false;}}
   var timber=Mat("CastleLiftTimber",new Color(.25f,.115f,.045f),0,.16f);var metal=Mat("CastleLiftIron",new Color(.1f,.115f,.13f),.75f,.32f);
   var paving=AssetDatabase.LoadAssetAtPath<Material>("Assets/Building/Cobblestone/CastlePavingStone01.mat");
   var root=new GameObject("Castle Scaffold Elevator");root.transform.SetParent(mountain.transform,false);root.transform.position=new Vector3(wall.min.x-3f,lower,wall.center.z);root.transform.rotation=Quaternion.LookRotation(Vector3.left);Undo.RegisterCreatedObjectUndo(root,"Add castle elevator");
   var platform=new GameObject("Moving Elevator");platform.transform.SetParent(root.transform,false);
   var deck=Cube(platform.transform,"Load Bearing Lift Deck",new Vector3(0,-.15f,0),new Vector3(4.8f,.3f,4.8f),timber).GetComponent<BoxCollider>();
   var visual=(GameObject)PrefabUtility.InstantiatePrefab(prefab);visual.name="Imported Fantasy Elevator Cage";visual.transform.SetParent(platform.transform,false);
   bool found=false;foreach(var r in visual.GetComponentsInChildren<Renderer>()){bool keep=r.gameObject.name=="SM_Elevator_Base";r.enabled=keep;if(keep){found=true;r.sharedMaterial=timber;}}
   foreach(var col in visual.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(col);
   if(!found)throw new InvalidOperationException("SM_Elevator_Base mesh missing from provided FBX.");
   Bounds b=VisibleBounds(visual);visual.transform.localScale*=4.5f/Mathf.Max(b.size.x,b.size.z);b=VisibleBounds(visual);
   visual.transform.position+=new Vector3(platform.transform.position.x-b.center.x,lower-.3f-b.min.y,platform.transform.position.z-b.center.z);
   Cube(platform.transform,"Left Safety Rail",new Vector3(-2.35f,1.1f,0),new Vector3(.12f,.14f,4.7f),metal);
   Cube(platform.transform,"Right Safety Rail",new Vector3(2.35f,1.1f,0),new Vector3(.12f,.14f,4.7f),metal);
   float travel=upper-lower,frameHeight=travel+8.5f;
   for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Cube(root.transform,"Timber Guide Post",new Vector3(x*2.95f,frameHeight*.5f,z*2.65f),new Vector3(.4f,frameHeight,.4f),timber);
   foreach(float y in new[]{.5f,travel+.4f,frameHeight-.5f})
   {
    Cube(root.transform,"Left Scaffold Beam",new Vector3(-2.95f,y,0),new Vector3(.35f,.35f,5.7f),timber);
    Cube(root.transform,"Right Scaffold Beam",new Vector3(2.95f,y,0),new Vector3(.35f,.35f,5.7f),timber);
   }
   Beam(root.transform,"Left Cross Brace",new Vector3(-2.95f,.6f,-2.65f),new Vector3(-2.95f,travel,2.65f),.2f,timber);
   Beam(root.transform,"Right Cross Brace",new Vector3(2.95f,.6f,2.65f),new Vector3(2.95f,travel,-2.65f),.2f,timber);
   Cube(root.transform,"Roof Cap",new Vector3(0,frameHeight,0),new Vector3(6.7f,.35f,6.2f),timber);
   Cube(root.transform,"Upper Landing Bridge",new Vector3(0,travel-.15f,-3.1f),new Vector3(4.5f,.3f,2f),paving?paving:timber);
   Cube(root.transform,"Upper Left Handrail",new Vector3(-2.3f,travel+1.1f,-3.1f),new Vector3(.12f,.15f,2f),metal);
   Cube(root.transform,"Upper Right Handrail",new Vector3(2.3f,travel+1.1f,-3.1f),new Vector3(.12f,.15f,2f),metal);
   // Visible winch chains track the scaffold; the provided cage moves as one unit.
   for(int x=-1;x<=1;x+=2)Cube(root.transform,"Iron Lift Guide",new Vector3(x*2.6f,frameHeight*.5f,0),new Vector3(.08f,frameHeight,.08f),metal);
   var lever=(GameObject)PrefabUtility.InstantiatePrefab(prefab);lever.name="Lower Call Lever";lever.transform.SetParent(root.transform,false);foreach(var r in lever.GetComponentsInChildren<Renderer>()){r.enabled=r.gameObject.name=="SM_Lever_Base"||r.gameObject.name=="SM_Lever_Handle";if(r.enabled)r.sharedMaterial=metal;}foreach(var col in lever.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(col);
   b=VisibleBounds(lever);lever.transform.localScale*=1.2f/b.size.y;b=VisibleBounds(lever);Vector3 leverTarget=root.transform.TransformPoint(new Vector3(-2.7f,.1f,3));lever.transform.position+=new Vector3(leverTarget.x-b.center.x,leverTarget.y-b.min.y,leverTarget.z-b.center.z);
   var lift=root.AddComponent<StarterAssets.CastleElevator>();lift.Platform=platform.transform;lift.Deck=deck;lift.LowerHeight=lower;lift.UpperHeight=upper;lift.Speed=2.5f;lift.LowerCallPoint=root.transform.TransformPoint(new Vector3(0,0,3.5f));lift.UpperCallPoint=root.transform.TransformPoint(new Vector3(0,travel,-4.3f));
   Physics.SyncTransforms();
   if(!deck.Raycast(new Ray(platform.transform.position+Vector3.up*2,Vector3.down),out RaycastHit hit,4)||Mathf.Abs(hit.point.y-lower)>.01f)throw new InvalidOperationException("Lift deck height check failed.");
   for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){Vector3 p=root.transform.TransformPoint(new Vector3(x*2.95f,100,z*2.65f));if(!floor.Raycast(new Ray(p,Vector3.down),out hit,150))throw new InvalidOperationException("Lift scaffold is not supported by the castle floor.");}
   var bridge=root.transform.Find("Upper Landing Bridge").GetComponent<BoxCollider>();if(!bridge.Raycast(new Ray(lift.UpperCallPoint+root.transform.forward*.8f+Vector3.up*10,Vector3.down),out hit,20)||Mathf.Abs(hit.point.y-upper)>.01f)throw new InvalidOperationException("Upper landing height check failed.");
   EditorUtility.SetDirty(lift);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Selection.activeGameObject=root;SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleElevatorSetup-report.txt","Imported fantasy lift enlarged to a 4.5m cage over 4.8m deck. Timber scaffold, braces, roof, iron guides, call lever and upper bridge added at west entrance gap.\nTravel: "+travel+"m; speed 2.5m/s. E rides on deck or calls lift from either landing. CharacterController passengers carried without parenting.\nDeck, landing height and four scaffold floor support checks passed.\nBackup: "+backup);Debug.Log("Castle elevator ready. Step onto lift and press E; press E nearby to call it.");
  }
  static Material Mat(string name,Color color,float metallic,float smooth){string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(mat,path);}return mat;}
  static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 size,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
  static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float width,Material material){var go=Cube(parent,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),material);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
  static Bounds VisibleBounds(GameObject go){bool found=false;Bounds b=new Bounds();foreach(var r in go.GetComponentsInChildren<Renderer>())if(r.enabled){if(!found){b=r.bounds;found=true;}else b.Encapsulate(r.bounds);}if(!found)throw new InvalidOperationException("No visible imported model parts.");return b;}
 }
}
#endif

