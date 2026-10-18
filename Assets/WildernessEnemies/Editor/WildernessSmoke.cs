#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace WildernessCombat.Editor {
[InitializeOnLoad] public static class WildernessSmoke {
 static double start;static int stage;static WildernessEnemyWorld world;static WildernessEnemy target;static float health;static Vector3 initial;static string report="";
 static WildernessSmoke(){EditorApplication.update+=Tick;}
 public static void Run(){SessionState.SetBool("WildSmoke",true);EditorSceneManager.OpenScene("Assets/Scenes/MyForest.unity");EditorApplication.isPlaying=true;}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);report+="PASS: "+text+"\n";}
 static void Tick(){if(!SessionState.GetBool("WildSmoke",false)||!EditorApplication.isPlaying)return;try{
 if(stage==0){world=UnityEngine.Object.FindFirstObjectByType<WildernessEnemyWorld>();if(!world||world.Enemies.Count==0)return;start=EditorApplication.timeSinceStartup;Check(world.Enemies.Count>=8,"Outside enemies spawned: "+world.Enemies.Count);Check(world.Enemies.All(e=>world.IsWild(e.transform.position)),"All enemies outside city zones");Check(world.Enemies.Any(e=>e.Mage)&&world.Enemies.Any(e=>!e.Mage),"Magic and sword roles spawned");world.Settings=UnityEngine.Object.Instantiate(world.Settings);target=world.Enemies.First(e=>!e.Mage);var cc=world.Player.GetComponent<CharacterController>();cc.enabled=false;world.Player.position=target.transform.position+target.transform.forward*2;cc.enabled=true;initial=target.transform.position;health=world.Player.GetComponent<WildernessPlayerHealth>().Health;stage=1;}
 else if(stage==1&&EditorApplication.timeSinceStartup-start>6){Check(Vector3.Distance(initial,target.transform.position)>.05f,"Enemy moves during pursuit");Check(world.Player.GetComponent<WildernessPlayerHealth>().Health<health,"Basic attack damaged player");float before=target.Health;target.ReceiveDamage(20,world.Player);Check(target.Health==before-20,"Player weapon damage interface triggers hit reaction");target.ReceiveDamage(1000,world.Player);Check(!target.IsDamageable&&!target.GetComponent<CharacterController>().enabled,"Enemy dies and stops collision");Check(world.Settings.CitySafeZones.All(b=>!world.IsWild(b.center)),"City interiors protected");File.WriteAllText("C:/Users/Joshua/Documents/Codex/2026-10-11/ca/outputs/wilderness-enemies/runtime-test.txt",report);SessionState.SetBool("WildSmoke",false);Debug.Log("WILDERNESS_SMOKE_PASS\n"+report);EditorApplication.Exit(0);}
 }catch(Exception e){SessionState.SetBool("WildSmoke",false);File.WriteAllText("C:/Users/Joshua/Documents/Codex/2026-10-11/ca/outputs/wilderness-enemies/runtime-test.txt",report+e);Debug.LogException(e);EditorApplication.Exit(1);}}
}}
#endif
