#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using StarterAssets;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class TestChapelMap
 {
  const string Request="Assets/Editor/CQC/TestChapelMap-v1.request.txt",Key="ChapelMapTest";
  static double next;static ChapelThroneDomain portal;static string playerId;static float since;static bool oldBackground;
  static TestChapelMap(){EditorApplication.update+=Poll;}
  static Transform ExpectedReturn=>SessionState.GetBool(Key+"Back",false)?portal.BackOutsideReturn:portal.OutsideReturn;
  static string Stage{get=>SessionState.GetString(Key,"");set{SessionState.SetString(Key,value);SessionState.SetFloat(Key+"Started",(float)EditorApplication.timeSinceStartup);since=Time.unscaledTime;}}
  static void Poll()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.15;
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isPlaying)return;
   if(!File.Exists(Request))return;
   try
   {
    if(!EditorApplication.isPlaying)
    {
     if(Stage=="Done"){SessionState.EraseString(Key);AssetDatabase.DeleteAsset(Request);return;}
     if(Stage!="")throw new InvalidOperationException("Travel test was interrupted before completion.");
     if(SceneManager.GetActiveScene().name!="MyForest"||!UnityEngine.Object.FindAnyObjectByType<ChapelThroneDomain>())return;
     SessionState.SetBool(Key+"Back",false);oldBackground=Application.runInBackground;SessionState.SetBool(Key+"Background",oldBackground);Application.runInBackground=true;Stage="Starting";EditorApplication.EnterPlaymode();return;
    }
    if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Started",0)>120)throw new TimeoutException("Domain travel test timed out at "+Stage);
    if(!portal)portal=UnityEngine.Object.FindAnyObjectByType<ChapelThroneDomain>();if(!portal||!portal.Player)return;
    if(Stage=="Starting")
    {
     if(Time.time<1)return;playerId=portal.Player.GetEntityId().ToString();SessionState.SetString(Key+"Player",playerId);
     var controller=portal.Player.GetComponent<CharacterController>();controller.enabled=false;portal.Player.SetPositionAndRotation(ExpectedReturn.position,ExpectedReturn.rotation);controller.enabled=true;Physics.SyncTransforms();Stage="Cancel";return;
    }
    if(Stage=="Cancel")
    {
     if(Time.unscaledTime-since<1.3f)return;
     if(!portal.OpenMap()||!portal.MapOpen||Time.timeScale!=0||!DomainInteractionState.IsModal)throw new InvalidOperationException("Door confirmation did not capture input and pause.");
     ScreenCapture.CaptureScreenshot("Logs/ChapelMap-preview.png");Stage="MapPreview";return;
    }
    if(Stage=="MapPreview")
    {
     if(Time.unscaledTime-since<.5f)return;var position=portal.Player.position;portal.CloseMap();if(portal.MapOpen||Time.timeScale==0||DomainInteractionState.IsModal||portal.Player.position!=position)throw new InvalidOperationException("Cancel did not preserve player state.");Stage="BeginEnter";return;
    }
    if(Stage=="BeginEnter")
    {
     if(Time.unscaledTime-since<1.3f)return;if(!portal.OpenMap()||!portal.MapTeleportToThrone())throw new InvalidOperationException("Map throne teleport could not begin.");Stage="Entering";return;
    }
    if(Stage=="Entering")
    {
     if(!string.IsNullOrEmpty(portal.LastError))throw new InvalidOperationException(portal.LastError);
     if(!portal.InDomain||portal.IsTravelling)return;
     if(SceneManager.GetActiveScene().name!="ThroneDomain"||portal.Player.position.x<4990||Time.timeScale==0||DomainInteractionState.IsModal)throw new InvalidOperationException("Domain arrival or input restoration failed.");
     if(portal.Player.GetEntityId().ToString()!=SessionState.GetString(Key+"Player",""))throw new InvalidOperationException("The player was recreated during travel.");
     if(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)!=1)throw new InvalidOperationException("Domain introduced duplicate audio listeners.");Stage="Domain";return;
    }
    if(Stage=="Domain")
    {
     if(Time.unscaledTime-since<1.5f)return;
     if(Camera.main&&Vector3.Distance(Camera.main.transform.position,portal.Player.position)>25)throw new InvalidOperationException("Camera did not follow the player into the domain.");
     if(!portal.OpenMap()||!portal.MapTeleportToCourtyard())throw new InvalidOperationException("Map courtyard teleport could not begin.");Stage="Leaving";return;
    }
    if(Stage=="Leaving")
    {
     if(!string.IsNullOrEmpty(portal.LastError))throw new InvalidOperationException(portal.LastError);
     if(portal.InDomain||portal.IsTravelling)return;
     var room=SceneManager.GetSceneByPath(ChapelThroneDomain.ScenePath);if(room.isLoaded||SceneManager.GetActiveScene().name!="MyForest")throw new InvalidOperationException("Domain was not unloaded or outside scene restored.");
     if(Vector3.Distance(portal.Player.position,ExpectedReturn.position)>.4f||!portal.Player.GetComponent<CharacterController>().enabled||!portal.Player.GetComponent<ThirdPersonController>().enabled||!portal.Player.GetComponent<WeaponController>().enabled||DomainInteractionState.IsModal||Time.timeScale==0)throw new InvalidOperationException("Return position, controller or input was not restored.");
     Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ChapelMap-playtest.txt","PASS: MAP open and close, input and time restored on cancel, map teleport to enlarged Throne Hall, map teleport back to courtyard. Actual Play-mode test of door confirmation, cancel without movement, entering the throne scene additively, arrival, camera following, same player identity, one AudioListener, return confirmation, courtyard return, domain unload and movement/weapon/input/time restoration. Existing town remains loaded.\n");
     Application.runInBackground=SessionState.GetBool(Key+"Background",false);Stage="Done";EditorApplication.ExitPlaymode();
    }
   }
   catch(Exception e)
   {
    Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ChapelMap-playtest-error.txt",e.ToString());Debug.LogException(e);SessionState.EraseString(Key);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");Application.runInBackground=SessionState.GetBool(Key+"Background",false);if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
   }
  }
 }
}
#endif
