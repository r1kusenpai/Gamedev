#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class ApplyRecommendedSword
    {
        const string Request = "Assets/Editor/CQC/ApplyRecommendedSword-v1.request.txt";
        const string Source = "Assets/DoubleL/FBX Unity/One Hand Up/";
        const string Folder = "Assets/Resources/WeaponAnimations/DoubleLSword";
        static double next;
        static ApplyRecommendedSword() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 2;
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "MyForest") return;
            try { Apply(); AssetDatabase.DeleteAsset(Request); }
            catch (Exception e)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/RecommendedSword-error.txt", e.ToString()); Debug.LogException(e);
                AssetDatabase.MoveAsset(Request, Request + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".failed");
            }
        }

        [MenuItem("Tools/Weapons/Apply Recommended One Handed Sword")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before applying sword animations.");
            var player = GameObject.Find("PlayerArmature");
            var weapon = player ? player.GetComponent<WeaponController>() : null;
            var animator = player ? player.GetComponent<Animator>() : null;
            if (!weapon || !animator || !animator.isHuman || !animator.avatar || !animator.avatar.isValid)
                throw new InvalidOperationException("The existing player's WeaponController and valid Humanoid avatar are required.");
            var scene = SceneManager.GetActiveScene();
            string backup = "Assets/ForestExpansion/Backups/MyForest-before-DoubleL-sword-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Sword scene backup failed.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources/WeaponAnimations", "DoubleLSword");
            var report = new StringBuilder("Recommended DoubleL sword animation setup\n");
            var set = ScriptableObject.CreateInstance<SwordAnimationSet>();
            try
            {
                set.Combo1 = Copy("Combo1", "Attack_A/InPlace/1Hand_Up_Attack_A_1_InPlace.fbx", false, report);
                set.Combo2 = Copy("Combo2", "Attack_A/InPlace/1Hand_Up_Attack_A_2_InPlace.fbx", false, report);
                set.Combo3 = Copy("Combo3", "Attack_A/InPlace/1Hand_Up_Attack_A_3_InPlace.fbx", false, report);
                set.ChargedAttack = Copy("ChargedAttack", "Attack_B/InPlace/1Hand_Up_Attack_B_3_InPlace.fbx", false, report);
                set.RunningAttack = Copy("RunningAttack", "Attack_B/InPlace/1Hand_Up_Attack_B_1_InPlace.fbx", false, report);
                set.AirAttack = Copy("AirAttack", "Attack_B/InPlace/1Hand_Up_Attack_B_2_InPlace.fbx", false, report);
                set.Idle = Copy("Idle", "Movement/Idle/Idle/1Hand_Up_Stand_Idle_A_2.fbx", true, report);
                set.Walk = Copy("Walk", "Movement/Walk/Base/InPlace/1Hand_Up_Walk_A_F_InPlace.fbx", true, report);
                set.Run = Copy("Run", "Movement/Run/Base/InPlace/1Hand_Up_Run_A_F_InPlace.fbx", true, report);
                set.Jump = Copy("Jump", "Jump/InPlace/1Hand_Up_Jump_B_InPlace.fbx", false, report);
                set.CrouchIdle = Copy("CrouchIdle", "Movement/Crouch/Idle/Idle/1Hand_Up_Crouch_Idle_1.fbx", true, report);
                set.CrouchWalk = Copy("CrouchWalk", "Movement/Crouch/Base/InPlace/1Hand_Up_Crouch_F_InPlace.fbx", true, report);
                if (!set.IsReady) throw new InvalidOperationException("Sword animation set is incomplete.");
                VerifyRetargeting(player, set, report);
                string setPath = Folder + "/RecommendedSword.asset";
                var saved = AssetDatabase.LoadAssetAtPath<SwordAnimationSet>(setPath);
                if (saved) { EditorUtility.CopySerialized(set, saved); EditorUtility.SetDirty(saved); }
                else { saved = set; AssetDatabase.CreateAsset(saved, setPath); }
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(setPath, ImportAssetOptions.ForceSynchronousImport);
                saved = AssetDatabase.LoadAssetAtPath<SwordAnimationSet>(setPath);
                if (!saved || !saved.IsReady) throw new InvalidOperationException("Saved sword animation set failed verification.");
                Undo.RecordObject(weapon, "Use recommended sword attacks and locomotion");
                weapon.SwordAnimations = saved;
                weapon.CurrentWeapon = WeaponController.Weapon.Sword;
                EditorUtility.SetDirty(weapon);
                PrefabUtility.RecordPrefabInstancePropertyModifications(weapon);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the sword setup.");
                report.AppendLine("Saved resource set validated; player equipped with the existing sword model.");
                report.AppendLine("Controls: 1 sword; click: A1/A2/A3 combo; hold >=0.25s: repeat B3 attacks while held, release stops repeats after the current swing (same input behavior as greatsword); Shift+move+click: B1 running attack; airborne click: B2 aerial attack; C crouch; move crouch walk; hold Shift while crouched: same clip at 1.75x.");
                report.AppendLine("B1/B2/B3 are adapted attacks, not dedicated running/aerial/charge clips. Crouch run reuses crouch walk. Sword movement is in-place, with controller gravity/collisions preserved. Source FBXs, greatsword, bow and NPCs retained.");
                report.AppendLine("Backup: " + backup);
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/RecommendedSword-report.txt", report.ToString());
                Debug.Log("DoubleL sword ready: three-hit combo, greatsword-style held repeat, running and jump attacks, sword crouch walk/run. Scene saved.");
            }
            finally { if (set && !AssetDatabase.Contains(set)) UnityEngine.Object.DestroyImmediate(set); }
        }

        static AnimationClip Copy(string role, string relative, bool loop, StringBuilder report)
        {
            string source = Source + relative;
            var clip = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.isHumanMotion && c.length > .05f);
            if (!clip) throw new InvalidOperationException("A valid Humanoid animation is required: " + source);
            var copy = UnityEngine.Object.Instantiate(clip);
            copy.name = "Sword_" + role;
            AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = loop; settings.loopBlend = loop;
            settings.loopBlendOrientation = true; settings.keepOriginalOrientation = true;
            settings.loopBlendPositionXZ = true; settings.keepOriginalPositionXZ = false;
            settings.loopBlendPositionY = true; settings.keepOriginalPositionY = false; settings.heightFromFeet = true;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            string path = Folder + "/" + role + ".anim";
            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (saved) { EditorUtility.CopySerialized(copy, saved); UnityEngine.Object.DestroyImmediate(copy); EditorUtility.SetDirty(saved); }
            else { saved = copy; AssetDatabase.CreateAsset(saved, path); }
            if (!saved.isHumanMotion || AnimationUtility.GetAnimationEvents(saved).Length != 0)
                throw new InvalidOperationException("Invalid saved sword clip: " + path);
            report.AppendLine(role + " = " + relative + "; " + saved.length.ToString("F3") + "s; loop=" + loop + "; no animation events.");
            return saved;
        }

        static void VerifyRetargeting(GameObject player, SwordAnimationSet set, StringBuilder report)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject clone = null;
            PlayableGraph graph = default;
            try
            {
                clone = UnityEngine.Object.Instantiate(player);
                clone.name = "Sword Retarget Verification";
                SceneManager.MoveGameObjectToScene(clone, scene);
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                foreach (var collider in clone.GetComponentsInChildren<Collider>()) collider.enabled = false;
                var animator = clone.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false; animator.Rebind();
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (!hand || !head) throw new InvalidOperationException("Player's Humanoid skeleton is incomplete.");
                var all = new[] { set.Idle, set.Walk, set.Run, set.Jump, set.CrouchIdle, set.CrouchWalk,
                    set.Combo1, set.Combo2, set.Combo3, set.ChargedAttack, set.RunningAttack, set.AirAttack };
                foreach (var clip in all)
                {
                    graph = PlayableGraph.Create("Verify " + clip.name);
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    playable.SetApplyFootIK(false);
                    var output = AnimationPlayableOutput.Create(graph, "Humanoid", animator);
                    output.SetSourcePlayable(playable); graph.Play();
                    Vector3 first = Vector3.zero; float motion = 0;
                    for (int frame = 0; frame < 5; frame++)
                    {
                        playable.SetTime(clip.length * frame * .2f); graph.Evaluate(0);
                        var point = clone.transform.InverseTransformPoint(hand.position);
                        if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                            throw new InvalidOperationException("Non-finite retargeted pose: " + clip.name);
                        if (frame == 0) first = point; else motion = Mathf.Max(motion, Vector3.Distance(first, point));
                    }
                    if (clip.name.Contains("Attack") || clip.name.Contains("Combo"))
                        if (motion < .005f) throw new InvalidOperationException("Sword clip has no retargeted hand movement: " + clip.name);
                    report.AppendLine("Humanoid retarget verified on player: " + clip.name + "; hand variation=" + motion.ToString("F3") + "m");
                    graph.Destroy();
                }
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (clone) UnityEngine.Object.DestroyImmediate(clone);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
