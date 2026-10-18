using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [DefaultExecutionOrder(-50)]
    public class CQCSprintPhase : MonoBehaviour
    {
        public float DoubleTapWindow = 0.35f;
        public float SecondPhaseSpeed = 7.5f;
        public bool IsSecondPhase { get; private set; }
        private float lastTap = float.NegativeInfinity;
        private StarterAssetsInputs inputs;
        private CQCPosture posture;
        private CQCCrouchClips clips;
        private void Awake()
        { inputs = GetComponent<StarterAssetsInputs>(); posture = GetComponent<CQCPosture>(); clips = GetComponent<CQCCrouchClips>(); }
        private void Update()
        {
            if (!Application.isFocused || Time.timeScale <= 0f) return;
            if (!clips) clips = GetComponent<CQCCrouchClips>();
            bool pressed = false, held = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pressed = Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame;
                held = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            held = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
            bool moving = inputs != null && inputs.move.sqrMagnitude > 0.001f;
            if ((!held || !moving) && (!clips || !clips.IsCrouchSprintTransition)) IsSecondPhase = false;
            if (!pressed) return;
            bool doubleTap = Time.time - lastTap <= Mathf.Max(0.1f, DoubleTapWindow);
            lastTap = Time.time;
            if (!doubleTap || !moving || !clips || !clips.HasSprintClips) return;
            var combat = GetComponent<CQCCombat>();
            var motor = GetComponent<ThirdPersonController>();
            var controller = GetComponent<CharacterController>();
            if ((combat && (combat.IsBusy || combat.IsHolding)) || (motor && motor.IsLandingFromAirAttack)
                || (controller && !controller.isGrounded)) return;
            if (posture && posture.CurrentStance == CQCPosture.Stance.Crouching)
            {
                if (!posture.TryChangeStance(CQCPosture.Stance.Standing)) return;
                clips.PlayCrouchSprintTransition();
            }
            IsSecondPhase = true;
            lastTap = float.NegativeInfinity;
        }
    }
}
