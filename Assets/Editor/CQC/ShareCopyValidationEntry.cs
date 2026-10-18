#if UNITY_EDITOR
using System.IO;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
public static class ShareCopyValidationEntry {
 public static void Run(){
  Directory.CreateDirectory("C:/Users/Joshua/Documents/Codex/2026-10-11/ca/outputs/verified-share-copy/playtest");
  SessionState.EraseString("ShareCopyGameCheck");
  EditorSceneManager.OpenScene("Assets/Scenes/MyForest.unity",OpenSceneMode.Single);
  File.WriteAllText("Assets/Editor/CQC/PreUploadGameCheck.request.txt","Verify clean sharing copy");
 }
}
#endif
