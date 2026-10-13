#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class CastleFunicularSetup
 {
  const string Request="Assets/Editor/CQC/CastleFunicularSetup.request.txt";
  static CastleFunicularSetup(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleFunicularSetup-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Add Castle Funicular")] public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");if(mountain.transform.Find("Castle Two Track Funicular"))return;
   var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<Renderer>().bounds;var sides=mountain.transform.Find("Rocky Mountain Slopes").GetComponent<Renderer>().bounds;
   var cageSource=mountain.transform.Find("Castle Scaffold Elevator/Moving Elevator/Imported Fantasy Elevator Cage");if(!cageSource)throw new InvalidOperationException("Existing imported elevator cage required.");
   var railPrefab=AddMarkedTaverns.Build("FunicularRail");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-funicular-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   foreach(string name in new[]{"Mountain Access Ramp","Ramp Rock Embankment"}){var old=mountain.transform.Find(name);if(old){foreach(var r in old.GetComponentsInChildren<Renderer>()){Undo.RecordObject(r,"Clear funicular route");r.enabled=false;}foreach(var c in old.GetComponentsInChildren<Collider>()){Undo.RecordObject(c,"Clear funicular route");c.enabled=false;}}}
   var root=new GameObject("Castle Two Track Funicular");root.transform.SetParent(mountain.transform,false);root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;Undo.RegisterCreatedObjectUndo(root,"Add castle funicular");
   var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Building/CastleElevator/CastleLiftTimber.mat");var iron=AssetDatabase.LoadAssetAtPath<Material>("Assets/Building/CastleElevator/CastleLiftIron.mat");
   float bottomZ=sides.min.z-7f,topZ=floor.min.z-2.4f;float ground=Ground(floor.center.x,bottomZ);float lowerY=ground+3.4f,upperY=floor.max.y;
   var lift=root.AddComponent<StarterAssets.CastleFunicular>();lift.Carts=new Transform[2];lift.Decks=new BoxCollider[2];lift.BottomPoints=new Vector3[2];lift.TopPoints=new Vector3[2];lift.Speed=4f;int sections=0;
   for(int lane=0;lane<2;lane++)
   {
    float x=floor.center.x+(lane==0?-2.8f:2.8f);Vector3 bottom=new Vector3(x,lowerY,bottomZ),top=new Vector3(x,upperY,topZ);lift.BottomPoints[lane]=bottom;lift.TopPoints[lane]=top;
    Vector3 rb=bottom-Vector3.up*3f,rt=top-Vector3.up*3f,dir=(rt-rb).normalized,normal=Vector3.Cross(dir,Vector3.right).normalized;float length=Vector3.Distance(rb,rt);int n=Mathf.CeilToInt(length/5.7f);
    var track=new GameObject("Railway Track "+(lane+1));track.transform.SetParent(root.transform,false);
    for(int i=0;i<n;i++)
    {
     Vector3 p=Vector3.Lerp(rb,rt,(i+.5f)/n);var rail=(GameObject)PrefabUtility.InstantiatePrefab(railPrefab);rail.name="Imported Rail Section "+(i+1);rail.transform.SetParent(track.transform,false);rail.transform.rotation=Quaternion.LookRotation(Vector3.left,normal);
     rail.transform.localScale=new Vector3((length/n+.05f)/4f,1.45f,4.2f/2.8151f);Bounds b=BoundsOf(rail);rail.transform.position+=p-b.center;
     foreach(var col in rail.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(col);sections++;
     float supportGround=Ground(p.x,p.z);if(p.y>supportGround+.5f){Cube(root.transform,"Track Timber Support",new Vector3(p.x,(p.y+supportGround)*.5f-.2f,p.z),new Vector3(.3f,p.y-supportGround,.3f),wood);}
    }
    var cart=new GameObject("Funicular Cart "+(lane+1));cart.transform.SetParent(root.transform,false);cart.transform.position=lane==0?bottom:top;lift.Carts[lane]=cart.transform;
    lift.Decks[lane]=Cube(cart.transform,"Level Passenger Deck",new Vector3(0,-.15f,0),new Vector3(3.8f,.3f,4.4f),wood,true).GetComponent<BoxCollider>();
    var cage=UnityEngine.Object.Instantiate(cageSource.gameObject);cage.name="Elevator Style Carriage Without Chains";cage.transform.SetParent(cart.transform,false);cage.transform.localRotation=Quaternion.identity;
    foreach(var renderer in cage.GetComponentsInChildren<Renderer>())if(!renderer.enabled||renderer.name.IndexOf("Chain",StringComparison.OrdinalIgnoreCase)>=0)UnityEngine.Object.DestroyImmediate(renderer.gameObject);
    Bounds cb=BoundsOf(cage);cage.transform.localScale*=3.5f/Mathf.Max(cb.size.x,cb.size.z);cb=BoundsOf(cage);cage.transform.position+=new Vector3(cart.transform.position.x-cb.center.x,cart.transform.position.y-.3f-cb.min.y,cart.transform.position.z-cb.center.z);
    foreach(var collider in cage.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
    Cube(cart.transform,"Left Carriage Handrail",new Vector3(-1.84f,1.05f,0),new Vector3(.1f,.15f,4.2f),iron,true);Cube(cart.transform,"Right Carriage Handrail",new Vector3(1.84f,1.05f,0),new Vector3(.1f,.15f,4.2f),iron,true);
    for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2){var wheel=GameObject.CreatePrimitive(PrimitiveType.Cylinder);wheel.name="Cart Wheel";wheel.transform.SetParent(cart.transform,false);wheel.transform.localPosition=new Vector3(side*1.7f,end==1?-1.65f:-3f,end*1.6f);wheel.transform.localRotation=Quaternion.Euler(0,0,90);wheel.transform.localScale=new Vector3(.65f,.15f,.65f);wheel.GetComponent<Renderer>().sharedMaterial=iron;UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());}
   }
   lift.LowerStation=new Vector3(floor.center.x,lowerY,bottomZ-3.5f);lift.UpperStation=new Vector3(floor.center.x,upperY,topZ+3.5f);
   Cube(root.transform,"Lower Boarding Platform",lift.LowerStation-Vector3.up*.2f,new Vector3(11f,.4f,3f),wood,true);Cube(root.transform,"Upper Castle Boarding Bridge",lift.UpperStation-Vector3.up*.2f,new Vector3(11f,.4f,3f),wood,true);
   int stairs=Mathf.CeilToInt((lowerY-ground)/.2f);for(int i=0;i<stairs;i++){float h=(i+1)*(lowerY-ground)/stairs;Cube(root.transform,"Forest Access Step "+(i+1),new Vector3(floor.center.x,ground+h*.5f,bottomZ-5.3f-(stairs-1-i)*.5f),new Vector3(4f,h,.55f),wood,true);}
   // Stations have small timber shelters while both carriage exits remain open.
   foreach(Vector3 station in new[]{lift.LowerStation,lift.UpperStation}){foreach(float x in new[]{-5f,5f})Cube(root.transform,"Station Shelter Post",station+new Vector3(x,3.3f,0),new Vector3(.25f,6.6f,.25f),wood);Cube(root.transform,"Station Shelter Roof",station+Vector3.up*6.8f,new Vector3(11.5f,.3f,3.6f),wood);}
   Physics.SyncTransforms();for(int i=0;i<2;i++){if(!lift.Decks[i].Raycast(new Ray(lift.Carts[i].position+Vector3.up*2,Vector3.down),out RaycastHit hit,4)||Mathf.Abs(hit.point.y-lift.Carts[i].position.y)>.01f)throw new InvalidOperationException("Cart deck is not level.");}
   EditorUtility.SetDirty(lift);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Selection.activeGameObject=root;SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleFunicularSetup-report.txt","Two imported textured rail tracks, "+sections+" rail sections. Elevator style carts with no chains and horizontal passenger decks.\nForest station, access steps, castle landing and timber shelters. Parallel carts move in opposite directions at 4m/s. E aboard starts a trip; CharacterController passenger follows cart movement without parenting.\nCart deck collision/level checks passed. Previous south ramp/embankment disabled to clear route; existing elevator preserved.\nBackup: "+backup);Debug.Log("Two track funicular ready. Board either cart and press E to travel.");
  }
  static float Ground(float x,float z){foreach(var terrain in Terrain.activeTerrains){var p=terrain.transform.position;var s=terrain.terrainData.size;if(x>=p.x&&x<=p.x+s.x&&z>=p.z&&z<=p.z+s.z)return p.y+terrain.terrainData.GetInterpolatedHeight((x-p.x)/s.x,(z-p.z)/s.z);}return 10f;}
  static GameObject Cube(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,bool collide=false){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!collide)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
  static Bounds BoundsOf(GameObject go){bool found=false;Bounds b=new Bounds();foreach(var renderer in go.GetComponentsInChildren<Renderer>())if(renderer.enabled){if(!found){b=renderer.bounds;found=true;}else b.Encapsulate(renderer.bounds);}if(!found)throw new InvalidOperationException("Model has no visible mesh.");return b;}
 }
}
#endif

