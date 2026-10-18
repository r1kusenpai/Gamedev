#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class RepairPlayerTreeLods {
 const string Req="Assets/Editor/CQC/RepairPlayerTreeLods.request.txt",Out="C:/Users/Joshua/Documents/Codex/2026-10-11/ca/outputs/tree-lod-repair/";
 static RepairPlayerTreeLods(){EditorApplication.update+=Poll;}
 static void Poll(){if(!File.Exists(Req)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(Req);
 try{var scene=SceneManager.GetSceneByName("MyForest");if(!scene.isLoaded)throw new Exception("Open MyForest to repair its two tree LOD groups.");Directory.CreateDirectory(Out);File.Copy(scene.path,Out+"MyForest-before-tree-lod-repair.unity",true);var report=new System.Text.StringBuilder();int repaired=0,remaining=0;
 foreach(var root in scene.GetRootGameObjects())foreach(var group in root.GetComponentsInChildren<LODGroup>(true)){
  var levels=group.GetLODs();if(!levels.Any(l=>l.renderers.Any(r=>!r)))continue;
  if(!group.name.StartsWith("UNS_Spruce_01")){report.AppendLine("Skipped unrelated LOD group "+group.name);continue;}
  var available=group.GetComponentsInChildren<Renderer>(true).Where(r=>r&&r.GetComponentInParent<LODGroup>()==group).ToArray();
  if(available.Length==0){remaining++;report.AppendLine("FAIL no available renderers: "+group.name);continue;}
  Undo.RecordObject(group,"Repair spruce LOD renderer links");report.AppendLine("Repair "+group.name+" path="+PathOf(group.transform)+" available="+string.Join(",",available.Select(r=>r.name)));
  for(int i=0;i<levels.Length;i++){
   if(!levels[i].renderers.Any(r=>!r))continue;
   var valid=levels[i].renderers.Where(r=>r).ToList();
   var matching=available.Where(r=>r.name.IndexOf("LOD"+i,StringComparison.OrdinalIgnoreCase)>=0||r.name.IndexOf("LOD_"+i,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
   if(matching.Length==0)matching=levels.Where(l=>l.renderers.Any(r=>r)).Select(l=>l.renderers.Where(r=>r).ToArray()).FirstOrDefault()??available;
   foreach(var renderer in matching)if(!valid.Contains(renderer))valid.Add(renderer);
   levels[i].renderers=valid.ToArray();report.AppendLine("LOD "+i+" -> "+string.Join(",",valid.Select(r=>r.name)));
  }
  group.SetLODs(levels);group.RecalculateBounds();PrefabUtility.RecordPrefabInstancePropertyModifications(group);EditorUtility.SetDirty(group);repaired++;
  if(group.GetLODs().Any(l=>l.renderers.Any(r=>!r)))remaining++;
 }
 if(repaired>0){EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Unable to save repaired scene.");}
 report.AppendLine("Repaired tree groups="+repaired+"; unresolved="+remaining);File.WriteAllText(Out+"result.txt",report.ToString());Debug.Log("TREE_LOD_REPAIR: "+repaired+" repaired; "+remaining+" unresolved");
 }catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"error.txt",e.ToString());Debug.LogException(e);}}
 static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
}
#endif
