using UnityEngine;

namespace StarterAssets
{
    public class CQCTrainingTarget : MonoBehaviour
    {
        public float MaxHealth = 140f;
        public float ResetAfterSeconds = 3f;
        public Transform VisualRoot;
        public Renderer FeedbackRenderer;
        public bool IsDown { get; private set; }
        public float StunnedUntil { get; private set; }
        public bool IsHeld { get; private set; }
        public bool TryHold()
        {
            if (!ready || IsDown || IsHeld) return false;
            IsHeld = true;
            return true;
        }
        public void ReleaseHold() { IsHeld = false; StunnedUntil = Time.time + 0.5f; }
        public float Throw(Vector3 direction, CharacterController thrower = null)
        {
            ReleaseHold();
            ReceivePunch(MaxHealth, false);
            var bot = GetComponent<CQCPracticeBot>();
            if (reactionClips && reactionClips.PlayShoulderThrow(out float throwDuration, thrower, direction))
            {
                resetAt = Time.time + throwDuration + Mathf.Max(0.2f, ResetAfterSeconds);
                if (bot) bot.ClearThrowVelocity();
                return throwDuration;
            }
            if (bot) bot.Push(direction);
            return 0f;
        }
        private float health, resetAt, flashUntil;
        private MaterialPropertyBlock properties;
        private Color originalColor;
        private int colorProperty;
        private Vector3 initialScale, visualPosition, standingCenter;
        private Quaternion visualRotation;
        private CharacterController controller;
        private float standingHeight, standingStep;
        private bool ready;
        private CQCHostageVictimClips reactionClips;

        private void Start()
        {
            health = MaxHealth;
            initialScale = transform.localScale;
            if (!FeedbackRenderer) FeedbackRenderer = GetComponentInChildren<Renderer>();
            properties = new MaterialPropertyBlock();
            if (FeedbackRenderer && FeedbackRenderer.sharedMaterial)
            {
                colorProperty = FeedbackRenderer.sharedMaterial.HasProperty("_BaseColor")
                    ? Shader.PropertyToID("_BaseColor") : Shader.PropertyToID("_Color");
                originalColor = FeedbackRenderer.sharedMaterial.GetColor(colorProperty);
                FeedbackRenderer.GetPropertyBlock(properties);
                if (properties.HasColor(colorProperty)) originalColor = properties.GetColor(colorProperty);
            }
            if (VisualRoot) { visualPosition = VisualRoot.localPosition; visualRotation = VisualRoot.localRotation; }
            controller = GetComponent<CharacterController>();
            if (controller) { standingHeight = controller.height; standingCenter = controller.center; standingStep = controller.stepOffset; }
            ready = true;
        }

        public void ReceivePunch(float damage, bool backwardFall = true)
        {
            if (!ready || IsDown) return;
            health -= damage;
            flashUntil = Time.time + 0.18f;
            StunnedUntil = Time.time + 0.8f;
            if (!reactionClips) reactionClips = GetComponentInChildren<CQCHostageVictimClips>();
            Debug.Log("Practice bot hit. Health: " + Mathf.Max(0f, health), this);
            if (health <= 0f)
            {
                IsDown = true;
                resetAt = Time.time + Mathf.Max(0.2f, ResetAfterSeconds);
                if (reactionClips && reactionClips.PlayKnockdown(backwardFall, out float fallDuration))
                    resetAt = Time.time + fallDuration + Mathf.Max(0.2f, ResetAfterSeconds);
                if (controller)
                {
                    controller.stepOffset = 0.1f;
                    controller.height = Mathf.Max(0.6f, controller.radius * 2f);
                    controller.center = new Vector3(standingCenter.x,
                        standingCenter.y - standingHeight * 0.5f + controller.height * 0.5f, standingCenter.z);
                }
            }
            else if (reactionClips && reactionClips.PlayStun(0.8f))
                StunnedUntil = Time.time + reactionClips.CurrentReactionDuration;
        }

        private bool CanRecover()
        {
            if (!controller) return true;
            Vector3 center = transform.TransformPoint(standingCenter);
            float radius = Mathf.Max(0.01f, controller.radius - 0.04f);
            float half = Mathf.Max(0f, standingHeight * 0.5f - radius - 0.04f);
            foreach (Collider obstacle in Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half,
                radius, ~0, QueryTriggerInteraction.Ignore))
                if (!obstacle.transform.IsChildOf(transform)) return false;
            return true;
        }

        private void Update()
        {
            if (IsDown && Time.time >= resetAt && CanRecover())
            {
                IsDown = false; health = MaxHealth;
                if (reactionClips) reactionClips.Cancel();
                if (controller) { controller.height = standingHeight; controller.center = standingCenter; controller.stepOffset = standingStep; }
            }
            float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
            if (VisualRoot)
            {
                bool animated = reactionClips && reactionClips.IsReactionActive;
                Quaternion desired = visualRotation * Quaternion.Euler(animated ? 0f : IsDown ? 90f : Time.time < flashUntil ? -12f : 0f, 0f, 0f);
                VisualRoot.localRotation = Quaternion.Slerp(VisualRoot.localRotation, desired, blend);
                VisualRoot.localPosition = Vector3.Lerp(VisualRoot.localPosition, visualPosition + (IsDown && !animated ? Vector3.up * 0.25f : Vector3.zero), blend);
            }
            else if (!controller)
                transform.localScale = Vector3.Lerp(transform.localScale,
                    IsDown ? Vector3.Scale(initialScale, new Vector3(1.2f, 0.25f, 1.2f)) : initialScale, blend);
            if (!FeedbackRenderer) return;
            FeedbackRenderer.GetPropertyBlock(properties);
            properties.SetColor(colorProperty, IsDown ? Color.gray : Time.time < flashUntil ? Color.red : originalColor);
            FeedbackRenderer.SetPropertyBlock(properties);
        }
    }
}
