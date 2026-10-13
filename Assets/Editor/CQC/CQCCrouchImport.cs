#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class CQCCrouchImport
    {
        private const string Folder = "Assets/Resources/CQCAnimations/";
        static CQCCrouchImport()
        {
            EditorApplication.delayCall += PrepareWhenReady;
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += PrepareWhenReady; };
        }

        private static void PrepareWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += PrepareWhenReady; return; }
            if (!File.Exists(Folder + "Crouched Walking.fbx")) return;
            try { Prepare(false); }
            catch (Exception ex) { Debug.LogError("Crouch import failed: " + ex.Message); }
        }

        [MenuItem("Tools/CQC/Prepare Crouch Animations")]
        public static void PrepareFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorUtility.DisplayDialog("Crouch animations", "Stop Play first.", "OK"); return; }
            Prepare(true);
        }

        [MenuItem("Tools/CQC/Prepare All Animations")]
        public static void PrepareAllFromMenu() { PrepareFromMenu(); }

        private static void Prepare(bool force)
        {
            string[] names = { "Crouched Walking", "Standing To Crouched", "Crouch To Stand", "Flying Knee Punch Combo",
                "Taken Hostage - Villain", "Taken Hostage - Victim", "Hostage Situation Idle - Villain",
                "Hostage Situation Idle - Hostage", "Release Hostage - Villain", "Release Hostage - Hostage",
                "Stunned", "Dying Backwards", "Dying", "Punching", "Punch Combo", "Combo Punch", "Roundhouse Kick", "Mma Kick", "Getting Hit", "Sprint", "Crouched To Sprinting", "Hook Punch", "Stand To Roll", "Shoulder Throw, Victim" };
            foreach (string name in names)
            {
                string path = Folder + name + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) { Debug.LogWarning("Waiting for Unity to import " + path); continue; }
                bool groundAttack = name == "Punching" || name == "Punch Combo" || name == "Combo Punch" || name == "Roundhouse Kick" || name == "Mma Kick" || name == "Hook Punch" || name == "Stand To Roll";
                bool shoulderThrow = name == "Shoulder Throw, Victim";
                string revision = shoulderThrow ? "CQC-Shoulder-RootMotion-v3" : name == "Flying Knee Punch Combo" ? "CQC-Air-RootMotion-v2" : groundAttack ? "CQC-Ground-RootMotion-v1" : "CQC-Humanoid-v1";
                if (!force && importer.userData == revision) continue;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.importAnimation = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = name;
                    clip.loopTime = name == "Crouched Walking" || name == "Sprint" || name == "Stunned" || name.StartsWith("Hostage Situation Idle");
                    clip.loopPose = clip.loopTime;
                    clip.lockRootRotation = true;
                    clip.keepOriginalOrientation = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalPositionXZ = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionY = false;
                    clip.heightFromFeet = true;
                    if (shoulderThrow)
                    {
                        // The source has a long guard lead-in and several seconds lying down.
                        // Keep the lead into the actual throw, flip, and first settled ground pose.
                        clip.firstFrame = 60f;
                        clip.lastFrame = Mathf.Min(228f, clip.lastFrame);
                        clip.keepOriginalPositionY = true;
                        clip.heightFromFeet = false;
                    }
                    if (name == "Flying Knee Punch Combo")
                    {
                        clip.lockRootPositionXZ = false;
                        clip.keepOriginalPositionXZ = false;
                        clip.lockRootHeightY = false;
                        clip.keepOriginalPositionY = false;
                    }
                    else if (shoulderThrow || groundAttack || name == "Sprint" || name == "Crouched To Sprinting")
                    {
                        clip.lockRootPositionXZ = false;
                        clip.keepOriginalPositionXZ = false;
                    }
                }
                importer.clipAnimations = clips;
                importer.userData = revision;
                importer.SaveAndReimport();
            }
            bool valid = true;
            string report = "CQC imported crouch clips\n";
            foreach (string name in names)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Folder + name + ".fbx").OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                var avatar = AssetDatabase.LoadAllAssetsAtPath(Folder + name + ".fbx").OfType<Avatar>().FirstOrDefault();
                bool ok = clip && clip.isHumanMotion && clip.length > 0f && avatar && avatar.isValid && avatar.isHuman;
                valid &= ok;
                report += name + ": " + (ok ? "Humanoid, " + clip.length.ToString("F2") + " seconds" : "NOT READY") + "\n";
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "CQC-Crouch-Import.txt"), report);
            if (!valid) Debug.LogError(report);
            else if (force) EditorUtility.DisplayDialog("CQC animations ready", report + "Press Play to test movement, attacks, hostage actions, and bot reactions.", "OK");
        }
    }
}
#endif
