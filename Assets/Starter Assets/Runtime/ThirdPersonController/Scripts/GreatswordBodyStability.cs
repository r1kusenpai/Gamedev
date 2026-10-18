using UnityEngine;

namespace StarterAssets
{
    // Limit the authored torso before crouch leg correction and weapon attachment.
    [DefaultExecutionOrder(-110)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator),typeof(WeaponController))]
    public sealed class GreatswordBodyStability : MonoBehaviour
    {
        [Range(0f,1f)] public float BodyAnimationWeight=.65f;
        [Range(5f,35f)] public float SpineLimit=14f,ChestLimit=14f,UpperChestLimit=10f;
        [SerializeField] bool neutralPoseSaved;
        [SerializeField] Quaternion spineRest=Quaternion.identity,chestRest=Quaternion.identity,upperRest=Quaternion.identity;
        Transform spine,chest,upper;WeaponController weapon;CQCCrouchClips source;
        void FindBones()
        {
            var animator=GetComponent<Animator>();if(!animator||!animator.isHuman)return;
            spine=animator.GetBoneTransform(HumanBodyBones.Spine);chest=animator.GetBoneTransform(HumanBodyBones.Chest);upper=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            weapon=GetComponent<WeaponController>();source=GetComponent<CQCCrouchClips>();
        }
        void Awake(){FindBones();if(!neutralPoseSaved)CaptureNeutralPose();}
        public bool CaptureNeutralPose()
        {
            FindBones();if(!spine)return false;
            spineRest=spine.localRotation;if(chest)chestRest=chest.localRotation;if(upper)upperRest=upper.localRotation;
            neutralPoseSaved=true;return true;
        }
        void LateUpdate(){StabilizeBody();}
        public void StabilizeBody()
        {
            if(!neutralPoseSaved)return;if(!spine||!weapon)FindBones();
            if(!weapon||weapon.CurrentWeapon!=WeaponController.Weapon.Greatsword)return;
            if(weapon.UsesCopiedSwordAttacks || weapon.UsesNormalWeaponRunningBody)return;
            if(!source)source=GetComponent<CQCCrouchClips>();
            if(source&&(source.IsRolling||source.IsTransitioning))return;
            Limit(spine,spineRest,SpineLimit);Limit(chest,chestRest,ChestLimit);Limit(upper,upperRest,UpperChestLimit);
        }
        void Limit(Transform bone,Quaternion rest,float degrees)
        {
            if(bone)bone.localRotation=Quaternion.RotateTowards(rest,Quaternion.Slerp(rest,bone.localRotation,BodyAnimationWeight),degrees);
        }
    }
}
