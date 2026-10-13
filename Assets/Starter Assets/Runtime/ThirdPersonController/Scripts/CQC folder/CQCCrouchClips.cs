using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(Animator))]
    public class CQCCrouchClips : MonoBehaviour
    {
        public bool UseWalkingClipWithoutShift = false;
        [Range(0.5f, 2f)] public float CrouchRunPlaybackSpeed = 1.35f;
        public bool IsTransitioning { get; private set; }
        public bool WeaponActive => TryGetComponent<WeaponController>(out var weapon) && weapon.UsesWeaponPose;
        public bool UsesImportedPose => WeaponActive || PoseWeight > 0.001f;
        public float PoseWeight { get; private set; }
        private PlayableGraph graph;
        private AnimationPlayableOutput weaponOutput;
        public PlayableGraph WeaponGraph => graph;
        public AnimationLayerMixerPlayable CreateWeaponOverlay()
        {
            var blend = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(armLayers, 0, blend, 0);
            blend.SetInputWeight(0, 1); blend.SetInputWeight(1, 0);
            weaponOutput.SetSourcePlayable(blend); return blend;
        }
        private AnimatorControllerPlayable baseController;
        private AnimationMixerPlayable mixer;
        private AnimationLayerMixerPlayable armLayers;
        private AnimationClipPlayable crouchArms;
        private AvatarMask crouchArmMask;
        private AvatarMask hookMask;
        private AnimationClip hookClip;
        private AnimationClipPlayable hookPlayable;
        private bool runningHook;
        private float hookElapsed, hookDuration;
        private bool finishHookAfterEvaluation;
        public bool TryPlayRunningHook(out float duration)
        {
            duration = 0f;
            if (!enabled || !graph.IsValid() || !hookClip) return false;
            duration = hookClip.length;
            hookDuration = duration; hookElapsed = 0f; runningHook = true; finishHookAfterEvaluation = false;
            hookPlayable.SetTime(0f); hookPlayable.SetSpeed(1f); hookPlayable.SetDuration(duration); hookPlayable.SetDone(false);
            armLayers.SetInputWeight(2, 1f);
            return true;
        }
        private float crouchArmWeight;
        public bool UsesCrouchArmPose => crouchArmWeight > 0.001f;
        private AnimationClip rollClip;
        private AnimationClipPlayable rollPlayable;
        private float rollElapsed;
        private bool finishRollAfterEvaluation;
        [Range(1f, 4f)] public float RollPlaybackSpeed = 2f;
        public float RollDistance = 3.5f;
        private Vector3 rollDirection;
        private float rollDuration, rollTravelSpeed;
        public bool IsRolling { get; private set; }
        public Vector3 RollVelocity => IsRolling ? rollDirection * rollTravelSpeed : Vector3.zero;
        private AnimationMixerPlayable movingPunchMixer;
        private readonly AnimationClipPlayable[] movingPunches = new AnimationClipPlayable[3];
        private bool movingPunchActive;
        public bool UsesMovingPunchPose => movingPunchActive;
        public void TryStartRoll()
        {
            if (!rollClip || !graph.IsValid() || IsRolling || IsTransitioning || IsGroundAttackPlaying
                || playingAirAttack || hostagePhase != 0 || runningHook) return;
            var controller = GetComponent<CharacterController>();
            var combat = GetComponent<CQCCombat>();
            var motor = GetComponent<ThirdPersonController>();
            if (!controller || !controller.isGrounded || (combat && (combat.IsBusy || combat.IsHolding))
                || (motor && motor.IsLandingFromAirAttack) || (posture && (posture.IsProne || posture.IsDiving))) return;
            if (posture && posture.CurrentStance != CQCPosture.Stance.Standing
                && !posture.TryChangeStance(CQCPosture.Stance.Standing)) return;
            if (inputs != null && inputs.move.sqrMagnitude > 0.001f)
            {
                Vector3 forward = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized : transform.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 direction = forward * inputs.move.y + right * inputs.move.x;
                if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            }
            IsRolling = true; rollElapsed = 0f; finishRollAfterEvaluation = false;
            rollDirection = transform.forward;
            rollDuration = rollClip.length / Mathf.Max(1f, RollPlaybackSpeed);
            float incomingSpeed = Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude;
            rollTravelSpeed = Mathf.Max(RollDistance / Mathf.Max(0.1f, rollDuration), incomingSpeed);
            rollPlayable.SetTime(0f); rollPlayable.SetDuration(rollClip.length); rollPlayable.SetDone(false); rollPlayable.SetSpeed(RollPlaybackSpeed);
            SetExclusiveWeight(14);
        }
        private AnimationClip sprintClip, crouchSprintClip;
        private AnimationClipPlayable sprintPlayable, crouchSprintPlayable;
        private CQCSprintPhase sprintPhase;
        private float crouchSprintElapsed;
        public bool IsCrouchSprintTransition { get; private set; }
        public bool HasSprintClips => graph.IsValid() && sprintClip && crouchSprintClip;
        public void PlayCrouchSprintTransition()
        {
            if (!HasSprintClips) return;
            IsCrouchSprintTransition = true;
            IsTransitioning = true;
            crouchSprintElapsed = 0f;
            crouchSprintPlayable.SetTime(0f);
            crouchSprintPlayable.SetSpeed(1f);
            SetExclusiveWeight(12);
        }
        private AnimationClipPlayable enter, exit, walk;
        private AnimationClip enterClip, exitClip, walkClip;
        private CQCPosture posture;
        private StarterAssetsInputs inputs;
        private Animator animator;
        private int transitionInput;
        private float transitionElapsed, transitionDuration;
        private float walkWeight;
        private AnimationClip airAttackClip;
        private AnimationClipPlayable airAttack;
        private bool playingAirAttack;
        private float airElapsed;
        private bool stopAirAfterEvaluation;
        public bool IsAirAttackPlaying => playingAirAttack;
        public float AirAttackProgress => playingAirAttack && airAttackClip ? Mathf.Clamp01(airElapsed / airAttackClip.length) : 0f;
        private readonly AnimationClip[] groundClips = new AnimationClip[4];
        private readonly AnimationClipPlayable[] groundPlayables = new AnimationClipPlayable[4];
        private int groundAttackIndex = -1;
        private float groundElapsed;
        private bool finishGroundAfterEvaluation;
        public bool IsGroundAttackPlaying => groundAttackIndex >= 0;

        public bool TryPlayGroundAttack(int index, out float duration)
        {
            duration = 0f;
            index = Mathf.Clamp(index, 0, 3);
            if (!enabled || !graph.IsValid() || !groundClips[index]) return false;
            duration = groundClips[index].length;
            groundAttackIndex = index;
            runningHook = false;
            armLayers.SetInputWeight(2, 0f);
            playingAirAttack = false;
            stopAirAfterEvaluation = false;
            groundElapsed = 0f;
            finishGroundAfterEvaluation = false;
            groundPlayables[index].SetTime(0f);
            groundPlayables[index].SetDuration(duration);
            groundPlayables[index].SetDone(false);
            groundPlayables[index].SetSpeed(1f);
            SetExclusiveWeight(8 + index);
            if (index < 3 && inputs != null && inputs.move.sqrMagnitude > 0.001f && movingPunchMixer.IsValid())
            {
                SetExclusiveWeight(sprintPhase && sprintPhase.IsSecondPhase && sprintClip ? 13 : 0);
                for (int i = 0; i < 3; i++) movingPunchMixer.SetInputWeight(i, i == index ? 1f : 0f);
                movingPunches[index].SetTime(0f);
                movingPunches[index].SetSpeed(1f);
                armLayers.SetInputWeight(3, 1f);
                movingPunchActive = true;
            }
            return true;
        }

        public void StopGroundAttack()
        { if (IsGroundAttackPlaying) finishGroundAfterEvaluation = true; if (runningHook) finishHookAfterEvaluation = true; }

        private void SetExclusiveWeight(int input)
        {
            for (int i = 0; i < 15; i++) mixer.SetInputWeight(i, i == input ? 1f : 0f);
            PoseWeight = input == 0 ? 0f : 1f;
            if (input >= 4 && armLayers.IsValid()) { armLayers.SetInputWeight(1, 0f); crouchArmWeight = 0f; }
        }
        private AnimationClip hostageTakeClip, hostageIdleClip, hostageReleaseClip;
        private AnimationClipPlayable hostageTake, hostageIdle, hostageRelease;
        private int hostagePhase;
        private float hostageElapsed, hostageDuration;
        public bool IsHostageSequenceActive => hostagePhase != 0;
        public bool IsHostageReleasing => hostagePhase == 3;
        public float HostageTakeLength => hostageTakeClip ? hostageTakeClip.length : 0f;
        public float HostageReleaseLength => hostageReleaseClip ? hostageReleaseClip.length : 0f;
        public bool HasHostageClips => graph.IsValid() && hostageTakeClip && hostageIdleClip && hostageReleaseClip;

        public bool BeginHostage(float duration)
        {
            if (!HasHostageClips) return false;
            runningHook = false;
            if (armLayers.IsValid()) armLayers.SetInputWeight(2, 0f);
            EndAirAttackNow();
            groundAttackIndex = -1;
            finishGroundAfterEvaluation = false;
            IsTransitioning = false;
            hostagePhase = 1;
            hostageElapsed = 0f;
            hostageDuration = Mathf.Max(0.1f, duration);
            hostageTake.SetTime(0f);
            hostageTake.SetSpeed(hostageTakeClip.length / hostageDuration);
            SetHostageWeights(5);
            return true;
        }

        public void ReleaseHostage(float duration)
        {
            if (!HasHostageClips) return;
            hostagePhase = 3; hostageElapsed = 0f; hostageDuration = Mathf.Max(0.1f, duration);
            hostageRelease.SetTime(0f);
            hostageRelease.SetSpeed(hostageReleaseClip.length / hostageDuration);
            SetHostageWeights(7);
        }

        public void CancelHostage()
        {
            hostagePhase = 0;
            if (mixer.IsValid())
            {
                mixer.SetInputWeight(0, 1f);
                for (int i = 1; i < 15; i++) mixer.SetInputWeight(i, 0f);
            }
            PoseWeight = 0f;
        }

        private void SetHostageWeights(int input)
        {
            for (int i = 0; i < 15; i++) mixer.SetInputWeight(i, i == input ? 1f : 0f);
            PoseWeight = 1f;
            if (armLayers.IsValid()) { armLayers.SetInputWeight(1, 0f); crouchArmWeight = 0f; }
        }

        public bool TryPlayAirAttack(out float duration)
        {
            duration = 0f;
            if (!enabled || !graph.IsValid() || !airAttackClip || !airAttack.IsValid()) return false;
            runningHook = false;
            if (armLayers.IsValid()) armLayers.SetInputWeight(2, 0f);
            duration = airAttackClip.length;
            airAttack.SetTime(0f);
            airAttack.SetSpeed(1f);
            airAttack.SetDuration(airAttackClip.length);
            airAttack.SetDone(false);
            airElapsed = 0f;
            playingAirAttack = true;
            groundAttackIndex = -1;
            finishGroundAfterEvaluation = false;
            stopAirAfterEvaluation = false;
            IsTransitioning = false;
            SetExclusiveWeight(4);
            PoseWeight = 1f;
            return true;
        }

        public void StopAirAttack()
        {
            if (playingAirAttack) { stopAirAfterEvaluation = true; return; }
            EndAirAttackNow();
        }

        private void EndAirAttackNow()
        {
            playingAirAttack = false;
            stopAirAfterEvaluation = false;
            if (mixer.IsValid())
            {
                mixer.SetInputWeight(0, 1f);
                for (int i = 1; i < 15; i++) mixer.SetInputWeight(i, 0f);
            }
            PoseWeight = 0f;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            posture = GetComponent<CQCPosture>();
            inputs = GetComponent<StarterAssetsInputs>();
            rollClip = LoadClip("Stand To Roll");
            hookClip = LoadClip("Hook Punch");
            sprintClip = LoadClip("Sprint");
            crouchSprintClip = LoadClip("Crouched To Sprinting");
            enterClip = LoadClip("Standing To Crouched");
            exitClip = LoadClip("Crouch To Stand");
            walkClip = LoadClip("Crouched Walking");
            airAttackClip = LoadClip("Flying Knee Punch Combo");
            hostageTakeClip = LoadClip("Taken Hostage - Villain");
            hostageIdleClip = LoadClip("Hostage Situation Idle - Villain");
            hostageReleaseClip = LoadClip("Release Hostage - Villain");
            string[] groundNames = { "Punching", "Punch Combo", "Combo Punch", "Mma Kick" };
            for (int i = 0; i < 4; i++) groundClips[i] = LoadClip(groundNames[i]);
            if (!animator.runtimeAnimatorController || !enterClip || !walkClip)
            {
                Debug.LogWarning("Crouch clips are not ready. Run Tools > CQC > Prepare Crouch Animations. The existing crouch remains available.", this);
                enabled = false;
                return;
            }
            graph = PlayableGraph.Create("CQC Crouch Clips");
            // OnAnimatorMove in ThirdPersonController consumes motion only for the flying attack.
            animator.applyRootMotion = true;
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            baseController = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            enter = AnimationClipPlayable.Create(graph, enterClip);
            exit = AnimationClipPlayable.Create(graph, exitClip ? exitClip : enterClip);
            walk = AnimationClipPlayable.Create(graph, walkClip);
            enter.SetApplyFootIK(true); exit.SetApplyFootIK(true); walk.SetApplyFootIK(true);
            mixer = AnimationMixerPlayable.Create(graph, 15);
            if (rollClip)
            {
                rollPlayable = AnimationClipPlayable.Create(graph, rollClip);
                rollPlayable.SetApplyFootIK(false);
                graph.Connect(rollPlayable, 0, mixer, 14);
            }
            if (sprintClip && crouchSprintClip)
            {
                sprintPlayable = AnimationClipPlayable.Create(graph, sprintClip);
                crouchSprintPlayable = AnimationClipPlayable.Create(graph, crouchSprintClip);
                graph.Connect(crouchSprintPlayable, 0, mixer, 12);
                graph.Connect(sprintPlayable, 0, mixer, 13);
            }
            for (int i = 0; i < 4; i++)
            {
                if (!groundClips[i]) continue;
                groundPlayables[i] = AnimationClipPlayable.Create(graph, groundClips[i]);
                groundPlayables[i].SetApplyFootIK(false);
                graph.Connect(groundPlayables[i], 0, mixer, 8 + i);
            }
            graph.Connect(baseController, 0, mixer, 0);
            graph.Connect(enter, 0, mixer, 1);
            graph.Connect(exit, 0, mixer, 2);
            graph.Connect(walk, 0, mixer, 3);
            if (hostageTakeClip && hostageIdleClip && hostageReleaseClip)
            {
                hostageTake = AnimationClipPlayable.Create(graph, hostageTakeClip);
                hostageIdle = AnimationClipPlayable.Create(graph, hostageIdleClip);
                hostageRelease = AnimationClipPlayable.Create(graph, hostageReleaseClip);
                graph.Connect(hostageTake, 0, mixer, 5);
                graph.Connect(hostageIdle, 0, mixer, 6);
                graph.Connect(hostageRelease, 0, mixer, 7);
            }
            if (airAttackClip)
            {
                airAttack = AnimationClipPlayable.Create(graph, airAttackClip);
                airAttack.SetApplyFootIK(false);
                graph.Connect(airAttack, 0, mixer, 4);
            }
            mixer.SetInputWeight(0, 1f);
            // Use the same clip's arms over the existing normal crouch-walk legs.
            crouchArms = AnimationClipPlayable.Create(graph, walkClip);
            crouchArms.SetApplyFootIK(false);
            armLayers = AnimationLayerMixerPlayable.Create(graph, 4);
            graph.Connect(mixer, 0, armLayers, 0);
            graph.Connect(crouchArms, 0, armLayers, 1);
            armLayers.SetInputWeight(0, 1f);
            crouchArmMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                crouchArmMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            crouchArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            crouchArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            crouchArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            crouchArmMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            armLayers.SetLayerMaskFromAvatarMask(1, crouchArmMask);
            hookMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) hookMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            hookMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            hookMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            hookMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            hookMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            hookMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            hookMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            movingPunchMixer = AnimationMixerPlayable.Create(graph, 3);
            for (int i = 0; i < 3; i++)
            {
                if (!groundClips[i]) continue;
                movingPunches[i] = AnimationClipPlayable.Create(graph, groundClips[i]);
                graph.Connect(movingPunches[i], 0, movingPunchMixer, i);
            }
            graph.Connect(movingPunchMixer, 0, armLayers, 3);
            armLayers.SetLayerMaskFromAvatarMask(3, hookMask);
            if (hookClip)
            {
                hookPlayable = AnimationClipPlayable.Create(graph, hookClip);
                hookPlayable.SetApplyFootIK(false);
                graph.Connect(hookPlayable, 0, armLayers, 2);
                armLayers.SetLayerMaskFromAvatarMask(2, hookMask);
            }
            var output = AnimationPlayableOutput.Create(graph, "Robot Crouch", animator);
            output.SetSourcePlayable(armLayers);
            weaponOutput = output;
            graph.Play();
        }

        private AnimationClip LoadClip(string name)
        {
            foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>("CQCAnimations/" + name))
                if (!clip.name.StartsWith("__preview__") && clip.isHumanMotion) return clip;
            return null;
        }

        public void SetControllerFloat(int id, float value)
        { if (baseController.IsValid()) baseController.SetFloat(id, value); }
        public void SetControllerBool(int id, bool value)
        { if (baseController.IsValid()) baseController.SetBool(id, value); }

        public void BeginTransition(CQCPosture.Stance previous, CQCPosture.Stance next)
        {
            if (!enabled || !graph.IsValid()) return;
            IsCrouchSprintTransition = false;
            if (previous == CQCPosture.Stance.Standing && next == CQCPosture.Stance.Crouching)
            { transitionInput = 1; transitionDuration = enterClip.length; enter.SetTime(0); enter.SetSpeed(1); }
            else if (previous == CQCPosture.Stance.Crouching && next == CQCPosture.Stance.Standing)
            {
                IsTransitioning = false;
                transitionInput = 0;
                walkWeight = 0f;
                PoseWeight = 0f;
                mixer.SetInputWeight(0, 1f);
                mixer.SetInputWeight(1, 0f);
                mixer.SetInputWeight(2, 0f);
                mixer.SetInputWeight(3, 0f);
                return;
            }
            else { IsTransitioning = false; transitionInput = 0; return; }
            transitionElapsed = 0f;
            IsTransitioning = true;
        }

        private void Update()
        {
            if (!graph.IsValid()) return;
            movingPunchActive = false;
            armLayers.SetInputWeight(3, 0f);
            if (!posture) posture = GetComponent<CQCPosture>();
            bool rollPressed = false;
#if ENABLE_INPUT_SYSTEM
            rollPressed = Keyboard.current != null && Keyboard.current.leftAltKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            rollPressed = Input.GetKeyDown(KeyCode.LeftAlt);
#endif
            if (Application.isFocused && rollPressed) TryStartRoll();
            if (IsRolling)
            {
                rollElapsed += Time.deltaTime;
                SetExclusiveWeight(14);
                if (rollElapsed >= rollDuration) finishRollAfterEvaluation = true;
                return;
            }
            if (posture && posture.IsProne)
            {
                walkWeight = crouchArmWeight = 0f;
                armLayers.SetInputWeight(1, 0f);
                SetExclusiveWeight(0);
                return;
            }
            if (runningHook)
            {
                hookElapsed += Time.deltaTime;
                float weight = Mathf.Min(Mathf.Clamp01(hookElapsed / 0.06f), Mathf.Clamp01((hookDuration - hookElapsed) / 0.1f));
                armLayers.SetInputWeight(2, weight);
                if (hookElapsed >= hookDuration) finishHookAfterEvaluation = true;
            }
            if (!sprintPhase) sprintPhase = GetComponent<CQCSprintPhase>();
            if (!posture) posture = GetComponent<CQCPosture>();
            bool normalCrouchWalk = posture && posture.CurrentStance == CQCPosture.Stance.Crouching
                && inputs != null && inputs.move.sqrMagnitude > 0.001f && !inputs.sprint
                && !UseWalkingClipWithoutShift && !IsTransitioning && !IsGroundAttackPlaying
                && !playingAirAttack && hostagePhase == 0;
            var combat = GetComponent<CQCCombat>();
            bool holding = combat && combat.IsHolding;
            bool action = IsTransitioning || IsGroundAttackPlaying || playingAirAttack || hostagePhase != 0 || holding;
            crouchArmWeight = action ? 0f : Mathf.MoveTowards(crouchArmWeight, normalCrouchWalk ? 1f : 0f, Time.deltaTime * 10f);
            crouchArms.SetTime(walk.GetTime());
            crouchArms.SetSpeed(1f);
            armLayers.SetInputWeight(1, crouchArmWeight);
            if (IsGroundAttackPlaying)
            {
                groundElapsed += Time.deltaTime;
                if (groundAttackIndex < 3 && inputs != null && inputs.move.sqrMagnitude > 0.001f)
                {
                    int locomotion = sprintPhase && sprintPhase.IsSecondPhase && sprintClip ? 13 : 0;
                    SetExclusiveWeight(locomotion);
                    for (int i = 0; i < 3; i++) movingPunchMixer.SetInputWeight(i, i == groundAttackIndex ? 1f : 0f);
                    movingPunches[groundAttackIndex].SetTime(groundPlayables[groundAttackIndex].GetTime());
                    movingPunches[groundAttackIndex].SetSpeed(1f);
                    float blend = Mathf.Min(Mathf.Clamp01(groundElapsed / 0.06f), Mathf.Clamp01((groundClips[groundAttackIndex].length - groundElapsed) / 0.1f));
                    armLayers.SetInputWeight(3, blend);
                    movingPunchActive = true;
                }
                else SetExclusiveWeight(8 + groundAttackIndex);
                if (groundElapsed >= groundClips[groundAttackIndex].length) finishGroundAfterEvaluation = true;
                return;
            }
            if (hostagePhase != 0)
            {
                hostageElapsed += Time.deltaTime;
                if (hostagePhase == 1 && hostageElapsed >= hostageDuration)
                {
                    // Keep the exact completed grab pose instead of switching to a moving idle loop.
                    hostagePhase = 2;
                    hostageTake.SetTime(Mathf.Max(0f, hostageTakeClip.length - 0.001f));
                    hostageTake.SetSpeed(0f);
                    SetHostageWeights(5);
                }
                else if (hostagePhase == 3 && hostageElapsed >= hostageDuration)
                { CancelHostage(); }
                if (hostagePhase != 0) return;
            }
            if (playingAirAttack)
            {
                airElapsed += Time.deltaTime;
                mixer.SetInputWeight(0, 0f);
                mixer.SetInputWeight(1, 0f); mixer.SetInputWeight(2, 0f); mixer.SetInputWeight(3, 0f);
                mixer.SetInputWeight(4, 1f);
                PoseWeight = 1f;
                if (airElapsed >= airAttackClip.length) stopAirAfterEvaluation = true;
                return;
            }
            if (IsCrouchSprintTransition)
            {
                crouchSprintElapsed += Time.deltaTime;
                if (crouchSprintElapsed < crouchSprintClip.length) { SetExclusiveWeight(12); return; }
                IsCrouchSprintTransition = false;
                IsTransitioning = false;
            }
            if (!holding && HasSprintClips && sprintPhase && sprintPhase.IsSecondPhase && inputs != null && inputs.move.sqrMagnitude > 0.001f
                && GetComponent<CharacterController>().isGrounded
                && (!posture || posture.CurrentStance == CQCPosture.Stance.Standing))
            {
                if (sprintPlayable.GetTime() >= sprintClip.length) sprintPlayable.SetTime(sprintPlayable.GetTime() % sprintClip.length);
                SetExclusiveWeight(13);
                return;
            }
            mixer.SetInputWeight(12, 0f); mixer.SetInputWeight(13, 0f);
            if (!posture) posture = GetComponent<CQCPosture>();
            float moving = inputs != null ? inputs.move.magnitude : 0f;
            bool useWalk = posture && posture.CurrentStance == CQCPosture.Stance.Crouching
                && moving > 0.01f && inputs != null && (inputs.sprint || UseWalkingClipWithoutShift);
            if (IsTransitioning)
            {
                transitionElapsed += Time.deltaTime;
                if (transitionElapsed >= Mathf.Max(0.1f, transitionDuration)) IsTransitioning = false;
            }
            float transitionWeight = IsTransitioning
                ? Mathf.Min(Mathf.Clamp01(transitionElapsed / 0.08f), Mathf.Clamp01((transitionDuration - transitionElapsed) / 0.08f)) : 0f;
            walkWeight = Mathf.MoveTowards(walkWeight, useWalk && !IsTransitioning ? 1f : 0f, Time.deltaTime * 10f);
            if (walkClip.length > 0f && walk.GetTime() >= walkClip.length)
                walk.SetTime(walk.GetTime() % walkClip.length);
            walk.SetSpeed(inputs != null && inputs.sprint ? CrouchRunPlaybackSpeed : 1f);
            float walkContribution = walkWeight * (1f - transitionWeight);
            PoseWeight = transitionWeight + walkContribution;
            mixer.SetInputWeight(0, 1f - PoseWeight);
            mixer.SetInputWeight(1, transitionInput == 1 ? transitionWeight : 0f);
            mixer.SetInputWeight(2, transitionInput == 2 ? transitionWeight : 0f);
            mixer.SetInputWeight(3, walkContribution);
        }

        private void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            IsTransitioning = false; PoseWeight = 0f;
            playingAirAttack = false;
            hostagePhase = 0;
            groundAttackIndex = -1;
            IsCrouchSprintTransition = false;
            crouchArmWeight = 0f;
            if (crouchArmMask) Destroy(crouchArmMask);
            if (hookMask) Destroy(hookMask);
            runningHook = false;
            IsRolling = false;
            movingPunchActive = false;
        }
        private void LateUpdate()
        {
            // Consume the final frame's root delta before returning to locomotion.
            if (stopAirAfterEvaluation)
            {
                var movement = GetComponent<ThirdPersonController>();
                if (movement) movement.FinishAirAttackLanding();
                EndAirAttackNow();
            }
            if (finishGroundAfterEvaluation && IsGroundAttackPlaying)
            {
                groundAttackIndex = -1;
                movingPunchActive = false;
                armLayers.SetInputWeight(3, 0f);
                finishGroundAfterEvaluation = false;
                if (mixer.IsValid()) SetExclusiveWeight(0);
            }
            if (finishHookAfterEvaluation && runningHook)
            { runningHook = false; finishHookAfterEvaluation = false; armLayers.SetInputWeight(2, 0f); }
            if (finishRollAfterEvaluation && IsRolling)
            { IsRolling = false; finishRollAfterEvaluation = false; SetExclusiveWeight(0); }
        }
        private void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}





