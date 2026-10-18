using UnityEngine;using UnityEngine.Animations;using UnityEngine.Playables;
namespace StarterAssets {
 public partial class WeaponController {
  AnimationClipPlayable downloadedGreatCarry;AvatarMask downloadedGreatCarryMask;float downloadedGreatCarryWeight;
  void InitializeDownloadedGreatCarry(){var source=Resources.LoadAll<AnimationClip>("WeaponAnimations/GreatswordCarry/DownloadedCarry");AnimationClip clip=null;foreach(var c in source)if(c.isHumanMotion&&!c.name.StartsWith("__preview__")){clip=c;break;}if(!clip)return;
   overlay.SetInputCount(4);downloadedGreatCarry=AnimationClipPlayable.Create(graph,clip);downloadedGreatCarry.SetApplyFootIK(false);downloadedGreatCarry.SetApplyPlayableIK(false);graph.Connect(downloadedGreatCarry,0,overlay,3);
   downloadedGreatCarryMask=new AvatarMask();for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++){var part=(AvatarMaskBodyPart)i;downloadedGreatCarryMask.SetHumanoidBodyPartActive(part,part==AvatarMaskBodyPart.RightArm||part==AvatarMaskBodyPart.RightFingers);}overlay.SetLayerMaskFromAvatarMask(3,downloadedGreatCarryMask);overlay.SetInputWeight(3,0);
  }
  void UpdateDownloadedGreatCarry(){if(!downloadedGreatCarry.IsValid())return;bool attack=actionTime>0||swordSequence.Active||charging||pending;downloadedGreatCarryWeight=attack?0:Mathf.MoveTowards(downloadedGreatCarryWeight,WantsGreatswordBackCarry?1:0,Time.deltaTime*10);
   overlay.SetInputWeight(3,downloadedGreatCarryWeight);var clip=downloadedGreatCarry.GetAnimationClip();if(downloadedGreatCarry.GetTime()>=clip.length)downloadedGreatCarry.SetTime(downloadedGreatCarry.GetTime()%clip.length);
  }
 }
}
namespace StarterAssets {
 public partial class WeaponController {
  AnimationClipPlayable playerHit2Playable;AvatarMask playerHit2Mask;AnimationClip playerHit2Clip;
  float playerHit2Started=-100f,playerHitBend=-1f;
  public bool IsPlayingDamageReaction=>playerHit2Clip&&Time.time-playerHit2Started<playerHit2Clip.length/1.5f+.1f;
  void InitializePlayerHit2(){
   foreach(var clip in Resources.LoadAll<AnimationClip>("WeaponAnimations/PlayerHit2"))
    if(clip.isHumanMotion&&!clip.name.StartsWith("__preview__")){playerHit2Clip=clip;break;}
   if(!playerHit2Clip)return;
   playerHit2Mask=new AvatarMask();
   for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)playerHit2Mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,i!=(int)AvatarMaskBodyPart.Root&&i!=(int)AvatarMaskBodyPart.LeftFootIK&&i!=(int)AvatarMaskBodyPart.RightFootIK);
   overlay.SetInputCount(5);playerHit2Playable=AnimationClipPlayable.Create(graph,playerHit2Clip);
   playerHit2Playable.SetApplyFootIK(false);playerHit2Playable.SetApplyPlayableIK(false);playerHit2Playable.SetSpeed(0);
   graph.Connect(playerHit2Playable,0,overlay,4);overlay.SetLayerMaskFromAvatarMask(4,playerHit2Mask);overlay.SetInputWeight(4,0);
  }
  public void PlayDamageReaction(Vector3? attackerPosition=null){
   if(!ready||!playerHit2Playable.IsValid()||animationSource&&animationSource.IsRolling)return;
   if(attackerPosition.HasValue){var toward=Vector3.ProjectOnPlane(attackerPosition.Value-transform.position,transform.up);playerHitBend=Vector3.Dot(toward,transform.forward)>=0?-1f:1f;}else playerHitBend=-1f;
   CancelForRoll();playerHit2Started=Time.time;playerHit2Playable.SetTime(0);playerHit2Playable.SetSpeed(0);
   overlay.SetInputWeight(0,1);overlay.SetInputWeight(4,1);
   overlay.SetInputWeight(1,0);overlay.SetInputWeight(2,0);overlay.SetInputWeight(3,0);
   downloadedGreatCarryWeight=greatswordBackWeight=0;
  }
  bool UpdatePlayerHit2(){
   if(!playerHit2Playable.IsValid())return false;
   float age=Time.time-playerHit2Started;
   if(age>=playerHit2Clip.length/1.5f+.1f){overlay.SetInputWeight(4,0);return false;}
   playerHit2Playable.SetTime(Mathf.Clamp(age*1.5f,0,playerHit2Clip.length));
   overlay.SetInputWeight(0,1);overlay.SetInputWeight(4,age<=playerHit2Clip.length/1.5f?1:1-(age-playerHit2Clip.length/1.5f)/.1f);
   overlay.SetInputWeight(1,0);overlay.SetInputWeight(2,0);overlay.SetInputWeight(3,0);
   downloadedGreatCarryWeight=greatswordBackWeight=0;return true;
  }
  void ApplyDirectionalHitBend(){
   if(!IsPlayingDamageReaction||!animator)return;
   float age=Time.time-playerHit2Started;
   float duration=playerHit2Clip.length/1.5f;
   float envelope=age<.045f?Mathf.SmoothStep(0,1,age/.045f):1-Mathf.SmoothStep(0,1,(age-.045f)/Mathf.Max(.05f,duration+.1f-.045f));
   var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
   var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
   float angle=22f*playerHitBend*envelope;
   if(spine)spine.rotation=Quaternion.AngleAxis(angle*(chest?.45f:1f),transform.right)*spine.rotation;
   if(chest)chest.rotation=Quaternion.AngleAxis(angle*(spine?.55f:1f),transform.right)*chest.rotation;
  } }
}


