#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;using StarterAssets;using QueenBattle;
[InitializeOnLoad]public static class PreUploadGameCheck {
 const string Req="Assets/Editor/CQC/PreUploadGameCheck.request.txt",Out="C:/Users/Joshua/Documents/Codex/2026-10-11/ca/outputs/verified-share-copy/playtest/",Key="ShareCopyGameCheck";
 static double next;static ChapelThroneDomain portal;static readonly List<string> messages=new List<string>();static readonly List<string> runtimeErrors=new List<string>();
 static string Stage{get=>SessionState.GetString(Key,"");set{SessionState.SetString(Key,value);SessionState.SetFloat(Key+"Time",(float)EditorApplication.timeSinceStartup);}}
 static double Age=>EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Time",0);
 static PreUploadGameCheck(){EditorApplication.update+=Poll;Application.logMessageReceived+=Log;}
 static void Log(string m,string stack,LogType t){if(File.Exists(Req)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))runtimeErrors.Add(m+"\n"+stack);}
 static void Check(bool ok,string text){messages.Add((ok?"PASS: ":"FAIL: ")+text);Save();if(!ok)throw new Exception(text);}
 static void Save(){Directory.CreateDirectory(Out);File.WriteAllLines(Out+"unity-check.txt",messages.Concat(runtimeErrors.Select(x=>"RUNTIME ERROR: "+x)));}
 static void Scan(Scene scene){int missingScripts=0,missingMeshes=0,missingMaterials=0,missingObjects=0;var details=new List<string>();
 foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)){
 missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
 foreach(var component in t.GetComponents<Component>()){if(!component)continue;try{var so=new SerializedObject(component);var it=so.GetIterator();while(it.NextVisible(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue==null&&!it.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId))){missingObjects++;if(details.Count<40)details.Add(t.name+" / "+component.GetType().Name+" / "+it.propertyPath);}}catch{}}
 if(!t.gameObject.activeInHierarchy)continue;
 var mesh=t.GetComponent<MeshFilter>();var renderer=t.GetComponent<Renderer>();if(renderer&&renderer.enabled){if(mesh&&!mesh.sharedMesh)missingMeshes++;var skin=renderer as SkinnedMeshRenderer;if(skin&&!skin.sharedMesh)missingMeshes++;foreach(var mat in renderer.sharedMaterials)if(!mat||!mat.shader||mat.shader.name=="Hidden/InternalErrorShader")missingMaterials++;}
 }
 messages.Add(scene.name+": missing scripts="+missingScripts+", meshes="+missingMeshes+", materials/shaders="+missingMaterials+", serialized references="+missingObjects);messages.AddRange(details);Save();messages.Add((missingScripts+missingMeshes+missingMaterials+missingObjects==0?"PASS: ":"FAIL: ")+scene.name+" scene assets resolve");Save();}
 static void BeginTravel(int selection,string nextStage){Check(portal.OpenMap(),"Open destination selector");typeof(ChapelThroneDomain).GetField("mapSelection",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(portal,selection);Check(portal.MapTeleportSelected(),"Begin teleport selection "+selection);Stage=nextStage;}
 static void Poll(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.2;if(!File.Exists(Req)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
 try{
 if(Stage==""&&EditorApplication.isPlaying){Stage="AwaitStop";EditorApplication.ExitPlaymode();return;}
 if(Stage=="AwaitStop"){if(EditorApplication.isPlayingOrWillChangePlaymode)return;Stage="";}
 if(Stage!=""&&messages.Count==0&&File.Exists(Out+"unity-check.txt"))messages.AddRange(File.ReadAllLines(Out+"unity-check.txt"));
 if(Stage==""){
  Directory.CreateDirectory(Out);messages.Clear();runtimeErrors.Clear();messages.Add("Pre-upload validation. Saved and currently open scene data checked; no scene saves or asset edits.");
  Check(!EditorApplication.isPlayingOrWillChangePlaymode,"Start checks outside Play mode");
  var main=SceneManager.GetSceneByName("MyForest");Check(main.isLoaded,"MyForest is open");Scan(main);
  var throne=EditorSceneManager.OpenPreviewScene(ChapelThroneDomain.ScenePath);try{Scan(throne);}finally{EditorSceneManager.ClosePreviewScene(throne);}
  var set=Resources.Load<SwordAnimationSet>("WeaponAnimations/DownloadedSword/DownloadedSword");Check(set&&set.IsReady,"Sword animation set complete");
  var sky=RenderSettings.skybox;messages.Add("Active skybox shader: "+(sky&&sky.shader?sky.shader.name:"none"));Save();
  var settings=Resources.Load<WildernessCombat.WildernessEnemySettings>("WildernessEnemies/Settings");Check(settings&&settings.Ready,"Wilderness models and basic animations available");
  Check(Resources.LoadAll<AnimationClip>("WeaponAnimations/PlayerHit2").Any(c=>c.isHumanMotion),"Player hit animation available");
  SessionState.SetBool(Key+"Background",Application.runInBackground);Application.runInBackground=true;Stage="Starting";EditorApplication.EnterPlaymode();return;
 }
 if(Stage=="Done"){if(!EditorApplication.isPlaying){File.Delete(Req);SessionState.EraseString(Key);if(Application.isBatchMode)EditorApplication.Exit(messages.Any(x=>x.StartsWith("FAIL:")||x.StartsWith("RUNTIME ERROR:"))?1:0);}return;}
 if(!EditorApplication.isPlaying)return;
 if(Age>90)throw new TimeoutException(Stage);
 if(!portal)portal=UnityEngine.Object.FindAnyObjectByType<ChapelThroneDomain>();if(!portal||!portal.Player)return;
 if(Stage=="Starting"){
  if(Time.time<3)return;Check(portal.Player.GetComponent<Animator>().avatar.isValid,"Player humanoid avatar valid");
  ScreenCapture.CaptureScreenshot(Out+"forest.png");
  Check(UnityEngine.Object.FindObjectsByType<WildernessCombat.WildernessEnemy>(FindObjectsSortMode.None).Length>0,"Wilderness enemies spawned");
  var w=portal.Player.GetComponent<WeaponController>();Check(w&&w.enabled,"Weapon animation graph initialized");w.PlayDamageReaction(portal.Player.position+portal.Player.forward);Stage="Hit";return;
 }
 if(Stage=="Hit"){
  if(Age<.16)return;foreach(var skin in portal.Player.GetComponentsInChildren<SkinnedMeshRenderer>()){var mesh=new Mesh();skin.BakeMesh(mesh);Check(!float.IsNaN(mesh.bounds.size.magnitude)&&mesh.bounds.size.magnitude<10,"Player mesh remains valid during hit: "+skin.name);UnityEngine.Object.DestroyImmediate(mesh);}Stage="MoveCircle";return;
 }
 if(Stage=="MoveCircle"){if(Age<1)return;BeginTravel(2,"Circle");return;}
 if(Stage=="Circle"){if(portal.IsTravelling)return;Check(string.IsNullOrEmpty(portal.LastError),"Outside circle arrival");Check(Vector3.Distance(portal.Player.position,portal.CastleCircleTeleport.position)<1,"Outside circle landing point");Stage="MoveFloor";return;}
 if(Stage=="MoveFloor"){if(Age<1.2)return;BeginTravel(3,"Floor");return;}
 if(Stage=="Floor"){if(portal.IsTravelling)return;Check(string.IsNullOrEmpty(portal.LastError),"First-floor arrival");Check(Vector3.Distance(portal.Player.position,portal.CastleFirstFloorTeleport.position)<1,"First floor landing near cart");Stage="MoveWild";return;}
 if(Stage=="MoveWild"){if(Age<1.2)return;BeginTravel(4,"Wild");return;}
 if(Stage=="Wild"){if(portal.IsTravelling)return;Check(string.IsNullOrEmpty(portal.LastError),"Wilderness arrival");Stage="MoveThrone";return;}
 if(Stage=="MoveThrone"){if(Age<1.2)return;BeginTravel(0,"Throne");return;}
 if(Stage=="Throne"){
  if(portal.IsTravelling)return;Check(portal.InDomain&&string.IsNullOrEmpty(portal.LastError),"Throne hall scene loaded and player arrived");
  var boss=UnityEngine.Object.FindAnyObjectByType<QueenBossBattle>();Check(boss&&boss.Actor,"Queen model spawned");ScreenCapture.CaptureScreenshot(Out+"throne.png");boss.StartBattle();Check(boss.Fighting,"Queen battle starts");Stage="Boss";return;
 }
 if(Stage=="Boss"){
  if(Age<2)return;var boss=UnityEngine.Object.FindAnyObjectByType<QueenBossBattle>();boss.ReceivePlayerDamage(1);Check(boss.PlayerHealth<boss.PlayerMaxHealth,"Boss damage reaches player");boss.AbortBattle();Stage="Return";return;
 }
 if(Stage=="Return"){if(Age<1)return;BeginTravel(1,"Courtyard");return;}
 if(Stage=="Courtyard"){
  if(portal.IsTravelling)return;Check(!portal.InDomain&&string.IsNullOrEmpty(portal.LastError),"Returned outside and unloaded domain");Check(!DomainInteractionState.IsModal&&Time.timeScale>0,"Input and time restored");Check(runtimeErrors.Count==0,"No runtime errors during tested sequence");
  Save();Application.runInBackground=SessionState.GetBool(Key+"Background",false);Stage="Done";EditorApplication.ExitPlaymode();return;
 }
 }catch(Exception e){messages.Add("STOPPED: "+e);Save();Application.runInBackground=SessionState.GetBool(Key+"Background",false);File.Delete(Req);SessionState.EraseString(Key);if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(1);}}
}
#endif



