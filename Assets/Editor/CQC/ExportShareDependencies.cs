#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;
[InitializeOnLoad]public static class ExportShareDependencies {
 const string Req="Assets/Editor/CQC/ExportShareDependencies.request.txt",Out="C:/Users/Joshua/Documents/Codex/2026-10-11/ca/outputs/verified-share-copy/";
 static ExportShareDependencies(){EditorApplication.update+=Poll;}
 public static void Run(){File.WriteAllText(Req,"Export only");Poll();EditorApplication.Exit(File.Exists(Out+"unity-required-assets.txt")?0:1);}
 static void Poll(){if(!File.Exists(Req)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete(Req);try{
 Directory.CreateDirectory(Out);var all=AssetDatabase.GetAllAssetPaths();var roots=new HashSet<string>(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path));
 foreach(var path in all)if(path.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(path)){
  var ext=Path.GetExtension(path).ToLowerInvariant();
  if(path.Contains("/Resources/")||path.Contains("/StreamingAssets/")||new[]{".cs",".asmdef",".asmref",".shader",".compute",".hlsl",".cginc"}.Contains(ext))roots.Add(path);
 }
 var guidRegex=new System.Text.RegularExpressions.Regex("guid:\\s*([a-fA-F0-9]{32})");foreach(var file in Directory.GetFiles("ProjectSettings"))foreach(System.Text.RegularExpressions.Match match in guidRegex.Matches(File.ReadAllText(file))){var path=AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);if(path.StartsWith("Assets/"))roots.Add(path);}
 foreach(var path in all.Where(p=>p.StartsWith("Assets/")&&!p.Contains("/Editor/")&&p.EndsWith(".cs")))foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path),"\"(Assets/[^\"\\r\\n]+)\"")){var target=match.Groups[1].Value;if(File.Exists(target))roots.Add(target);}
 var dependencies=AssetDatabase.GetDependencies(roots.ToArray(),true).Where(p=>p.StartsWith("Assets/")).Distinct().OrderBy(p=>p).ToArray();File.WriteAllLines(Out+"unity-required-assets.txt",dependencies);File.WriteAllLines(Out+"dependency-roots.txt",roots.OrderBy(p=>p));File.WriteAllText(Out+"export-result.txt","Unity recursive dependency export: "+dependencies.Length+" assets; "+roots.Count+" roots. Original assets unchanged.\n");Debug.Log("SHARE_DEPENDENCIES_EXPORTED: "+dependencies.Length);
 }catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"export-error.txt",e.ToString());Debug.LogException(e);}}
}
#endif

