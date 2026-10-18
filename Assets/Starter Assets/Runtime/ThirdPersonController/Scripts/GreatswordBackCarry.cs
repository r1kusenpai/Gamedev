using UnityEngine;
using UnityEngine.Playables;
namespace StarterAssets {
 public partial class WeaponController {
  [Header("Greatsword running back mount")]
  public bool GreatswordBackCarryWhileRunning=true;
  float greatswordBackWeight;
  bool WantsGreatswordBackCarry=>GreatswordBackCarryWhileRunning&&CurrentWeapon==Weapon.Greatsword&&great&&movement&&movement.Grounded
   &&actionTime<=0&&!swordSequence.Active&&!charging&&!pending&&!dodging
   &&(!posture||posture.CurrentStance==CQCPosture.Stance.Standing||posture.CurrentStance==CQCPosture.Stance.Crouching)
   // Retain the back mount through grounded sprint acceleration transitions.
   &&(!animationSource||!animationSource.IsRolling&&(!animationSource.IsTransitioning||WeaponSprintActive));
  void UpdateGreatswordBackCarry(){
   if(!great||CurrentWeapon!=Weapon.Greatsword){greatswordBackWeight=0;return;}
   if(downloadedGreatCarry.IsValid()){greatswordBackWeight=downloadedGreatCarryWeight;if(greatswordBackWeight>0)ApplyDownloadedGreatswordGrip(greatswordBackWeight);return;}
   // Damage frames always use the actual hand grip, never a blended sheathed pose.
   if(actionTime>0||swordSequence.Active||charging||pending||dodging||animationSource&&(animationSource.IsRolling||animationSource.IsTransitioning)){greatswordBackWeight=0;return;}
   greatswordBackWeight=Mathf.MoveTowards(greatswordBackWeight,WantsGreatswordBackCarry?1:0,Time.deltaTime*8);
   if(greatswordBackWeight<=0)return;
   ApplyGreatswordBackMount(greatswordBackWeight);
  }
  void ApplyDownloadedGreatswordGrip(float weight){
   if(weight<=0||!animator||!great)return;
   var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
   if(!chest)chest=animator.GetBoneTransform(HumanBodyBones.Chest);
   if(!chest)return;
   float scale=Mathf.Abs(transform.lossyScale.y);
   // Mount directly to the chest skeleton. The palm and arm never determine this pose.
   // Derive a stable torso frame from mapped bones instead of rig-specific bone axes.
   var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
   var left=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
   var right=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
   Vector3 up=hips?(chest.position-hips.position).normalized:transform.up;
   Vector3 across=left&&right?right.position-left.position:transform.right;
   across=Vector3.ProjectOnPlane(across,up).normalized;
   if(across.sqrMagnitude<.01f)across=transform.right;
   Vector3 forward=Vector3.Cross(across,up).normalized;
   if(Vector3.Dot(forward,transform.forward)<0)forward=-forward;
   Vector3 direction=(-across*.72f-up*.69f).normalized;
   Quaternion rotation=Quaternion.LookRotation(-forward,direction);
   // Seat the middle of the blade over the upper back rather than placing its tip at the hips.
   Vector3 bladeContact=chest.position-forward*(.19f*scale)+up*(.04f*scale)+direction*(.17f*scale);
   Vector3 position=bladeContact-direction*(.80f*scale);
   great.transform.SetPositionAndRotation(Vector3.Lerp(great.transform.position,position,Mathf.Clamp01(weight)),Quaternion.Slerp(great.transform.rotation,rotation,Mathf.Clamp01(weight)));
   // The chest mount is authoritative. Solve the hand to its handle without refitting the sword.
   var arm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
   var forearm=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
   var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
   if(!arm||!forearm||!hand)return;
   if(!stableSwordGripReady)CalibrateStableSwordGrip();
   if(!stableSwordGripReady)return;
   Vector3 mountedPosition=great.transform.position;
   Quaternion mountedRotation=great.transform.rotation;
   Quaternion wrist=hand.rotation;
   Quaternion wantedWrist=mountedRotation*Quaternion.Inverse(stableSwordGripRotation*Quaternion.Euler(GreatswordGripAdjustment));
   Vector3 palmOffset=hand.TransformVector(stableSwordGripPosition+GreatswordGripOffset);
   Vector3 handle=great.transform.TransformPoint(new Vector3(0f,.20f,0f));
   Vector3 target=handle-wantedWrist*Quaternion.Inverse(wrist)*palmOffset;
   SolveSwordCarryArm(arm,forearm,hand,target,Mathf.Clamp01(weight),across-up*.25f+forward*.15f);
   hand.rotation=Quaternion.Slerp(wrist,wantedWrist,Mathf.Clamp01(weight));
   // The weapon is still parented beneath the hand for combat; restore the independent mount
   // after posing the arm so parent movement cannot drag it away from the chest.
   great.transform.SetPositionAndRotation(mountedPosition,mountedRotation);
  }
  void ApplyGreatswordBackMount(float weight){
   var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);if(!chest)chest=animator.GetBoneTransform(HumanBodyBones.Chest);if(!chest||!great)return;
   // Blade geometry extends along +Y; point it down-left with its flat face away from the back.
   Vector3 direction=CarryOffset(new Vector3(-.62f,-.78f,0)).normalized;
   Quaternion rotation=Quaternion.LookRotation(-transform.forward,direction);
   Vector3 grip=chest.position+CarryOffset(new Vector3(.12f,.47f,-.14f))*Mathf.Abs(transform.lossyScale.y);
   var upper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
   var lower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
   var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
   if(!upper||!lower||!hand)return;
   if(!stableSwordGripReady)CalibrateStableSwordGrip();
   if(!stableSwordGripReady)return;
   // Pose the hand around the mounted handle instead of leaving the arm at the side.
   Quaternion wrist=hand.rotation;
   Quaternion handToGrip=stableSwordGripRotation*Quaternion.Euler(GreatswordGripAdjustment);
   Quaternion wantedWrist=rotation*Quaternion.Inverse(handToGrip);
   Vector3 palmOffset=hand.TransformVector(stableSwordGripPosition+GreatswordGripOffset);
   Vector3 wantedHand=grip-wantedWrist*Quaternion.Inverse(wrist)*palmOffset;
   SolveSwordCarryArm(upper,lower,hand,wantedHand,weight,transform.right*.8f-transform.up*.2f-transform.forward*.4f);
   hand.rotation=Quaternion.Slerp(wrist,wantedWrist,weight);
   // Final attachment comes from the solved palm; never leave a detached back-mounted blade.
   FitGrip(great,HumanBodyBones.RightHand,HumanBodyBones.RightIndexProximal,HumanBodyBones.RightLittleProximal,GreatswordGripOffset,GreatswordGripAdjustment);
  }
 }
}


























