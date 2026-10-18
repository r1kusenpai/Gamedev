using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;
        private CQCPosture _posture;
        private CQCCombat _combat;
        private CQCCrouchClips _crouchClips;
        private CQCSprintPhase _sprintPhase;
        public bool IsLandingFromAirAttack { get; private set; }
        public void FinishAirAttackLanding()
        {
            IsLandingFromAirAttack = _controller != null && !_controller.isGrounded;
            _verticalVelocity = -4f;
        }

        public void ResetAfterDomainTravel(float facing)
        {
            _verticalVelocity = -2f;
            _speed = 0f;
            _animationBlend = 0f;
            _rotationVelocity = 0f;
            _targetRotation = facing;
            _cinemachineTargetYaw = facing;
            _cinemachineTargetPitch = 10f;
            IsLandingFromAirAttack = false;
            if (CinemachineCameraTarget) CinemachineCameraTarget.transform.rotation = Quaternion.Euler(10f, facing, 0f);
        }

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }


        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _posture = GetComponent<CQCPosture>();
            _combat = GetComponent<CQCCombat>();
            if (_posture == null) _posture = gameObject.AddComponent<CQCPosture>();
            if (GetComponent<CQCPracticeBotSpawner>() == null) gameObject.AddComponent<CQCPracticeBotSpawner>();
            _crouchClips = GetComponent<CQCCrouchClips>();
            if (_crouchClips == null) _crouchClips = gameObject.AddComponent<CQCCrouchClips>();
            _sprintPhase = GetComponent<CQCSprintPhase>();
            if (_sprintPhase == null) _sprintPhase = gameObject.AddComponent<CQCSprintPhase>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            GroundedCheck();
            if (TryGetComponent<TransportPassenger>(out var passenger) && passenger.IsSupported) Grounded = true;
            JumpAndGravity();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void SetAnimationFloat(int id, float value)
        {
            _animator.SetFloat(id, value);
            if (_crouchClips != null && _crouchClips.enabled) _crouchClips.SetControllerFloat(id, value);
        }

        private void SetAnimationBool(int id, bool value)
        {
            _animator.SetBool(id, value);
            if (_crouchClips != null && _crouchClips.enabled) _crouchClips.SetControllerBool(id, value);
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                SetAnimationBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            if (TryGetComponent<WeaponController>(out var weapon) && weapon.DodgeActive)
            { _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime)); return; }
            if (weapon && weapon.OriginalSwordAttackActive)
            { _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime)); return; }
            if (_crouchClips != null && _crouchClips.IsRolling)
            {
                // Dodge travel is gameplay movement, not a mesh offset that resets at clip end.
                _controller.Move(_crouchClips.ConsumeRollDisplacement(Time.deltaTime) + Vector3.up * (_verticalVelocity * Time.deltaTime));
                return;
            }
            if (IsLandingFromAirAttack && (_crouchClips == null || !_crouchClips.IsAirAttackPlaying))
            {
                if (!_controller.isGrounded)
                {
                    _speed = 0f;
                    _controller.Move(Vector3.up * (Mathf.Min(-4f, _verticalVelocity) * Time.deltaTime));
                    return;
                }
                IsLandingFromAirAttack = false;
            }
            if (_crouchClips != null && _crouchClips.IsAirAttackPlaying)
            {
                _speed = 0f;
                _animationBlend = 0f;
                if (_hasAnimator) { SetAnimationFloat(_animIDSpeed, 0f); SetAnimationFloat(_animIDMotionSpeed, 0f); }
                return;
            }
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (_sprintPhase != null && _sprintPhase.IsSecondPhase) targetSpeed = _sprintPhase.SecondPhaseSpeed;
            if (_posture != null && _posture.enabled)
            {
                if (_posture.CurrentStance == CQCPosture.Stance.Crouching) targetSpeed = _input.sprint ? _posture.CrouchSprintSpeed : _posture.CrouchSpeed;
            }

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            if (_combat != null && _combat.IsHolding) targetSpeed = 0f;
            if (_crouchClips != null && _crouchClips.IsTransitioning) targetSpeed = 0f;
            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            if (_combat != null && _combat.IsHolding) _speed = 0f;
            if (_crouchClips != null && _crouchClips.IsTransitioning) _speed = 0f;
            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero && (_combat == null || !_combat.IsHolding)
                && (_crouchClips == null || !_crouchClips.IsRolling))
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                SetAnimationFloat(_animIDSpeed, _animationBlend);
                SetAnimationFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (_crouchClips != null && _crouchClips.IsAirAttackPlaying)
            {
                // The attack supplies its own vertical arc; resume normal gravity after it finishes.
                _verticalVelocity = -2f;
                _input.jump = false;
                return;
            }
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    SetAnimationBool(_animIDJump, false);
                    SetAnimationBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f && (_posture == null || !_posture.enabled || _posture.CanJump) && (_combat == null || !_combat.IsHolding))
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        SetAnimationBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        SetAnimationBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity > -_terminalVelocity)
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity + Gravity * Time.deltaTime, -_terminalVelocity);
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnAnimatorMove()
        {
            if (TryGetComponent<WeaponController>(out var weapon) && weapon.Equipped)
            {
                if (weapon.UseActionRootMotion && _controller != null && _controller.enabled)
                { Vector3 travel = weapon.FilterActionTravel(_animator.deltaPosition); travel.y = 0; if (travel.sqrMagnitude < 1f) _controller.Move(travel); }
                return;
            }
            // Rolling already moves the collider in Move(); discard the clip's root travel to avoid rewinding.
            if (_crouchClips != null && _crouchClips.IsRolling) return;
            if (_controller == null || !_controller.enabled || _animator == null
                || _crouchClips == null || (!_crouchClips.IsAirAttackPlaying && !_crouchClips.IsGroundAttackPlaying
                    && !_crouchClips.IsCrouchSprintTransition && !_crouchClips.IsRolling)) return;
            Vector3 delta = _animator.deltaPosition;
            if (!_crouchClips.IsAirAttackPlaying) delta.y = 0f;
            else if (_crouchClips.AirAttackProgress >= 0.7f && _controller.isGrounded) delta.y = Mathf.Min(0f, delta.y);
            if (float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsNaN(delta.z)
                || float.IsInfinity(delta.x) || float.IsInfinity(delta.y) || float.IsInfinity(delta.z)) return;
            _controller.Move(delta);
            if (_crouchClips.IsAirAttackPlaying && _crouchClips.AirAttackProgress >= 0.7f)
            {
                // Settle the collider onto nearby ground during the last part of the move.
                Vector3 foot = transform.TransformPoint(_controller.center)
                    - transform.up * (_controller.height * Mathf.Abs(transform.lossyScale.y) * 0.5f);
                float distance = float.PositiveInfinity;
                foreach (RaycastHit hit in Physics.RaycastAll(foot + Vector3.up * 0.2f, Vector3.down, 3.3f,
                    GroundLayers.value != 0 ? GroundLayers.value : ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<CQCTrainingTarget>()) continue;
                    if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f) continue;
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.2f));
                }
                if (!float.IsInfinity(distance))
                    _controller.Move(Vector3.down * Mathf.Min(distance + _controller.skinWidth, 8f * Time.deltaTime));
            }
            if (_combat != null && _crouchClips.IsAirAttackPlaying) _combat.CheckAirContactAfterMovement();
            // PlayerCameraRoot is a child of this transform, so the camera follows the real travel.
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
}








