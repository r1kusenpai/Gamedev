using UnityEngine;
using UnityEngine.Serialization;

namespace StarterAssets
{
    public partial class WeaponController
    {
        [Header("Sword relaxed resting pose")]
        [FormerlySerializedAs("SwordOnShoulder")] public bool SwordRelaxedCarry = false;
        // Relative to the animated right shoulder, in character right/up/forward axes.
        [FormerlySerializedAs("SwordShoulderGrip")] public Vector3 SwordRestGrip = new Vector3(.10f, -.42f, .05f);
        [FormerlySerializedAs("SwordShoulderRest")] public Vector3 SwordRestTip = new Vector3(.28f, -.72f, .41f);
        float swordShoulderWeight;
        public Vector3 SwordNormalBladeDirection = new Vector3(.16f,-.55f,.80f);
        float normalSwordGripWeight;
        bool stableSwordGripReady;
        Vector3 stableSwordGripPosition;
        Quaternion stableSwordGripRotation;

        void CalibrateStableSwordGrip()
        {
            if(!animator)animator=GetComponent<Animator>();
            if(!animator||!animator.isHuman)return;
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var index=animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var little=animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            if(!hand||!index||!little)return;
            Vector3 knuckles=(index.position+little.position)*.5f;
            Vector3 shaft=(index.position-little.position).normalized;
            Vector3 normal=Vector3.Cross(shaft,knuckles-hand.position).normalized;
            if(normal.sqrMagnitude<.01f)return;
            stableSwordGripPosition=hand.InverseTransformPoint(Vector3.Lerp(hand.position,knuckles,.65f));
            stableSwordGripRotation=Quaternion.Inverse(hand.rotation)*Quaternion.LookRotation(normal,shaft);
            stableSwordGripReady=true;
        }

        void UpdateNormalSwordGrip()
        {
            bool normal = SwordUseNormalLocomotion && CurrentWeapon==Weapon.Sword && HasNewSwordAnimations
                && !swordSequence.Active && actionTime<=0 && movement && movement.Grounded
                && (!posture || posture.CurrentStance==CQCPosture.Stance.Standing)
                && animationSource && !animationSource.IsRolling && !animationSource.IsTransitioning;
            if(CurrentWeapon!=Weapon.Sword){normalSwordGripWeight=0;return;}
            normalSwordGripWeight=Mathf.MoveTowards(normalSwordGripWeight,normal?1:0,Time.deltaTime*10f);
            if(normalSwordGripWeight>.001f)ApplyNormalSwordGrip(normalSwordGripWeight);
        }

        void ApplyNormalSwordGrip(float weight)
        {
            if(!animator || !animator.isHuman)return;
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var index=animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var little=animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            if(!hand||!index||!little)return;
            Vector3 shaft=(index.position-little.position).normalized;
            Vector3 normal=Vector3.Cross(shaft,(index.position+little.position)*.5f-hand.position).normalized;
            Vector3 direction=CarryOffset(SwordNormalBladeDirection).normalized;
            if(normal.sqrMagnitude<.01f||direction.sqrMagnitude<.01f)return;
            Quaternion grip=(stableSwordGripReady ? hand.rotation*stableSwordGripRotation
                : Quaternion.LookRotation(normal,shaft))*Quaternion.Euler(SwordGripAdjustment);
            Quaternion correction=Quaternion.FromToRotation(grip*Vector3.up,direction);
            // Preserve the original shoulder, elbow and body animation; only turn the wrist.
            hand.rotation=Quaternion.Slerp(hand.rotation,correction*hand.rotation,Mathf.Clamp01(weight));
        }

        void UpdateSwordRestCarry()
        {
            bool carrying = SwordRelaxedCarry && !SwordUseNormalLocomotion && CurrentWeapon == Weapon.Sword && HasNewSwordAnimations
                && !swordSequence.Active && actionTime <= 0 && !charging
                && movement && movement.Grounded
                && (!posture || posture.CurrentStance == CQCPosture.Stance.Standing)
                && animationSource && !animationSource.IsRolling && !animationSource.IsTransitioning;
            if (CurrentWeapon != Weapon.Sword) { swordShoulderWeight = 0; return; }
            swordShoulderWeight = Mathf.MoveTowards(swordShoulderWeight, carrying ? 1f : 0f, Time.deltaTime * 10f);
            if (swordShoulderWeight > .001f) ApplySwordRestPose(swordShoulderWeight);
        }

        void ApplySwordRestPose(float weight)
        {
            if (!animator || !animator.isHuman) return;
            var upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            if (!upper || !elbow || !hand || !index || !little) return;
            weight = Mathf.Clamp01(weight);
            Vector3 knuckles = (index.position + little.position) * .5f;
            Vector3 shaft = (index.position - little.position).normalized;
            Vector3 normal = Vector3.Cross(shaft, knuckles - hand.position).normalized;
            if (normal.sqrMagnitude < .01f) return;
            Quaternion gripRotation = Quaternion.LookRotation(normal, shaft) * Quaternion.Euler(SwordGripAdjustment);
            float scale = Mathf.Abs(transform.lossyScale.y);
            Vector3 wantedGrip = upper.position + CarryOffset(SwordRestGrip) * scale;
            Vector3 rest = upper.position + CarryOffset(SwordRestTip) * scale;
            Vector3 direction = (rest - wantedGrip).normalized;
            if (direction.sqrMagnitude < .01f) return;
            Quaternion correction = Quaternion.FromToRotation(gripRotation * Vector3.up, direction);
            Quaternion wristRotation = hand.rotation;
            Vector3 palm = Vector3.Lerp(hand.position, knuckles, .65f) + hand.TransformVector(SwordGripOffset);
            // Move the arm and wrist with the handle; never translate the sword out of the palm.
            Vector3 wantedHand = wantedGrip - correction * (palm - hand.position);
            SolveSwordCarryArm(upper, elbow, hand, wantedHand, weight);
            hand.rotation = Quaternion.Slerp(wristRotation, correction * wristRotation, weight);
        }

        Vector3 CarryOffset(Vector3 value) => transform.right * value.x + transform.up * value.y + transform.forward * value.z;

        void SolveSwordCarryArm(Transform upper, Transform lower, Transform hand, Vector3 target, float weight, Vector3? poleHint = null)
        {
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, hand.position);
            Vector3 delta = target - upper.position;
            if (a < .001f || b < .001f || delta.sqrMagnitude < .0001f) return;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a-b) + .001f, a+b-.001f);
            Vector3 direction = delta.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(poleHint ?? (lower.position-upper.position), direction).normalized;
            if (pole.sqrMagnitude < .01f) pole = Vector3.ProjectOnPlane(transform.right-transform.up, direction).normalized;
            float along = (a*a + distance*distance - b*b) / (2f*distance);
            Vector3 wantedElbow = upper.position + direction*along + pole*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation = Quaternion.Slerp(upper.rotation,
                Quaternion.FromToRotation(lower.position-upper.position,wantedElbow-upper.position)*upper.rotation,weight);
            Vector3 wantedWrist = upper.position + direction*distance;
            lower.rotation = Quaternion.Slerp(lower.rotation,
                Quaternion.FromToRotation(hand.position-lower.position,wantedWrist-lower.position)*lower.rotation,weight);
        }
    }
}
