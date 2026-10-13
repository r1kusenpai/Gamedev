#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class CQCShoulderCamera
    {
        static CQCShoulderCamera()
        {
            EditorApplication.delayCall += ApplyOnce;
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyOnce; };
        }
        private static void ApplyOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += ApplyOnce; return; }
            string key = "CQCShoulderCamera-v1-" + EditorSceneManager.GetActiveScene().path;
            if (SessionState.GetBool(key, false)) return;
            if (Apply()) SessionState.SetBool(key, true);
        }
        [MenuItem("Tools/CQC/Apply Close Shoulder Camera")]
        public static void ApplyFromMenu() { Apply(); }

        public static bool Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            GameObject go = GameObject.Find("PlayerFollowCamera");
            if (!go) return false;
            var camera = go.GetComponent<CinemachineCamera>();
            var follow = go.GetComponent<CinemachineThirdPersonFollow>();
            if (!camera || !follow) return false;
            Undo.RecordObjects(new Object[] { camera, follow }, "Close Shoulder Camera");
            follow.CameraSide = 1f;
            follow.ShoulderOffset = new Vector3(0.55f, 0.08f, 0f);
            follow.VerticalArmLength = 0.12f;
            follow.CameraDistance = 1.65f;
            follow.Damping = new Vector3(0.08f, 0.15f, 0.12f);
            var lens = camera.Lens; lens.FieldOfView = 55f; lens.NearClipPlane = 0.08f; camera.Lens = lens;
            var noise = go.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise)
            { Undo.RecordObject(noise, "Steady Shoulder Camera"); noise.AmplitudeGain = 0f; EditorUtility.SetDirty(noise); PrefabUtility.RecordPrefabInstancePropertyModifications(noise); }
            GameObject player = GameObject.Find("PlayerArmature");
            var motor = player ? player.GetComponent<ThirdPersonController>() : null;
            if (motor && motor.CinemachineCameraTarget)
            {
                Transform target = motor.CinemachineCameraTarget.transform;
                Undo.RecordObject(target, "Shoulder Camera Target");
                Vector3 position = target.localPosition; position.y = 1.5f; target.localPosition = position;
                camera.Follow = target;
                EditorUtility.SetDirty(target); PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            EditorUtility.SetDirty(camera); EditorUtility.SetDirty(follow);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera); PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return true;
        }
    }
}
#endif
