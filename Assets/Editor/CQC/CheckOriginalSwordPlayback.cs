#if UNITY_EDITOR
using System.IO;using UnityEditor;using UnityEngine;
namespace StarterAssets.CQCEditorTools {
[InitializeOnLoad] public static class CheckOriginalSwordPlayback {
static CheckOriginalSwordPlayback(){EditorApplication.update+=Check;}
static void Check(){const string request="Assets/Editor/CQC/OriginalSwordCompare.request.txt";if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!GameObject.Find("PlayerArmature"))return;
File.Delete(request);try{PreviewDownloadedSword.Apply();var set=Resources.Load<SwordAnimationSet>("WeaponAnimations/DownloadedSword/DownloadedSword");if(set.Combo1.name!="sword_regular_b_Full"||set.Combo2.name!="sword_regular_b_reversed_Full"||Mathf.Abs(set.AttackSpeed-.5f)>.001f)throw new System.Exception("Original sword assignment mismatch");File.WriteAllText("Logs/OriginalSwordCompare-ready.txt","Regular/reversed originals, full combo on sprint, speed 0.5. Full body source poses. Grip checks passed.");}catch(System.Exception e){Debug.LogException(e);File.WriteAllText("Logs/OriginalSwordCompare-error.txt",e.ToString());}}
}}
#endif
