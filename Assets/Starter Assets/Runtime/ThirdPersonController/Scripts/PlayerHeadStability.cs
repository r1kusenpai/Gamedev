using UnityEngine;

namespace StarterAssets
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerHeadStability : MonoBehaviour
    {
        [Tooltip("0 keeps the neutral neck/head pose. 1 allows the full animation outside sword/crouch camera look.")]
        [Range(0f,1f)] public float HeadAnimationWeight = 0f;
        [Tooltip("Total head turn is twice this angle. 67.5 gives a 135 degree range, with no backward spin.")]
        [Range(0f,67.5f)] public float CameraYawLimit = 67.5f;
        [Range(0f,40f)] public float CameraPitchLimit = 25f;
        [Range(.03f,.3f)] public float CameraLookSmoothTime = .1f;
        [SerializeField] bool neutralPoseSaved;
        [SerializeField] Quaternion neckRotation = Quaternion.identity, headRotation = Quaternion.identity;
        [SerializeField] Vector3 neckPosition, headPosition;
        Transform neck, head;
        WeaponController weapon;
        CQCPosture posture;
        CQCCrouchClips rollAnimation;
        Camera viewCamera;
        StarterAssetsInputs movementInput;
        Quaternion headFacingOffset;
        bool facingSaved, looking;
        float yaw, pitch, yawVelocity, pitchVelocity;

        void Awake()
        {
            FindBones();
            weapon=GetComponent<WeaponController>();posture=GetComponent<CQCPosture>();
            movementInput=GetComponent<StarterAssetsInputs>();
            if(!neutralPoseSaved) CaptureNeutralPose();
            CacheFacing();
        }
        void FindBones()
        {
            var animator=GetComponent<Animator>();
            if(!animator || !animator.isHuman)return;
            neck=animator.GetBoneTransform(HumanBodyBones.Neck);
            head=animator.GetBoneTransform(HumanBodyBones.Head);
        }
        public bool CaptureNeutralPose()
        {
            FindBones();if(!head)return false;
            headRotation=head.localRotation;headPosition=head.localPosition;
            if(neck){neckRotation=neck.localRotation;neckPosition=neck.localPosition;}
            neutralPoseSaved=true;facingSaved=false;CacheFacing();return true;
        }
        void CacheFacing()
        {
            if(facingSaved || !head || !neutralPoseSaved)return;
            // Calibrate once from neutral bones; never derive the face axes from an attacking pose.
            var oldHead=head.localRotation;var oldNeck=neck?neck.localRotation:Quaternion.identity;
            if(neck)neck.localRotation=neckRotation;
            head.localRotation=headRotation;
            headFacingOffset=Quaternion.Inverse(transform.rotation)*head.rotation;
            head.localRotation=oldHead;if(neck)neck.localRotation=oldNeck;
            facingSaved=true;
        }
        void LateUpdate(){StabilizeHead();}
        public void StabilizeHead()
        {
            if(!rollAnimation)rollAnimation=GetComponent<CQCCrouchClips>();
            if(rollAnimation && rollAnimation.IsRolling){looking=false;return;}
            if(!neutralPoseSaved)return;
            if(!head)FindBones();if(!head)return;
            if(!weapon)weapon=GetComponent<WeaponController>();
            if(weapon&&weapon.IsPlayingDamageReaction){looking=false;return;}

            if(!posture)posture=GetComponent<CQCPosture>();
            bool cameraLook=(weapon && weapon.CurrentWeapon==WeaponController.Weapon.Sword)
                || (posture && posture.CurrentStance==CQCPosture.Stance.Crouching);
            if(cameraLook){ApplyCameraLook();return;}
            looking=false;
            float weight=Mathf.Clamp01(HeadAnimationWeight);
            if(neck)
            {
                neck.localRotation=Quaternion.Slerp(neckRotation,neck.localRotation,weight);
                neck.localPosition=Vector3.Lerp(neckPosition,neck.localPosition,weight);
            }
            head.localRotation=Quaternion.Slerp(headRotation,head.localRotation,weight);
            head.localPosition=Vector3.Lerp(headPosition,head.localPosition,weight);
        }
        void ApplyCameraLook()
        {
            CacheFacing();
            if(neck){neck.localRotation=neckRotation;neck.localPosition=neckPosition;}
            head.localRotation=headRotation;head.localPosition=headPosition;
            if(!viewCamera || !viewCamera.isActiveAndEnabled)viewCamera=Camera.main;
            if(!viewCamera || !facingSaved)return;
            // Follow the camera's aim, rather than looking backward at its position.
            // Use the character heading, not the animated chest/neck heading.
            // A sword swing must not shift the center of the allowed head range.
            Vector3 bodyForward=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
            if(bodyForward.sqrMagnitude<.001f)bodyForward=transform.forward;
            bodyForward.Normalize();
            Vector3 aim=viewCamera.transform.forward;
            Vector3 flatAim=Vector3.ProjectOnPlane(aim,Vector3.up);
            float wantedYaw=flatAim.sqrMagnitude>.001f ? Vector3.SignedAngle(bodyForward,flatAim,Vector3.up) : 0f;
            float yawLimit=Mathf.Clamp(CameraYawLimit,0f,67.5f);
            if(!movementInput)movementInput=GetComponent<StarterAssetsInputs>();
            bool forwardMovement=movementInput && movementInput.move.y>.1f
                && Mathf.Abs(movementInput.move.x)<movementInput.move.y*.75f;
            // Forward walking, sprinting and crouch walking all return to the same straight-ahead heading.
            bool idle=!movementInput || movementInput.move.sqrMagnitude<.01f;
            bool centerHead=idle || forwardMovement;
            if(centerHead)
            {
                // The neutral local pose is already restored above: leave it centered.
                // Do not reconstruct it from the camera or a cached world-facing offset.
                yaw=pitch=yawVelocity=pitchVelocity=0f;looking=false;
                return;
            }
            wantedYaw=Mathf.Clamp(wantedYaw,-yawLimit,yawLimit);
            float wantedPitch=Mathf.Clamp(Mathf.Atan2(aim.y,Mathf.Max(.001f,flatAim.magnitude))*Mathf.Rad2Deg,-CameraPitchLimit,CameraPitchLimit);
            if(centerHead)wantedPitch=0f;
            if(!looking){yaw=pitch=yawVelocity=pitchVelocity=0f;looking=true;}
            float dt=Application.isPlaying ? Mathf.Max(.0001f,Time.deltaTime) : 1f/60f;
            // Scalar smoothing crosses through the front; no Euler wrapping or accumulated 360 degree turns.
            yaw=Mathf.Clamp(Mathf.SmoothDamp(yaw,wantedYaw,ref yawVelocity,centerHead ? Mathf.Min(.06f,CameraLookSmoothTime) : CameraLookSmoothTime,Mathf.Infinity,dt),-yawLimit,yawLimit);
            pitch=Mathf.Clamp(Mathf.SmoothDamp(pitch,wantedPitch,ref pitchVelocity,CameraLookSmoothTime,Mathf.Infinity,dt),-CameraPitchLimit,CameraPitchLimit);
            if(centerHead && Mathf.Abs(yaw)<.1f){yaw=0f;yawVelocity=0f;}
            Vector3 facing=Quaternion.AngleAxis(yaw,Vector3.up)*bodyForward;
            facing=(facing+Vector3.up*Mathf.Tan(pitch*Mathf.Deg2Rad)).normalized;
            if(neck)neck.rotation=Quaternion.AngleAxis(yaw*.25f,Vector3.up)*neck.rotation;
            Quaternion target=Quaternion.LookRotation(facing,Vector3.up)*headFacingOffset;
            // Keep neck/head deflection bounded even during a deep attack or crouch lean.
            head.rotation=Quaternion.RotateTowards(head.rotation,target,75f);
        }
    }
}






