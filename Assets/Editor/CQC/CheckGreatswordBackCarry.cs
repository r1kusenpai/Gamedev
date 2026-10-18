#if UNITY_EDITOR
using System;using System.IO;using System.Reflection;using UnityEditor;using UnityEngine;using StarterAssets;
[InitializeOnLoad]public static class CheckGreatswordBackCarry {
 const string Req="Assets/Editor/CQC/CheckGreatswordBackCarry.request.txt",Out="C:/Users/Joshua/Documents/Codex/2026-10-11/ca/outputs/greatsword-back-carry.txt";const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static CheckGreatswordBackCarry(){EditorApplication.update+=Poll;}
 static void Poll(){if(!File.Exists(Req)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!GameObject.Find("PlayerArmature"))return;File.Delete(Req);try{Run();}catch(Exception e){File.WriteAllText(Out,e.ToString());Debug.LogException(e);}}
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 public static void Run(){var clone=UnityEngine.Object.Instantiate(GameObject.Find("PlayerArmature"));foreach(var b in clone.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;var template=clone.GetComponent<WeaponController>().GreatswordModelPrefab;var blade=template?UnityEngine.Object.Instantiate(template):new GameObject("Temporary back carry blade");try{
 var w=clone.GetComponent<WeaponController>();var a=clone.GetComponent<Animator>();a.Rebind();var motor=clone.GetComponent<ThirdPersonController>();var input=clone.GetComponent<StarterAssetsInputs>();Set(w,"animator",a);Set(w,"movement",motor);Set(w,"input",input);Set(w,"animationSource",null);Set(w,"posture",null);Set(w,"great",blade);w.CurrentWeapon=WeaponController.Weapon.Greatsword;w.GreatswordBackCarryWhileRunning=true;w.GreatswordUsesSwordAttacks=true;w.SwordUseNormalLocomotion=true;motor.Grounded=true;input.move=Vector2.up;input.sprint=true;Set(w,"actionTime",0f);Set(w,"pending",false);Set(w,"charging",false);Set(w,"greatswordBackWeight",1f);
 var wants=typeof(WeaponController).GetProperty("WantsGreatswordBackCarry",F);if(!(bool)wants.GetValue(w))throw new Exception("Sprint carry not enabled");typeof(WeaponController).GetMethod("UpdateGreatswordBackCarry",F).Invoke(w,null);var hand=a.GetBoneTransform(HumanBodyBones.RightHand);var chest=a.GetBoneTransform(HumanBodyBones.UpperChest);if(!chest)chest=a.GetBoneTransform(HumanBodyBones.Chest);if(Vector3.Dot(blade.transform.position-chest.position,clone.transform.forward)>-.10f)throw new Exception("Weapon is not against back");if(Vector3.Dot(blade.transform.up,clone.transform.up)>-.7f)throw new Exception("Blade is not pointing down back");
 if(Vector3.Distance(blade.transform.position,hand.TransformPoint((Vector3)typeof(WeaponController).GetField("stableSwordGripPosition",F).GetValue(w)+w.GreatswordGripOffset))>.01f)throw new Exception("Hand is not holding the handle");if(hand.position.y<chest.position.y)throw new Exception("Hand not raised to back handle");Quaternion settled=blade.transform.rotation;Vector3 settledPosition=blade.transform.position;for(int i=0;i<40;i++){hand.rotation=Quaternion.AngleAxis((i%2==0?65:-65),clone.transform.up)*hand.rotation;typeof(WeaponController).GetMethod("UpdateGreatswordBackCarry",F).Invoke(w,null);if(Quaternion.Angle(settled,blade.transform.rotation)>.1f||Vector3.Distance(settledPosition,blade.transform.position)>.005f)throw new Exception("Running wrist swing changed carry alignment");}
 blade.transform.position=new Vector3(1,2,3);Set(w,"actionTime",1f);typeof(WeaponController).GetMethod("UpdateGreatswordBackCarry",F).Invoke(w,null);if((float)typeof(WeaponController).GetField("greatswordBackWeight",F).GetValue(w)!=0||blade.transform.position!=new Vector3(1,2,3))throw new Exception("Attack did not release back carry immediately");Set(w,"actionTime",0f);input.sprint=false;if((bool)wants.GetValue(w))throw new Exception("Walking incorrectly triggers sprint back carry");input.sprint=true;motor.Grounded=false;if((bool)wants.GetValue(w))throw new Exception("Airborne carry should release");File.WriteAllText(Out,"PASS: Sprint shoulder carry; diagonal back mount close to torso, right hand holds the back handle; immediate attack hand-grip release; walking and airborne exclusions; 40 wrist-twist samples retain stable blade alignment.\n");Debug.Log("GREATSWORD_BACK_CARRY_PASSED");
 }finally{UnityEngine.Object.DestroyImmediate(blade);UnityEngine.Object.DestroyImmediate(clone);}}
}
#endif









