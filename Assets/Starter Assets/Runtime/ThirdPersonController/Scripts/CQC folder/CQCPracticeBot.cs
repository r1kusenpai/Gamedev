using UnityEngine;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController), typeof(CQCTrainingTarget))]
    public class CQCPracticeBot : MonoBehaviour
    {
        public Transform Player;
        public bool FollowPlayer = true;
        public float MoveSpeed = 0.9f;
        public float StopDistance = 1.1f;
        public Transform LeftLeg, RightLeg;
        private CharacterController controller;
        private CQCTrainingTarget target;
        private float verticalSpeed, stepTime;
        private Animator animator;
        private Vector3 throwVelocity;
        private CQCHostageVictimClips hostageClips;
        public void Push(Vector3 direction) { throwVelocity = direction * 3.5f; verticalSpeed = 2.5f; }
        public void ClearThrowVelocity() { throwVelocity = Vector3.zero; verticalSpeed = -2f; }
        private void Awake()
        { controller = GetComponent<CharacterController>(); target = GetComponent<CQCTrainingTarget>(); animator = GetComponentInChildren<Animator>(); }
        private void Start()
        {
            if (animator)
            {
                // Imported locomotion clips carry sound events; the practice bot has no player audio receiver.
                if (!animator.GetComponent<ThirdPersonController>() && !animator.GetComponent<PracticeBotAnimationEvents>())
                    animator.gameObject.AddComponent<PracticeBotAnimationEvents>();
                hostageClips = animator.GetComponent<CQCHostageVictimClips>();
                if (!hostageClips) hostageClips = animator.gameObject.AddComponent<CQCHostageVictimClips>();
            }
        }

        private void Update()
        {
            Vector3 horizontal = Vector3.zero;
            bool moving = false;
            if (!hostageClips && animator) hostageClips = animator.GetComponent<CQCHostageVictimClips>();
            if (target.IsHeld || (hostageClips && hostageClips.IsSequenceActive))
            {
                if (animator) { animator.SetFloat("Speed", 0f); animator.SetFloat("MotionSpeed", 0f); }
                if (hostageClips) { hostageClips.SetFloat("Speed", 0f); hostageClips.SetFloat("MotionSpeed", 0f); }
                return;
            }
            if (Player && !target.IsDown)
            {
                Vector3 direction = Vector3.ProjectOnPlane(Player.position - transform.position, Vector3.up);
                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-8f * Time.deltaTime));
                    if (FollowPlayer && direction.magnitude > StopDistance && Time.time >= target.StunnedUntil)
                    { horizontal = direction.normalized * MoveSpeed; moving = true; }
                }
            }
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(-30f, verticalSpeed - 15f * Time.deltaTime);
            controller.Move((horizontal + throwVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
            throwVelocity = Vector3.Lerp(throwVelocity, Vector3.zero, 1f - Mathf.Exp(-5f * Time.deltaTime));
            if (animator)
            {
                animator.SetBool("Grounded", controller.isGrounded);
                animator.SetBool("Jump", false);
                animator.SetBool("FreeFall", false);
                animator.SetFloat("Speed", moving ? MoveSpeed : 0f);
                animator.SetFloat("MotionSpeed", moving ? 1f : 0f);
                if (hostageClips)
                {
                    hostageClips.SetBool("Grounded", controller.isGrounded);
                    hostageClips.SetBool("Jump", false);
                    hostageClips.SetBool("FreeFall", false);
                    hostageClips.SetFloat("Speed", moving ? MoveSpeed : 0f);
                    hostageClips.SetFloat("MotionSpeed", moving ? 1f : 0f);
                }
            }
            if (moving) stepTime += Time.deltaTime * 7f;
            float swing = moving ? Mathf.Sin(stepTime) * 22f : 0f;
            if (LeftLeg) LeftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (RightLeg) RightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        }
    }
}
