#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace StarterAssets.CQCEditorTools
{
    public static class CQCSetupMenu
    {
        [MenuItem("Tools/CQC/Set Up Player And Spawn Bot")]
        private static void SetUp()
        {
            if (EditorApplication.isPlaying)
            { EditorUtility.DisplayDialog("CQC Setup", "Stop Play mode first, then run this setup again.", "OK"); return; }
            GameObject player = Selection.activeGameObject;
            if (!player || !player.GetComponent<ThirdPersonController>()) player = GameObject.Find("PlayerArmature");
            if (!player || !player.GetComponent<ThirdPersonController>())
            { EditorUtility.DisplayDialog("CQC Setup", "Select your PlayerArmature in the Hierarchy first.", "OK"); return; }
            Undo.RecordObject(player, "Set Up CQC Player");
            var combat = player.GetComponent<CQCCombat>();
            if (!combat) combat = Undo.AddComponent<CQCCombat>(player);
            var posture = player.GetComponent<CQCPosture>();
            if (!posture) posture = Undo.AddComponent<CQCPosture>(player);
            if (!player.GetComponent<CQCCrouchClips>()) Undo.AddComponent<CQCCrouchClips>(player);
            if (!player.GetComponent<CQCSprintPhase>()) Undo.AddComponent<CQCSprintPhase>(player);
            var spawner = player.GetComponent<CQCPracticeBotSpawner>();
            if (!spawner) spawner = Undo.AddComponent<CQCPracticeBotSpawner>(player);
            Undo.RecordObjects(new Object[] { combat, posture, spawner }, "Enable CQC Components");
            combat.enabled = posture.enabled = spawner.enabled = true;
            spawner.SpawnOnStart = true;
            spawner.ShowControls = false;
            spawner.BotModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab");
            // Keep a previously created scene bot; B can reset it during Play.
            GameObject existing = GameObject.Find("CQC Practice Bot");
            if (existing) { Undo.DestroyObjectImmediate(existing); existing = null; }
            if (!existing)
            {
                spawner.SpawnBot();
                if (spawner.SpawnedBot) Undo.RegisterCreatedObjectUndo(spawner.SpawnedBot, "Spawn CQC Bot");
            }
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(combat);
            EditorUtility.SetDirty(posture);
            EditorUtility.SetDirty(spawner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(combat);
            PrefabUtility.RecordPrefabInstancePropertyModifications(posture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
            EditorSceneManager.MarkSceneDirty(player.scene);
            Selection.activeGameObject = player;
            EditorUtility.DisplayDialog("CQC Setup", "CQC controls are attached to PlayerArmature. Save the scene, press Play, and click inside Game. Use C to toggle standing and crouching. The practice bot appears nearby if there is a clear space.", "OK");
        }
    }
}
#endif
