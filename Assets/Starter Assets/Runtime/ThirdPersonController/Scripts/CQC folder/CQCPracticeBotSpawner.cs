using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class CQCPracticeBotSpawner : MonoBehaviour
    {
        public bool BotFollowsPlayer = true;
        public bool SpawnOnStart = true;
        public bool ShowControls = false;
        public GameObject BotModelPrefab;
        public GameObject SpawnedBot => spawned;
        private string status = "";
        private GameObject spawned;

        private void Start()
        {
            GameObject existing = GameObject.Find("CQC Practice Bot");
            if (existing && existing.GetComponent<CQCPracticeBot>() && existing.GetComponent<CQCPracticeBot>().Player == transform)
                spawned = existing;
            if (spawned && !spawned.GetComponentInChildren<Animator>()) { RemoveObject(spawned); spawned = null; }
            if (!spawned && SpawnOnStart) SpawnBot();
        }

        private void Update()
        {
            if (!Application.isFocused || Time.timeScale <= 0f) return;
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetKeyDown(KeyCode.B);
#endif
            if (pressed) SpawnBot();
        }

        [ContextMenu("Spawn Practice Bot")]
        public void SpawnBot()
        {
            var movement = GetComponent<ThirdPersonController>();
            int mask = ~0;
            Vector3 position = Vector3.zero;
            bool found = false;
            // Try several nearby positions so a tree or wall does not trap the spawned bot.
            for (int i = 0; i < 32; i++)
            {
                Vector3 direction = Quaternion.AngleAxis((i % 8) * 45f, Vector3.up) * transform.forward;
                Vector3 probe = transform.position + direction * (2.5f + (i / 8) * 1.5f) + Vector3.up * 2f;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 6f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Dot(ground.normal, Vector3.up) < 0.7f) continue;
                position = ground.point + Vector3.up * 0.06f;
                bool blocked = false;
                foreach (Collider obstacle in Physics.OverlapCapsule(position + Vector3.up * 0.36f,
                    position + Vector3.up * 1.5f, 0.27f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (spawned && obstacle.transform.IsChildOf(spawned.transform)) continue;
                    blocked = true; break;
                }
                if (!blocked) { found = true; break; }
            }
            if (!found)
            { status = "No clear place for the bot. Move into a clearing and press B."; Debug.LogWarning(status, this); return; }
            status = "Practice bot ready.";
            if (!spawned)
            {
                GameObject existing = GameObject.Find("CQC Practice Bot");
                if (existing && existing.GetComponent<CQCPracticeBot>() && existing.GetComponent<CQCPracticeBot>().Player == transform)
                    spawned = existing;
            }
            if (spawned) RemoveObject(spawned);
            spawned = new GameObject("CQC Practice Bot");
            spawned.SetActive(false);
            spawned.transform.position = position;
            Vector3 toward = Vector3.ProjectOnPlane(transform.position - position, Vector3.up);
            if (toward.sqrMagnitude > 0.01f) spawned.transform.rotation = Quaternion.LookRotation(toward);
            var controller = spawned.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.04f;
            // Clone the existing rig under an inactive parent, before any player scripts can run.
            GameObject visualObject = Instantiate(BotModelPrefab ? BotModelPrefab : gameObject, spawned.transform);
            visualObject.name = "Robot Model";
            Transform visual = visualObject.transform;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            foreach (MonoBehaviour script in visualObject.GetComponentsInChildren<MonoBehaviour>(true))
            { script.enabled = false; RemoveObject(script); }
            foreach (Collider collider in visualObject.GetComponentsInChildren<Collider>(true))
            { collider.enabled = false; RemoveObject(collider); }
            foreach (Camera camera in visualObject.GetComponentsInChildren<Camera>(true))
            { camera.enabled = false; RemoveObject(camera); }
            foreach (AudioListener listener in visualObject.GetComponentsInChildren<AudioListener>(true))
            { listener.enabled = false; RemoveObject(listener); }
            foreach (Transform child in visualObject.GetComponentsInChildren<Transform>(true)) child.gameObject.tag = "Untagged";
            Animator robotAnimator = visualObject.GetComponentInChildren<Animator>(true);
            if (robotAnimator)
            { robotAnimator.enabled = true; robotAnimator.applyRootMotion = false; robotAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            var target = spawned.AddComponent<CQCTrainingTarget>();
            target.VisualRoot = visual;
            target.FeedbackRenderer = visualObject.GetComponentInChildren<Renderer>(true);
            var bot = spawned.AddComponent<CQCPracticeBot>();
            bot.Player = transform;
            bot.FollowPlayer = BotFollowsPlayer;
            spawned.SetActive(true);
        }

        private Transform Part(Transform parent, string label, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            RemoveObject(collider);
            var renderer = part.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            return part.transform;
        }

        private void RemoveObject(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void OnGUI()
        {
            // The on-screen practice guide was removed at the user's request.
        }

        private void OnDestroy() { if (Application.isPlaying && spawned) Destroy(spawned); }
    }
}
