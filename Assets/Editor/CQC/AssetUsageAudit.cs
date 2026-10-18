#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class AssetUsageAudit
 {
  const string Request="Assets/Editor/CQC/AssetUsageAudit.request.txt";static double next;
  static AssetUsageAudit(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;try{Audit();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AssetUsageAudit-error.txt",e.ToString());AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/Project/Report Asset Usage Without Changes")]
  public static void Audit()
  {
   var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&File.Exists(p)&&!p.Contains("/Backups/")).ToArray();
   var scene=new HashSet<string>(AssetDatabase.GetDependencies("Assets/Scenes/MyForest.unity",true));
   var resources=new HashSet<string>(AssetDatabase.GetDependencies(all.Where(p=>p.Contains("/Resources/")).ToArray(),true));
   var builds=new HashSet<string>(AssetDatabase.GetDependencies(EditorBuildSettings.scenes.Where(s=>s.enabled&&File.Exists(s.path)).Select(s=>s.path).ToArray(),true));
   var settings=new HashSet<string>(AssetDatabase.GetDependencies(Directory.GetFiles("ProjectSettings").Select(p=>p.Replace('\\','/')).ToArray(),true));
   var lines=new List<string>{"Asset,Bytes,Status"};long used=0,unused=0;
   foreach(var path in all.OrderBy(p=>p)){long size=new FileInfo(path).Length;string status=scene.Contains(path)?"Current scene dependency":resources.Contains(path)?"Resources runtime asset":builds.Contains(path)?"Other enabled build scene":settings.Contains(path)?"Project setting dependency":path.EndsWith(".cs")?"Script - retain":path.Contains("/Editor/")?"Editor asset - review":"Unreferenced candidate - review dynamic loading before deletion";if(status.StartsWith("Unreferenced"))unused+=size;else used+=size;lines.Add("\""+path.Replace("\"","\"\"")+"\","+size+",\""+status+"\"");}
   Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/AssetUsageAudit.csv",lines);File.WriteAllText("Logs/AssetUsageAudit-summary.txt","Current-scene dependency paths: "+scene.Count+". Retained-by-reference/resources/build/settings/scripts: "+used+" bytes. Unreferenced candidates: "+unused+" bytes. This is a read-only audit; no scenes, materials, prefabs or models were changed. Dynamic loading and editor import source files require review before deletion.");
  }
 }
}
#endif
