#if UNITY_EDITOR
using System;using System.IO;using UnityEditor;using UnityEngine;
namespace WildernessCombat.Editor {
[InitializeOnLoad]public static class CorrectCyclopsSize {
 const string Request="Assets/Editor/CQC/CorrectCyclopsSize.request.txt",Report="C:/Users/Joshua/Documents/Codex/2026-10-11/ca/outputs/wilderness-enemies/cyclops-size-check.txt";
 static CorrectCyclopsSize(){EditorApplication.update+=Poll;}
 static void Poll(){if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete(Request);try{Run();}catch(Exception e){File.WriteAllText(Report,e.ToString());Debug.LogException(e);}}
 public static void Run(){var settings=Resources.Load<WildernessEnemySettings>("WildernessEnemies/Settings");if(!settings||!settings.Cyclops||!settings.CyclopsClub)throw new Exception("Cyclops resources missing");settings.CyclopsHeight=3f;settings.CyclopsClubLength=1.65f;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
 var root=new GameObject("Temporary size validation");try{var visual=UnityEngine.Object.Instantiate(settings.Cyclops,root.transform);CyclopsSizing.FitHeight(visual,settings.CyclopsHeight);var body=CyclopsSizing.GeometryBounds(visual);var hand=visual.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand);var club=CyclopsSizing.AttachClub(settings,visual.transform,hand);float span=CyclopsSizing.LongestAxisLength(club);
 if(Mathf.Abs(body.size.y-3f)>.02f||Mathf.Abs(span-1.65f)>.02f)throw new Exception("Cyclops or club size validation failed");string report="Measured neutral body height: "+body.size.y.ToString("F3")+"m\nMeasured held club length: "+span.ToString("F3")+"m\nBoth measurements include imported hierarchy and hand scale.\n";
 int replaced=0;if(EditorApplication.isPlaying)foreach(var world in UnityEngine.Object.FindObjectsByType<WildernessEnemyWorld>(FindObjectsSortMode.None))replaced+=world.RebuildCyclopes();report+="Cleared and replaced live cyclopes: "+replaced+". On next Play, every pack uses the corrected sizes.\n";File.WriteAllText(Report,report);Debug.Log("CYCLOPS_SIZE_FIXED\n"+report);
 }finally{UnityEngine.Object.DestroyImmediate(root);}}
}
}
#endif
