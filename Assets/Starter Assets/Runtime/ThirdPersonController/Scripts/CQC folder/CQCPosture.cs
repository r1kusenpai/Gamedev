using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    // Applies before the punch arm pose; keeps the root upright for CharacterController movement.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterController), typeof(Animator))]
    public class CQCPosture : MonoBehaviour
    {
        public enum Stance { Standing, Crouching, Prone }
        public Stance CurrentStance { get; private set; }
        public float CrouchSpeed = 1.2f;
        public float CrouchSprintSpeed = 2.1f;
        private CQCCrouchClips crouchClips;
        public float ProneSpeed = 0.65f;
        public float DiveSpeed = 4.5f;
        [Header("Pose adjustment")]
        [Range(0.25f, 0.65f)] public float CrouchDrop = 0.46f;
        [Range(10f, 45f)] public float CrouchLean = 30f;
        [Range(0.12f, 0.4f)] public float ProneHipHeight = 0.22f;
        public bool IsDiving { get; private set; }
        private float diveElapsed;
        private Vector3 diveDirection;
        public Vector3 DiveVelocity => IsDiving ? diveDirection * DiveSpeed * (1f - diveElapsed / 0.6f)
            + Vector3.up * (2.8f * Mathf.Cos(diveElapsed / 0.6f * Mathf.PI)) : Vector3.zero;
        public bool CanJump => CurrentStance == Stance.Standing && (!crouchClips || (!crouchClips.IsTransitioning && !crouchClips.IsRolling));
        public bool CanPunch => CurrentStance != Stance.Prone && (!crouchClips || (!crouchClips.IsTransitioning && !crouchClips.IsRolling));
        public bool IsProne => CurrentStance == Stance.Prone;
        public string StatusMessage { get; private set; }
        public float HitHeight => CurrentStance == Stance.Crouching ? 0.75f : 1.1f;

        private CharacterController controller;
        private ThirdPersonController movement;
        private Animator animator;
        private Transform hips, chest, leftUpper, leftLower, leftFoot, rightUpper, rightLower, rightFoot;
        private Transform leftArm, leftForearm, leftHand, rightArm, rightForearm, rightHand, head;
        private StarterAssetsInputs inputs;
        private Transform[] bones;
        private Quaternion[] rotations;
        private Vector3 hipsPosition, cameraPosition;
        private Transform cameraTarget;
        private float standingHeight, standingStep, bottom;
        private Vector3 standingCenter;
        private bool poseApplied;
        private float stanceBlend;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            movement = GetComponent<ThirdPersonController>();
            animator = GetComponent<Animator>();
            inputs = GetComponent<StarterAssetsInputs>();
            standingHeight = controller.height;
            standingCenter = controller.center;
            standingStep = controller.stepOffset;
            bottom = standingCenter.y - standingHeight * 0.5f;
            if (movement && movement.CinemachineCameraTarget)
            {
                cameraTarget = movement.CinemachineCameraTarget.transform;
                cameraPosition = cameraTarget.localPosition;
            }
            if (!animator.isHuman) { enabled = false; return; }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (!chest) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!hips || !chest || !leftUpper || !leftLower || !leftFoot || !rightUpper || !rightLower || !rightFoot)
            { Debug.LogError("CQCPosture needs Humanoid hips and leg bones.", this); enabled = false; return; }
            bones = new[] { hips, chest, head, leftUpper, leftLower, leftFoot, rightUpper, rightLower, rightFoot,
                leftArm, leftForearm, leftHand, rightArm, rightForearm, rightHand };
            rotations = new Quaternion[bones.Length];
        }

        private void Update()
        {
            RestorePose();
            if (!crouchClips) crouchClips = GetComponent<CQCCrouchClips>();
            if (!Application.isFocused || Time.timeScale <= 0f) return;
            if (IsDiving)
            {
                diveElapsed += Time.deltaTime;
                if (diveElapsed >= 0.6f) IsDiving = false;
            }
            bool crouch = false, prone = false, dive = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            { crouch = Keyboard.current.cKey.wasPressedThisFrame; prone = Keyboard.current.zKey.wasPressedThisFrame; }
#elif ENABLE_LEGACY_INPUT_MANAGER
            crouch = Input.GetKeyDown(KeyCode.C); prone = Input.GetKeyDown(KeyCode.Z);
            // X selects unarmed; Z toggles prone.
#endif
            if (dive && !IsDiving && (!crouchClips || !crouchClips.IsRolling) && (!movement || movement.Grounded || controller.isGrounded))
            {
                var combat = GetComponent<CQCCombat>();
                if ((!combat || (!combat.IsBusy && !combat.IsHolding)) && TryChangeStance(Stance.Prone))
                { IsDiving = true; diveElapsed = 0f; diveDirection = transform.forward; }
            }
            if ((crouch || prone) && !IsDiving)
            {
                if (crouchClips && (crouchClips.IsTransitioning || crouchClips.IsRolling))
                { StatusMessage = "Finish changing stance first."; return; }
                if (movement && !movement.Grounded && !controller.isGrounded)
                { StatusMessage = "Land on the ground before changing stance."; return; }
                var combat = GetComponent<CQCCombat>();
                if (combat && (combat.IsBusy || combat.IsHolding))
                { StatusMessage = "Finish the punch before changing stance."; return; }
                Stance next = prone ? (IsProne ? Stance.Standing : Stance.Prone)
                    : (CurrentStance == Stance.Crouching ? Stance.Standing : Stance.Crouching);
                TryChangeStance(next);
            }
            if (cameraTarget)
            {
                Vector3 desired = cameraPosition;
                desired.y -= CurrentStance == Stance.Crouching ? 0.45f : IsProne ? 1.05f : 0f;
                if (crouchClips && crouchClips.IsRolling) desired.y -= 0.55f;
                cameraTarget.localPosition = Vector3.Lerp(cameraTarget.localPosition, desired, 1f - Mathf.Exp(-12f * Time.deltaTime));
            }
        }

        public bool TryChangeStance(Stance next)
        {
            float height = next == Stance.Standing ? standingHeight
                : next == Stance.Crouching ? Mathf.Max(controller.radius * 2f, standingHeight * 0.65f)
                : Mathf.Max(controller.radius * 2f, 0.6f);
            if (height > controller.height + 0.01f && !HasClearance(height))
            { StatusMessage = "Something overhead is blocking you from standing."; return false; }
            controller.stepOffset = Mathf.Min(standingStep, height * 0.3f);
            controller.height = height;
            controller.center = new Vector3(standingCenter.x, bottom + height * 0.5f, standingCenter.z);
            Stance previous = CurrentStance;
            CurrentStance = next;
            if (!crouchClips) crouchClips = GetComponent<CQCCrouchClips>();
            if (crouchClips) crouchClips.BeginTransition(previous, next);
            StatusMessage = "";
            return true;
        }

        private bool HasClearance(float height)
        {
            Vector3 scale = transform.lossyScale;
            float radius = Mathf.Max(0.01f, controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) - 0.04f);
            Vector3 center = transform.TransformPoint(new Vector3(standingCenter.x, bottom + height * 0.5f, standingCenter.z));
            float half = Mathf.Max(0f, height * Mathf.Abs(scale.y) * 0.5f - radius - 0.04f);
            foreach (Collider obstacle in Physics.OverlapCapsule(center + transform.up * half,
                center - transform.up * half, radius, ~0, QueryTriggerInteraction.Ignore))
                if (!obstacle.transform.IsChildOf(transform)) return false;
            return true;
        }

        private void LateUpdate()
        {
            stanceBlend = Mathf.MoveTowards(stanceBlend, CurrentStance == Stance.Standing ? 0f : 1f, Time.deltaTime * 8f);
            // Avoid bending an already crouched imported animation a second time.
            if (crouchClips && crouchClips.UsesImportedPose) return;
            if (stanceBlend <= 0f || bones == null) return;
            for (int i = 0; i < bones.Length; i++) if (bones[i]) rotations[i] = bones[i].localRotation;
            hipsPosition = hips.localPosition;
            poseApplied = true;
            if (IsProne)
            {
                Quaternion headFacing = head ? head.rotation : Quaternion.identity;
                // Tilt the skeleton, not the player root or its movement collider.
                Vector3 spineDirection = chest.position - hips.position;
                Quaternion tilt = Quaternion.FromToRotation(spineDirection.normalized, transform.forward);
                hips.rotation = Quaternion.Slerp(hips.rotation, tilt * hips.rotation, stanceBlend);
                float motion = inputs != null ? Mathf.Clamp01(inputs.move.magnitude) : 0f;
                float crawl = Mathf.Sin(Time.time * 4f) * motion;
                float leftPull = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f * motion;
                float rightPull = (1f - Mathf.Sin(Time.time * 4f)) * 0.5f * motion;
                Vector3 lowPosition = FloorPoint(Vector3.zero, ProneHipHeight + Mathf.Abs(crawl) * 0.015f);
                hips.position = Vector3.Lerp(hips.position, lowPosition, stanceBlend);
                // Stretch the legs back and place forearms forward, rather than leaving the idle knees tucked up.
                BendLimb(leftUpper, leftLower, leftFoot, FloorPoint(-transform.forward * (0.79f - leftPull * 0.44f)
                    - transform.right * (0.14f + leftPull * 0.14f), 0.13f), -transform.right + transform.up * 0.12f);
                BendLimb(rightUpper, rightLower, rightFoot, FloorPoint(-transform.forward * (0.79f - rightPull * 0.44f)
                    + transform.right * (0.14f + rightPull * 0.14f), 0.13f), transform.right + transform.up * 0.12f);
                BendLimb(leftArm, leftForearm, leftHand, FloorPoint(transform.forward * (0.67f + crawl * 0.12f)
                    - transform.right * 0.23f, 0.11f), -transform.right - transform.up * 2f);
                BendLimb(rightArm, rightForearm, rightHand, FloorPoint(transform.forward * (0.67f - crawl * 0.12f)
                    + transform.right * 0.23f, 0.11f), transform.right - transform.up * 2f);
                if (head) head.rotation = Quaternion.Slerp(head.rotation, headFacing, 0.65f * stanceBlend);
            }
            else
            {
                Vector3 leftTarget = leftFoot.position, rightTarget = rightFoot.position;
                Quaternion headFacing = head ? head.rotation : Quaternion.identity;
                hips.position -= (transform.up * CrouchDrop + transform.forward * 0.08f) * stanceBlend;
                hips.rotation = Quaternion.AngleAxis(CrouchLean * 0.5f * stanceBlend, transform.right) * hips.rotation;
                chest.rotation = Quaternion.AngleAxis(CrouchLean * 0.5f * stanceBlend, transform.right) * chest.rotation;
                BendLeg(leftUpper, leftLower, leftFoot, leftTarget);
                BendLeg(rightUpper, rightLower, rightFoot, rightTarget);
                if (head) head.rotation = Quaternion.Slerp(head.rotation, headFacing, stanceBlend);
                // Keep bent elbows and hands forward while crouch-walking, rather than leaving a standing arm swing.
                if (!crouchClips || (!crouchClips.UsesCrouchArmPose && !crouchClips.UsesMovingPunchPose))
                {
                    BendLimb(leftArm, leftForearm, leftHand, chest.position + transform.forward * 0.24f
                        - transform.right * 0.24f - transform.up * 0.18f, -transform.right - transform.up);
                    BendLimb(rightArm, rightForearm, rightHand, chest.position + transform.forward * 0.3f
                        + transform.right * 0.2f - transform.up * 0.14f, transform.right - transform.up);
                }
            }
        }

        private void BendLeg(Transform upper, Transform lower, Transform foot, Vector3 target)
        {
            BendLimb(upper, lower, foot, target, transform.forward);
        }

        private Vector3 FloorPoint(Vector3 horizontalOffset, float clearance)
        {
            Vector3 point = transform.position + horizontalOffset;
            int mask = movement && movement.GroundLayers.value != 0 ? movement.GroundLayers.value : ~0;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 1.5f, Vector3.down, 3f, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<CQCTrainingTarget>()) continue;
                if (hit.distance < nearest) { nearest = hit.distance; point.y = hit.point.y; }
            }
            return point + Vector3.up * clearance;
        }

        private void BendLimb(Transform upper, Transform lower, Transform foot, Vector3 target, Vector3 bendDirection)
        {
            if (!upper || !lower || !foot) return;
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, foot.position);
            if (a < 0.001f || b < 0.001f) return;
            Vector3 direction = (target - upper.position).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(target, upper.position), Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            target = upper.position + direction * distance;
            float along = (a * a + distance * distance - b * b) / (2f * distance);
            Vector3 bend = Vector3.ProjectOnPlane(bendDirection, direction).normalized;
            Vector3 knee = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, knee - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(foot.position - lower.position, target - lower.position) * lower.rotation;
        }

        private void RestorePose()
        {
            if (!poseApplied || bones == null) return;
            for (int i = 0; i < bones.Length; i++) if (bones[i]) bones[i].localRotation = rotations[i];
            if (hips) hips.localPosition = hipsPosition;
            poseApplied = false;
        }

        private void OnDisable()
        {
            RestorePose();
            if (controller)
            { controller.height = standingHeight; controller.center = standingCenter; controller.stepOffset = standingStep; }
            if (cameraTarget) cameraTarget.localPosition = cameraPosition;
            CurrentStance = Stance.Standing;
            IsDiving = false;
        }
    }
}
