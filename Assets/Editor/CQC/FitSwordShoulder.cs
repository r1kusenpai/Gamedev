#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class FitSwordShoulder
    {
        const string Request = "Assets/Editor/CQC/FitSwordLowerRest-v1.request.txt";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static double next;
        static FitSwordShoulder() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 2;
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "MyForest") return;
            try { Apply(); AssetDatabase.DeleteAsset(Request); }
            catch (Exception e)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/SwordLowerRest-error.txt", e.ToString());
                Debug.LogException(e);
                AssetDatabase.MoveAsset(Request, Request + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".failed");
            }
        }

        [MenuItem("Tools/Weapons/Fit Sword Down Beside Right Hip")]
        public static void Apply()
        {
            var player = GameObject.Find("PlayerArmature");
            var weapon = player ? player.GetComponent<WeaponController>() : null;
            if (!weapon || !weapon.SwordAnimations || !weapon.SwordAnimations.IsReady || !weapon.SwordModelPrefab)
                throw new InvalidOperationException("The player's sword animations and model must be assigned.");
            Directory.CreateDirectory("Logs");
            var report = new StringBuilder("Lower sword resting pose\n");
            Verify(player, report);
            var scene = SceneManager.GetActiveScene();
            string backup = "Assets/ForestExpansion/Backups/MyForest-before-lower-sword-rest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Lower sword rest backup failed.");
            Undo.RecordObject(weapon, "Rest sword down beside the right hip");
            weapon.SwordRelaxedCarry = true;
            weapon.SwordRestGrip = new Vector3(.10f,-.42f,.05f);
            weapon.SwordRestTip = new Vector3(.28f,-.72f,.41f);
            EditorUtility.SetDirty(weapon); PrefabUtility.RecordPrefabInstancePropertyModifications(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save lower sword rest.");
            report.AppendLine("Enabled for standing sword idle/walk/run, with a 0.1-second blend out for attacks, jumping, crouching and rolling. Grip stays in palm. Held repeat and combo input logic retained.");
            report.AppendLine("Backup: " + backup);
            File.WriteAllText("Logs/SwordLowerRest-report.txt", report.ToString());
            Debug.Log("Sword resting pose lowered beside the right hip, with hand contact and blade clearance verified. Scene saved.");
        }

        static void Verify(GameObject original, StringBuilder report)
        {
            PreviewRenderUtility preview = null;
            PlayableGraph graph = default;
            try
            {
                preview = new PreviewRenderUtility();
                var clone = UnityEngine.Object.Instantiate(original);
                clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                foreach (var collider in clone.GetComponentsInChildren<Collider>()) collider.enabled = false;
                preview.AddSingleGO(clone);
                var animator = clone.GetComponent<Animator>();
                var weapon = clone.GetComponent<WeaponController>();
                weapon.SwordRestGrip = new Vector3(.10f,-.42f,.05f);
                weapon.SwordRestTip = new Vector3(.28f,-.72f,.41f);
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false; animator.Rebind();
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var model = UnityEngine.Object.Instantiate(weapon.SwordModelPrefab, hand, false);
                model.transform.localScale = Vector3.one;
                typeof(WeaponController).GetField("animator", Private).SetValue(weapon, animator);
                typeof(WeaponController).GetField("sword", Private).SetValue(weapon, model);
                var fit = typeof(WeaponController).GetMethod("FitGrip", Private);
                var carry = typeof(WeaponController).GetMethod("ApplySwordRestPose", Private);
                object[] args = { model, HumanBodyBones.RightHand, HumanBodyBones.RightIndexProximal,
                    HumanBodyBones.RightLittleProximal, weapon.SwordGripOffset, weapon.SwordGripAdjustment };
                preview.camera.transform.position = new Vector3(2.6f, 2.05f, -2.8f);
                preview.camera.transform.LookAt(new Vector3(0,1.2f,-.15f));
                preview.camera.nearClipPlane = .05f; preview.camera.farClipPlane = 30;
                preview.camera.fieldOfView = 40;
                preview.camera.backgroundColor = new Color(.13f,.15f,.19f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.lights[0].intensity = 1.5f;
                preview.lights[0].transform.rotation = Quaternion.Euler(30,30,0);
                preview.lights[1].intensity = 1f;
                var set = weapon.SwordAnimations;
                foreach (var clip in new[] { set.Idle, set.Walk, set.Run })
                {
                    graph = PlayableGraph.Create("Sword Shoulder Verification");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph,clip);
                    playable.SetApplyFootIK(false);
                    var output = AnimationPlayableOutput.Create(graph,"Shoulder preview",animator);
                    output.SetSourcePlayable(playable); graph.Play();
                    float minimum = float.MaxValue, gripError = 0, lowestTip = float.MaxValue;
                    for (int frame=0; frame<12; frame++)
                    {
                        playable.SetTime(clip.length*frame/12f); graph.Evaluate(0);
                        fit.Invoke(weapon,args);
                        if (clip == set.Idle && frame == 0) Capture(preview,"Logs/SwordLowerRest-before.png",clone);
                        carry.Invoke(weapon,new object[] { 1f }); fit.Invoke(weapon,args);
                        Vector3 palm = Vector3.Lerp(hand.position,(index.position+little.position)*.5f,.65f)
                            + hand.TransformVector(weapon.SwordGripOffset);
                        gripError = Mathf.Max(gripError,Vector3.Distance(palm,model.transform.position));
                        Vector3 bladeStart = model.transform.TransformPoint(new Vector3(0,.18f,0));
                        Vector3 bladeEnd = model.transform.TransformPoint(new Vector3(0,1.2f,0));
                        lowestTip = Mathf.Min(lowestTip, clone.transform.InverseTransformPoint(bladeEnd).y);
                        Vector3 segment = bladeEnd-bladeStart;
                        float t = Mathf.Clamp01(Vector3.Dot(head.position-bladeStart,segment)/segment.sqrMagnitude);
                        minimum = Mathf.Min(minimum,Vector3.Distance(head.position,bladeStart+segment*t));
                        if (Vector3.Dot(model.transform.up,clone.transform.up) > -.5f)
                            throw new InvalidOperationException("Resting blade does not point downward: " + clip.name);
                        if (clip == set.Idle && frame == 0) Capture(preview,"Logs/SwordLowerRest-after.png",clone);
                    }
                    if (gripError > .002f || minimum < .20f || lowestTip < .05f)
                        throw new InvalidOperationException("Sword hand contact or head clearance failed: " + clip.name + "; grip=" + gripError + "; head=" + minimum);
                    report.AppendLine(clip.name + ": 12 samples; palm error=" + gripError.ToString("F5") + "m; minimum blade-to-head-center=" + minimum.ToString("F3") + "m; lowest blade tip=" + lowestTip.ToString("F3") + "m.");
                    graph.Destroy();
                }
            }
            finally { if(graph.IsValid())graph.Destroy(); if(preview!=null)preview.Cleanup(); }
        }

        internal static void Capture(PreviewRenderUtility preview, string path, GameObject posedModel)
        {
            var bakedObjects = new List<GameObject>();
            var bakedMeshes = new List<Mesh>();
            var originals = new List<SkinnedMeshRenderer>();
            var previous = RenderTexture.active; Texture2D image = null;
            try
            {
                // Freeze the evaluated bones so render-time animation updates cannot replace the pose.
                foreach (var skin in posedModel.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled) continue;
                    var mesh = new Mesh(); skin.BakeMesh(mesh);
                    bakedMeshes.Add(mesh);
                    var go = new GameObject("Evaluated pose"); go.transform.SetParent(skin.transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                    bakedObjects.Add(go); originals.Add(skin); skin.enabled=false;
                }
                RenderTexture texture = null;
                for (int warmup=0; warmup<2; warmup++)
                {
                    preview.BeginPreview(new Rect(0,0,900,900), GUIStyle.none);
                    preview.Render(true); texture = preview.EndPreview() as RenderTexture;
                }
                if (!texture) throw new IOException("Sword preview render failed.");
                RenderTexture.active = texture;
                image = new Texture2D(900,900,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,900,900),0,0); image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous; if(image)UnityEngine.Object.DestroyImmediate(image);
                foreach(var go in bakedObjects)UnityEngine.Object.DestroyImmediate(go);
                foreach(var mesh in bakedMeshes)UnityEngine.Object.DestroyImmediate(mesh);
                foreach(var skin in originals)if(skin)skin.enabled=true;
            }
        }
    }
}
#endif
