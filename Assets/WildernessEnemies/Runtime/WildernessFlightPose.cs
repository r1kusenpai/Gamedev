using UnityEngine;
namespace WildernessCombat {
// Additive flight locomotion, evaluated after the supplied humanoid animation.
// The controller still owns movement; this changes only the visual pose.
public sealed class WildernessFlightPose:MonoBehaviour {
 Transform visual,spine,leftArm,rightArm,leftThigh,rightThigh,leftKnee,rightKnee,leftFoot,rightFoot;
 Vector3 lastPosition;float movement,bank,previousYaw;bool ready;
 public void Initialize(Transform model,Animator animator){visual=model;spine=animator.GetBoneTransform(HumanBodyBones.Chest);
 leftArm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);rightArm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
 leftThigh=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);rightThigh=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
 leftKnee=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);rightKnee=animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
 leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
 lastPosition=transform.position;previousYaw=transform.eulerAngles.y;ready=true;}
 public float MovementBlend=>movement;
 public void Evaluate(bool locomotion,bool sword,float phase,float dt){if(!ready)return;
 float speed=Vector3.ProjectOnPlane(transform.position-lastPosition,Vector3.up).magnitude/Mathf.Max(.001f,dt);
 float turn=Mathf.DeltaAngle(previousYaw,transform.eulerAngles.y)/Mathf.Max(.001f,dt);lastPosition=transform.position;previousYaw=transform.eulerAngles.y;
 if(!locomotion){movement=0;bank=0;visual.localRotation=Quaternion.identity;return;}
 float target=Mathf.InverseLerp(.2f,9f,speed);float smooth=1-Mathf.Exp(-dt*8);movement=Mathf.Lerp(movement,target,smooth);bank=Mathf.Lerp(bank,Mathf.Clamp(-turn*.025f,-9,9)*movement,smooth);
 // Patrol is an upright slow glide; pursuit leans forward with trailing legs.
 float glide=Mathf.SmoothStep(0,1,movement),wave=Mathf.Sin(Time.time*(3+glide*3)+phase);
 visual.localRotation=Quaternion.Euler(glide*23,0,bank);
 Vector3 right=transform.right;
 Turn(spine,right,glide*3);Turn(leftThigh,right,glide*(-13+wave*4));Turn(rightThigh,right,glide*(-13-wave*4));
 Turn(leftKnee,right,glide*(24-wave*6));Turn(rightKnee,right,glide*(24+wave*6));Turn(leftFoot,right,glide*9);Turn(rightFoot,right,glide*9);
 // Keep the equipped left hand stable enough to carry the sword while flying.
 Turn(leftArm,right,glide*(sword?4:11));Turn(rightArm,right,glide*11);
 }
 static void Turn(Transform bone,Vector3 axis,float angle){if(bone)bone.rotation=Quaternion.AngleAxis(angle,axis)*bone.rotation;}
}
}
