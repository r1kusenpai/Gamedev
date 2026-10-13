using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    // Original procedural punching prototype. No external animation clips required.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public class CQCCombat : MonoBehaviour
    {
        [Header("Punching")]
        public float PunchDuration = 0.55f;
        public float ComboResetTime = 0.8f;
        public float HitRadius = 0.35f;
        public float AirAttackHitRadius = 0.65f;
        private Vector3 previousAirHitCenter;
        public LayerMask TargetLayers = ~0;
        public bool FaceCameraOnPunch = true;
        public bool IsBusy { get; private set; }
        public bool IsHolding => held != null || Time.time < throwLockedUntil || (importedClips && importedClips.IsHostageSequenceActive);
        private float throwLockedUntil;
        private CQCHostageVictimClips throwingVictimClips;
        private Transform throwingVictimHips;
        private CQCHostageVictimClips heldVictimClips;
        private float grabReadyAt;
        [Range(0.6f, 1f)] public float HostageSpacing = 0.65f;
        private CQCTrainingTarget held;
        private bool waitingForHold;
        private float holdTime;
        private bool holdByButton;
        private float lastHeldTime;
        private CQCCrouchClips importedClips;
        private bool importedAirAttack;
        private bool importedGroundAttack;
        private bool runningHookAttack;
        private float attackDuration;

        private Animator animator;
        private ThirdPersonController movement;
        private CQCPosture posture;
        private StarterAssetsInputs inputs;
        private Transform leftUpper, leftLower, leftHand;
        private Transform rightUpper, rightLower, rightHand, chest;
        private Transform[] bones;
        private Quaternion[] savedRotations;
        private bool poseApplied, queued, hitChecked;
        private int combo;
        private float elapsed, lastFinished = float.NegativeInfinity;
        private AnimatorCullingMode oldCulling;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            movement = GetComponent<ThirdPersonController>();
            inputs = GetComponent<StarterAssetsInputs>();
            if (!animator.avatar || !animator.avatar.isValid || !animator.isHuman)
            {
                Debug.LogError("CQCCombat needs a valid Humanoid avatar.", this);
                enabled = false;
                return;
            }
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (!chest) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (!leftUpper || !leftLower || !leftHand || !rightUpper || !rightLower || !rightHand || !chest)
            {
                Debug.LogError("CQCCombat could not find the arm and spine bones.", this);
                enabled = false;
                return;
            }
            bones = new[] { leftUpper, leftLower, leftHand, rightUpper, rightLower, rightHand, chest };
            savedRotations = new Quaternion[bones.Length];
        }

        private void OnEnable()
        {
            if (!animator) return;
            oldCulling = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private void Update()
        {
            RestorePose();
            if (TryGetComponent<WeaponController>(out var weapon) && weapon.Equipped) return;
            if (!posture) posture = GetComponent<CQCPosture>();
            if (importedClips && importedClips.IsHostageSequenceActive && !held)
            {
                if (!importedClips.IsHostageReleasing) CancelImportedHostage();
                else return;
            }
            if (!Application.isFocused || Time.timeScale <= 0f) return;
            if (Time.time < throwLockedUntil) return;
            bool pressed = false;
            bool grabPressed = false;
            bool buttonHeld = false;
            bool mouseHeld = false;
#if ENABLE_INPUT_SYSTEM
            pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
            grabPressed = Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
            mouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            buttonHeld = (Mouse.current != null && Mouse.current.leftButton.isPressed)
                || (Keyboard.current != null && Keyboard.current.fKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F);
            grabPressed = Input.GetKeyDown(KeyCode.G);
            mouseHeld = Input.GetMouseButton(0);
            buttonHeld = Input.GetMouseButton(0) || Input.GetKey(KeyCode.F);
#endif
            if (grabPressed)
            {
                if (held && Time.time >= grabReadyAt) ReleaseGrab();
                else if (!IsBusy && (!movement || movement.Grounded || GetComponent<CharacterController>().isGrounded)
                    && (!posture || !posture.enabled || posture.CanPunch)) TryGrab();
            }
            if (held)
            {
                if (buttonHeld) lastHeldTime = Time.time;
                if (holdByButton && !buttonHeld && Time.time - lastHeldTime > 0.35f && Time.time >= grabReadyAt)
                { ReleaseGrab(); return; }
                // Holding attack plus a movement direction throws once the grab has finished.
                bool throwRequested = inputs != null && inputs.move.sqrMagnitude > 0.01f
                    && (mouseHeld || pressed);
                if ((pressed || throwRequested) && Time.time >= grabReadyAt)
                {
                    if (inputs != null && inputs.move.sqrMagnitude > 0.01f)
                    {
                        Vector3 forward = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized : transform.forward;
                        if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
                        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                        Vector3 direction = (forward * inputs.move.y + right * inputs.move.x).normalized;
                        // Use one facing and starting anchor for the entire throw.
                        transform.rotation = Quaternion.LookRotation(direction);
                        CharacterController victimController = held.GetComponent<CharacterController>();
                        Vector3 anchor = transform.position + direction * HostageSpacing;
                        if (victimController && victimController.enabled)
                            victimController.Move(Vector3.ProjectOnPlane(anchor - held.transform.position, Vector3.up));
                        held.transform.rotation = transform.rotation;
                        throwingVictimClips = heldVictimClips;
                        Animator throwingAnimator = held.GetComponentInChildren<Animator>();
                        throwingVictimHips = throwingAnimator && throwingAnimator.isHuman
                            ? throwingAnimator.GetBoneTransform(HumanBodyBones.Hips) : null;
                        CancelImportedHostage();
                        float throwDuration = held.Throw(direction, GetComponent<CharacterController>());
                        throwLockedUntil = Time.time + throwDuration;
                        held = null;
                        heldVictimClips = null;
                    }
                    else
                    {
                        held.ReceivePunch(50f, false);
                        if (held.IsDown) { CancelImportedHostage(); held.ReleaseHold(); held = null; }
                    }
                }
                else
                {
                    Vector3 desired = transform.position + transform.forward * HostageSpacing;
                    CharacterController botController = held.GetComponent<CharacterController>();
                    if (!botController || Vector3.Distance(held.transform.position, desired) > 1.6f) ReleaseGrab();
                    else
                    {
                        Vector3 adjustment = Vector3.ProjectOnPlane(desired - held.transform.position, Vector3.up);
                        // Once aligned, leave the controller still; repeated tiny corrections cause jitter.
                        if (adjustment.sqrMagnitude > 0.0001f)
                            botController.Move(Vector3.ClampMagnitude(adjustment, 4f * Time.deltaTime));
                        held.transform.rotation = transform.rotation;
                    }
                }
                return;
            }
            if (pressed && (!movement || !movement.IsLandingFromAirAttack) && (!posture || !posture.enabled || posture.CanPunch))
            {
                if (!IsBusy)
                {
                    combo = Time.time - lastFinished > ComboResetTime ? 0 : (combo + 1) % 4;
                    if (GetComponent<CharacterController>().isGrounded && (inputs == null || inputs.move.sqrMagnitude < 0.001f))
                    { waitingForHold = true; holdTime = 0f; }
                    else BeginPunch();
                }
                else if (!importedAirAttack && elapsed >= attackDuration * 0.2f) queued = true;
            }
            if (waitingForHold)
            {
                if (posture && posture.enabled && !posture.CanPunch)
                { waitingForHold = false; return; }
                holdTime += Time.deltaTime;
                if (!buttonHeld || holdTime >= 0.25f)
                {
                    waitingForHold = false;
                    if (buttonHeld) TryGrab(true);
                    if (!held) BeginPunch();
                }
            }
            if (!IsBusy) return;
            elapsed += Time.deltaTime;
            if (importedAirAttack && !hitChecked && elapsed >= attackDuration * 0.15f)
                hitChecked = CheckHit(true);
            else if (!importedAirAttack && !hitChecked && elapsed >= attackDuration * 0.48f)
            {
                hitChecked = true;
                CheckHit();
            }
            if (elapsed >= attackDuration)
            {
                lastFinished = Time.time;
                if (!importedAirAttack && queued && combo < 3 && (!posture || !posture.enabled || posture.CanPunch))
                {
                    combo++;
                    BeginPunch();
                }
                else
                {
                    IsBusy = false; queued = false;
                    if (importedClips && importedAirAttack) importedClips.StopAirAttack();
                    if (importedClips && importedGroundAttack) importedClips.StopGroundAttack();
                    importedAirAttack = false;
                    importedGroundAttack = false;
                }
            }
        }

        private void BeginPunch()
        {
            IsBusy = true;
            elapsed = 0f;
            queued = false;
            hitChecked = false;
            previousAirHitCenter = transform.position + Vector3.up * 1.1f + transform.forward * 0.65f;
            attackDuration = PunchDuration;
            importedAirAttack = false;
            importedGroundAttack = false;
            runningHookAttack = false;
            if (!importedClips) importedClips = GetComponent<CQCCrouchClips>();
            if (!GetComponent<CharacterController>().isGrounded && importedClips
                && importedClips.TryPlayAirAttack(out float clipDuration))
            { importedAirAttack = true; attackDuration = Mathf.Max(0.1f, clipDuration); }
            else if (importedClips && inputs != null && inputs.sprint && inputs.move.sqrMagnitude > 0.001f
                && (!posture || posture.CurrentStance == CQCPosture.Stance.Standing)
                && importedClips.TryPlayRunningHook(out float hookDuration))
            { importedGroundAttack = true; runningHookAttack = true; combo = -1; attackDuration = Mathf.Max(0.1f, hookDuration); }
            else if (importedClips && importedClips.TryPlayGroundAttack(combo, out float groundDuration))
            { importedGroundAttack = true; attackDuration = Mathf.Max(0.1f, groundDuration); }
            if (FaceCameraOnPunch && Camera.main && (inputs == null || inputs.move.sqrMagnitude < 0.01f))
            {
                Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
                if (forward.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(forward);
            }
        }

        private void TryGrab(bool fromHeldButton = false)
        {
            CQCTrainingTarget closest = null;
            float distance = 1.5f * 1.5f;
            foreach (Collider collider in Physics.OverlapSphere(transform.position + transform.forward * 0.6f + Vector3.up,
                0.9f, TargetLayers, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<CQCTrainingTarget>();
                if (!target || target.IsDown || target.IsHeld || target.transform.IsChildOf(transform)) continue;
                Vector3 delta = target.transform.position - transform.position;
                if (delta.sqrMagnitude >= distance || Vector3.Dot(transform.forward, delta) < 0.1f) continue;
                bool blocked = false;
                Vector3 origin = transform.position + Vector3.up;
                Vector3 toTarget = target.transform.position + Vector3.up - origin;
                foreach (RaycastHit hit in Physics.RaycastAll(origin, toTarget.normalized, toTarget.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<CQCTrainingTarget>() == target) continue;
                    blocked = true; break;
                }
                if (!blocked) { closest = target; distance = delta.sqrMagnitude; }
            }
            if (closest && closest.TryHold())
            {
                if (posture && posture.CurrentStance != CQCPosture.Stance.Standing && !posture.TryChangeStance(CQCPosture.Stance.Standing))
                { closest.ReleaseHold(); return; }
                held = closest;
                holdByButton = fromHeldButton;
                lastHeldTime = Time.time;
                waitingForHold = false;
                if (!importedClips) importedClips = GetComponent<CQCCrouchClips>();
                Animator victimAnimator = held.GetComponentInChildren<Animator>();
                heldVictimClips = victimAnimator ? victimAnimator.GetComponent<CQCHostageVictimClips>() : null;
                if (victimAnimator && !heldVictimClips) heldVictimClips = victimAnimator.gameObject.AddComponent<CQCHostageVictimClips>();
                grabReadyAt = Time.time;
                if (importedClips && importedClips.HasHostageClips && heldVictimClips && heldVictimClips.IsReady)
                {
                    float duration = Mathf.Max(importedClips.HostageTakeLength, heldVictimClips.TakeLength);
                    importedClips.BeginHostage(duration);
                    heldVictimClips.Begin(duration);
                    grabReadyAt = Time.time + duration;
                }
            }
        }

        private void ReleaseGrab(bool playRelease = true)
        {
            if (playRelease && held && importedClips && importedClips.HasHostageClips && heldVictimClips && heldVictimClips.IsReady)
            {
                float duration = Mathf.Max(importedClips.HostageReleaseLength, heldVictimClips.ReleaseLength);
                importedClips.ReleaseHostage(duration);
                heldVictimClips.Release(duration);
            }
            else CancelImportedHostage();
            if (held) held.ReleaseHold();
            held = null;
            waitingForHold = false;
        }

        private void CancelImportedHostage()
        {
            if (importedClips) importedClips.CancelHostage();
            if (heldVictimClips && !heldVictimClips.IsReactionActive) heldVictimClips.Cancel();
        }

        private void LateUpdate()
        {
            if (TryGetComponent<WeaponController>(out var weapon) && weapon.Equipped) return;
            if (Time.time < throwLockedUntil)
            {
                if (!throwingVictimClips || !throwingVictimHips || bones == null) return;
                for (int i = 0; i < bones.Length; i++) savedRotations[i] = bones[i].localRotation;
                poseApplied = true;
                float progress = throwingVictimClips.ShoulderThrowProgress;
                float contact = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.85f, progress));
                float bend = Mathf.Sin(Mathf.Clamp01(progress / 0.7f) * Mathf.PI) * 32f;
                chest.rotation = Quaternion.AngleAxis(bend, transform.right) * chest.rotation;
                // Code-only attacker fallback follows the actual victim until release.
                Vector3 grip = throwingVictimHips.position + Vector3.up * 0.3f;
                PoseArmTo(leftUpper, leftLower, leftHand, grip - transform.right * 0.17f, -1f, contact);
                PoseArmTo(rightUpper, rightLower, rightHand, grip + transform.right * 0.17f, 1f, contact);
                return;
            }
            if (importedAirAttack || importedGroundAttack) return;
            if (importedClips && importedClips.IsHostageSequenceActive) return;
            if ((!IsBusy && !IsHolding) || bones == null) return;
            for (int i = 0; i < bones.Length; i++) savedRotations[i] = bones[i].localRotation;
            poseApplied = true;
            if (held)
            {
                Vector3 neck = held.transform.position + Vector3.up * 1.45f;
                PoseArmTo(leftUpper, leftLower, leftHand, neck - transform.right * 0.1f, -1f, 1f);
                PoseArmTo(rightUpper, rightLower, rightHand, neck + transform.right * 0.1f + transform.forward * 0.1f, 1f, 1f);
                return;
            }
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, PunchDuration));
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.15f))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.7f) / 0.3f)));
            float extension = t < 0.48f
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.18f) / 0.3f))
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.48f) / 0.3f));
            bool leftStrike = combo % 2 == 0;
            float side = leftStrike ? -1f : 1f;
            chest.rotation = Quaternion.AngleAxis(side * 18f * extension * blend, transform.up) * chest.rotation;
            PoseArm(leftUpper, leftLower, leftHand, -1f, leftStrike ? extension : 0f, blend);
            PoseArm(rightUpper, rightLower, rightHand, 1f, leftStrike ? 0f : extension, blend);
        }

        private void PoseArm(Transform upper, Transform lower, Transform hand, float side, float extension, float weight)
        {
            Vector3 guard = chest.position + transform.forward * 0.25f
                + transform.right * (side * 0.19f) + transform.up * 0.08f;
            Vector3 strike = chest.position + transform.forward * 0.8f
                + transform.right * (side * 0.12f);
            if (combo == 2 && extension > 0f)
                strike += transform.right * (-side * 0.16f) + transform.up * 0.06f;
            Vector3 target = Vector3.Lerp(guard, strike, extension);
            PoseArmTo(upper, lower, hand, target, side, weight);
        }

        private void PoseArmTo(Transform upper, Transform lower, Transform hand, Vector3 target, float side, float weight)
        {
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            if (a < 0.001f || b < 0.001f) return;
            Vector3 direction = (target - upper.position).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(target, upper.position), Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            target = upper.position + direction * distance;
            Vector3 bend = Vector3.ProjectOnPlane(transform.right * side - transform.up, direction).normalized;
            float along = (a * a + distance * distance - b * b) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 elbow = upper.position + direction * along + bend * height;
            Quaternion upperRotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            upper.rotation = Quaternion.Slerp(upper.rotation, upperRotation, weight);
            Quaternion lowerRotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
            lower.rotation = Quaternion.Slerp(lower.rotation, lowerRotation, weight);
        }

        private bool CheckHit(bool aerial = false)
        {
            Vector3 origin = transform.position + Vector3.up * (posture && posture.enabled ? posture.HitHeight : 1.1f);
            Vector3 center = origin + transform.forward * (aerial ? 0.65f : 0.8f);
            CQCTrainingTarget nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Collider[] candidates = aerial
                ? Physics.OverlapCapsule(previousAirHitCenter, center, AirAttackHitRadius, TargetLayers, QueryTriggerInteraction.Ignore)
                : Physics.OverlapSphere(center, HitRadius, TargetLayers, QueryTriggerInteraction.Ignore);
            if (aerial) previousAirHitCenter = center;
            foreach (Collider candidate in candidates)
            {
                CQCTrainingTarget target = candidate.GetComponentInParent<CQCTrainingTarget>();
                if (!target || target.IsDown || target.transform.IsChildOf(transform)) continue;
                Vector3 point = candidate.ClosestPoint(origin);
                Vector3 delta = point - origin;
                if (!aerial && Vector3.Dot(transform.forward, delta) <= 0f) continue;
                // Prevent punches passing through solid scenery. Ignore the player's colliders.
                bool blocked = false;
                foreach (RaycastHit obstacle in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (obstacle.transform.IsChildOf(transform)) continue;
                    if (obstacle.collider.GetComponentInParent<CQCTrainingTarget>() == target) continue;
                    blocked = true;
                    break;
                }
                if (!blocked && delta.sqrMagnitude < nearestDistance)
                { nearest = target; nearestDistance = delta.sqrMagnitude; }
            }
            if (!nearest) return false;
            nearest.ReceivePunch(aerial ? 60f : runningHookAttack ? 30f : combo == 3 ? 60f : combo == 0 ? 20f : 30f);
            return true;
        }

        public void CheckAirContactAfterMovement()
        {
            if (importedClips && importedClips.IsAirAttackPlaying && importedClips.AirAttackProgress >= 0.15f && !hitChecked)
                hitChecked = CheckHit(true);
        }

        private void RestorePose()
        {
            if (!poseApplied || bones == null) return;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i]) bones[i].localRotation = savedRotations[i];
            poseApplied = false;
        }

        private void OnDisable()
        {
            throwLockedUntil = 0f;
            throwingVictimClips = null;
            throwingVictimHips = null;
            RestorePose();
            ReleaseGrab(false);
            IsBusy = false;
            if (importedClips && importedAirAttack) importedClips.StopAirAttack();
            if (importedClips && importedGroundAttack) importedClips.StopGroundAttack();
            importedAirAttack = false;
            importedGroundAttack = false;
            waitingForHold = false;
            queued = false;
            if (animator) animator.cullingMode = oldCulling;
        }

        private void OnValidate() { PunchDuration = Mathf.Max(0.2f, PunchDuration); HitRadius = Mathf.Max(0.05f, HitRadius); }
    }
}



