using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace StarterAssets
{
 [DefaultExecutionOrder(10)] [RequireComponent(typeof(Animator),typeof(CharacterController))]
 public class WeaponController : MonoBehaviour
 {
  public GameObject SwordModelPrefab,GreatswordModelPrefab,BowModelPrefab;
  public Vector3 SwordGripOffset,GreatswordGripOffset,BowGripOffset;
  public Vector3 SwordGripRotation=new Vector3(0,0,-90),GreatswordGripRotation=new Vector3(0,0,-90),BowGripRotation=new Vector3(0,0,-90);
  public enum Weapon {Unarmed=0,Sword=1,Greatsword=2,Bow=3}
  public Weapon CurrentWeapon=Weapon.Unarmed;
  public bool Equipped=>CurrentWeapon!=Weapon.Unarmed;
  public bool DodgeActive=>false;
  public bool UsesWeaponPose=>Equipped && !(posture && posture.CurrentStance!=CQCPosture.Stance.Standing) && !(animationSource && (animationSource.IsRolling || animationSource.IsTransitioning));
  public bool AutoFitGrips=true;
  public Vector3 SwordGripAdjustment,GreatswordGripAdjustment,BowGripAdjustment;
  public bool UseActionRootMotion=>Equipped&&actionTime>0&&!dodging&&!usingUpperBody&&CurrentWeapon!=Weapon.Bow;
  public float ChargeSeconds=1.4f,ComboResetSeconds=1f;
  [Header("Bow focus")]
  [Range(.4f,1f)] public float BowZoomRatio=.65f;
  public float BowZoomSmoothness=8f;
  public float BowFocusCircleStart=72f,BowFocusCircleEnd=12f;
  [Header("Bow body movement")]
  [Range(0f,20f)] public float BowBodyPitchLimit=12f;
  [Range(0f,.5f)] public float BowBodyPitchShare=.25f;
  bool bowAiming;float bowCharge,focusBlend;
  CinemachineCamera zoomCamera;Camera zoomFallback;float originalFov;bool zoomCaptured;
  Transform[] bowArmBones;Quaternion[] bowArmRotations;int bowArmPoseFrame=-1;
  public float SwordDamage=24f,GreatswordDamage=38f,ArrowDamage=35f;
  public bool IsBlocking=>false;
  public float IncomingDamageMultiplier=>1f;
  Animator animator;CharacterController controller;ThirdPersonController movement;StarterAssetsInputs input;CQCPosture posture;CQCCombat combat;CQCCrouchClips animationSource;
  AnimationLayerMixerPlayable overlay;AnimationMixerPlayable clips;AvatarMask upperMask,fullMask,greatGripMask;bool usingUpperBody;AnimationClipPlayable[] players=new AnimationClipPlayable[2];AnimationClip currentClip;PlayableGraph graph;
  int currentSlot,combo;float fade,actionTime,actionLength,lastAttack=-100,pressedAt;bool pending,charging,queued,queuedCharged,dodging,greatHoldRequested;
  float attackDamage;bool heldSlashAction;float holdRecoveryUntil;HashSet<CQCTrainingTarget> hits=new HashSet<CQCTrainingTarget>();
  readonly Dictionary<string,AnimationClip> library=new Dictionary<string,AnimationClip>();
  GameObject sword,great,bow;Material steel,wood;bool ready;
  void Start()
  {
   animator=GetComponent<Animator>();controller=GetComponent<CharacterController>();movement=GetComponent<ThirdPersonController>();input=GetComponent<StarterAssetsInputs>();posture=GetComponent<CQCPosture>();combat=GetComponent<CQCCombat>();animationSource=GetComponent<CQCCrouchClips>();
   if(!animator.isHuman||!animationSource||!animationSource.WeaponGraph.IsValid()){Debug.LogError("Weapon animations need the robot Humanoid avatar and existing CQC animation graph.",this);enabled=false;return;}
   graph=animationSource.WeaponGraph;overlay=animationSource.CreateWeaponOverlay();clips=AnimationMixerPlayable.Create(graph,2);graph.Connect(clips,0,overlay,1);BuildWeapons();upperMask=new AvatarMask();fullMask=new AvatarMask();for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++){fullMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,true);upperMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,i!=(int)AvatarMaskBodyPart.Root&&i!=(int)AvatarMaskBodyPart.LeftLeg&&i!=(int)AvatarMaskBodyPart.RightLeg&&i!=(int)AvatarMaskBodyPart.LeftFootIK&&i!=(int)AvatarMaskBodyPart.RightFootIK);}greatGripMask=new AvatarMask();
   for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)
   {
    var part=(AvatarMaskBodyPart)i;
    greatGripMask.SetHumanoidBodyPartActive(part,part==AvatarMaskBodyPart.LeftArm || part==AvatarMaskBodyPart.RightArm || part==AvatarMaskBodyPart.LeftFingers || part==AvatarMaskBodyPart.RightFingers);
   }
   bowArmBones=new[]{animator.GetBoneTransform(HumanBodyBones.Spine),animator.GetBoneTransform(HumanBodyBones.Chest),animator.GetBoneTransform(HumanBodyBones.UpperChest),animator.GetBoneTransform(HumanBodyBones.Neck),animator.GetBoneTransform(HumanBodyBones.Head),animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),animator.GetBoneTransform(HumanBodyBones.LeftHand),animator.GetBoneTransform(HumanBodyBones.RightUpperArm),animator.GetBoneTransform(HumanBodyBones.RightLowerArm),animator.GetBoneTransform(HumanBodyBones.RightHand)};
   bowArmRotations=new Quaternion[bowArmBones.Length];
   ready=true;ShowWeapons();
  }
  AnimationClip Clip(string group,string name)
  {
   string path=group+"/"+name;if(library.TryGetValue(path,out var clip))return clip;
   foreach(var c in Resources.LoadAll<AnimationClip>("WeaponAnimations/"+path))if(c.isHumanMotion&&!c.name.StartsWith("__preview__")){library[path]=c;return c;}
   library[path]=null;return null;
  }
  void Play(AnimationClip clip,bool restart=false,float frozenTime=-1)
  {
   if(!clip)return;if(clip==currentClip&&!restart){if(frozenTime>=0){players[currentSlot].SetTime(frozenTime);players[currentSlot].SetSpeed(0);}else players[currentSlot].SetSpeed(1);return;}
   int next=1-currentSlot;if(players[next].IsValid()){clips.DisconnectInput(next);graph.DestroyPlayable(players[next]);}
   players[next]=AnimationClipPlayable.Create(graph,clip);players[next].SetApplyFootIK(false);players[next].SetApplyPlayableIK(true);players[next].SetTime(frozenTime>=0?frozenTime:0);players[next].SetSpeed(frozenTime>=0?0:1);graph.Connect(players[next],0,clips,next);currentSlot=next;currentClip=clip;fade=players[1-currentSlot].IsValid()?0:1;
  }
  void Select(Weapon weapon)
  {
   if(actionTime>0||(combat&&(combat.IsBusy||combat.IsHolding))||(animationSource&&(animationSource.IsRolling||animationSource.IsTransitioning||animationSource.IsHostageSequenceActive||animationSource.IsAirAttackPlaying||animationSource.IsGroundAttackPlaying)))return;
   CurrentWeapon=weapon;pending=charging=queued=greatHoldRequested=false;combo=0;ShowWeapons();
   if(!Equipped)return;string group=Group();string name=CurrentWeapon==Weapon.Sword?"draw sword 1":CurrentWeapon==Weapon.Greatsword?"draw a great sword 1":"standing equip bow";
   BeginAction(Clip(group,name),0,false);
  }
  string Group()=>CurrentWeapon==Weapon.Sword?"Sword":CurrentWeapon==Weapon.Greatsword?"Great":"Bow";
  void Update()
  {
   bowAiming=false;bowCharge=0;
   if(!ready||!graph.IsValid())return;
#if ENABLE_INPUT_SYSTEM
   var k=Keyboard.current;var m=Mouse.current;
   if(k!=null){if(k.digit1Key.wasPressedThisFrame||k.numpad1Key.wasPressedThisFrame)Select(Weapon.Sword);if(k.digit2Key.wasPressedThisFrame||k.numpad2Key.wasPressedThisFrame)Select(Weapon.Greatsword);if(k.digit3Key.wasPressedThisFrame||k.numpad3Key.wasPressedThisFrame)Select(Weapon.Bow);if(k.digit4Key.wasPressedThisFrame||k.numpad4Key.wasPressedThisFrame||k.xKey.wasPressedThisFrame)Select(Weapon.Unarmed);}
   bool down=m!=null&&m.leftButton.wasPressedThisFrame,up=m!=null&&m.leftButton.wasReleasedThisFrame,held=m!=null&&m.leftButton.isPressed,right=m!=null&&m.rightButton.isPressed,alt=k!=null&&k.leftAltKey.wasPressedThisFrame;
#else
   if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1))Select(Weapon.Sword);if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Keypad2))Select(Weapon.Greatsword);if(Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.Keypad3))Select(Weapon.Bow);if(Input.GetKeyDown(KeyCode.Alpha4)||Input.GetKeyDown(KeyCode.Keypad4)||Input.GetKeyDown(KeyCode.X))Select(Weapon.Unarmed);
   bool down=Input.GetMouseButtonDown(0),up=Input.GetMouseButtonUp(0),held=Input.GetMouseButton(0),right=Input.GetMouseButton(1),alt=Input.GetKeyDown(KeyCode.LeftAlt);
#endif
   overlay.SetInputWeight(0,1);overlay.SetInputWeight(1,Equipped?1:0);if(!Equipped)return;
   if(alt && movement.Grounded){CancelAction();animationSource.TryStartRoll();}
   if(animationSource.IsRolling || animationSource.IsTransitioning && CurrentWeapon!=Weapon.Bow || (posture && posture.IsProne))
   {CancelAction();overlay.SetInputWeight(1,0);return;}
   bool crouched=posture && posture.CurrentStance==CQCPosture.Stance.Crouching;
   if(CurrentWeapon==Weapon.Greatsword)
   {
    if(down){pressedAt=Time.time;pending=true;}
    if(!Application.isFocused){pending=false;greatHoldRequested=false;}
    if(up || !held)greatHoldRequested=false;
    // A short click remains a normal combo strike; only a sustained press enters the repeat attack.
    if(pending && held && Time.time-pressedAt>=.25f)
    {
     greatHoldRequested=true;
     if(actionTime<=0){pending=false;BeginGreatHeldSlash();}
    }
    if(up && pending)
    {
     pending=false;
     if(Time.time-pressedAt<.25f)
     {
      if(actionTime>0){queued=true;queuedCharged=false;}
      else Attack(false);
     }
    }
   }
   bool greatCrouchGrip=crouched && CurrentWeapon==Weapon.Greatsword && actionTime<=0;
   overlay.SetInputWeight(1,crouched && actionTime<=0 && !charging && !greatCrouchGrip ? 0:1);
   bool upper=(crouched || input&&input.move.sqrMagnitude>.01f)&&movement.Grounded&&!dodging&&(charging||actionTime>0&&attackDamage>0||CurrentWeapon==Weapon.Bow&&actionTime>0&&currentClip&&currentClip.name.Contains("recoil"))&&currentClip&&!currentClip.name.Contains("slide")&&!currentClip.name.Contains("jump");
   usingUpperBody=upper || greatCrouchGrip || CurrentWeapon==Weapon.Bow && actionTime>0 && currentClip && currentClip.name.Contains("recoil");overlay.SetLayerMaskFromAvatarMask(1,greatCrouchGrip?greatGripMask:upper || CurrentWeapon==Weapon.Bow && actionTime>0 && currentClip && currentClip.name.Contains("recoil")?upperMask:fullMask);
   fade=Mathf.Min(1,fade+Time.deltaTime/.12f);clips.SetInputWeight(currentSlot,fade);clips.SetInputWeight(1-currentSlot,players[1-currentSlot].IsValid()?1-fade:0);
   if(CurrentWeapon==Weapon.Bow && down){pressedAt=Time.time;pending=true;}
   if(actionTime>0)
   {
    if(CurrentWeapon==Weapon.Bow && up)pending=false;
    actionTime-=Time.deltaTime;float progress=1-actionTime/Mathf.Max(actionLength,.01f);
    if(attackDamage>0&&progress>=.28f&&progress<=.7f)CheckHit();
    // Buffer a completed tap, never a mouse-down that may become a charge.
    if(down&&CurrentWeapon==Weapon.Sword){pressedAt=Time.time;pending=true;}
    if(up&&pending&&CurrentWeapon==Weapon.Sword){pending=false;queued=true;queuedCharged=Time.time-pressedAt>=.25f;charging=false;}
    if(actionTime<=0){dodging=false;lastAttack=Time.time;if(CurrentWeapon==Weapon.Greatsword && greatHoldRequested && held){BeginGreatHeldSlash();}else if(queued){queued=false;Attack(queuedCharged);queuedCharged=false;}else attackDamage=0;}
    return;
   }
   if(CurrentWeapon==Weapon.Bow)
   {
    if(down || held && !pending){pressedAt=Time.time;pending=true;}
    if(up&&pending){pending=false;FireArrow(Mathf.Clamp01((Time.time-pressedAt)/ChargeSeconds));BeginAction(Clip("Bow","standing aim recoil"),0,false);return;}
    if((right || held) && Application.isFocused)
    {
     bowAiming=true;bowCharge=pending&&held?Mathf.Clamp01((Time.time-pressedAt)/Mathf.Max(.1f,ChargeSeconds)):0;
     FaceCamera();
     // Keep the same draw/aim arms in every stance; the base graph supplies standing, walk,
     // sprint and crouch legs rather than replacing them with a standing aim-walk clip.
     usingUpperBody=true;overlay.SetInputWeight(1,1);overlay.SetLayerMaskFromAvatarMask(1,upperMask);
     var aim=Clip("Bow","standing aim overdraw");
     if(aim)Play(aim,false,aim.length*Mathf.Lerp(.15f,.55f,pending&&held?bowCharge:1f));
     return;
    }
   }
   else if(CurrentWeapon==Weapon.Sword)
   {
    if(down){pressedAt=Time.time;pending=true;}
    if(pending&&held&&Time.time-pressedAt>.25f){charging=true;overlay.SetInputWeight(1,1);if(crouched){usingUpperBody=true;overlay.SetLayerMaskFromAvatarMask(1,upperMask);}var windup=Clip(Group(),CurrentWeapon==Weapon.Greatsword?"great sword attack":"sword and shield slash (5)");if(windup)Play(windup,false,windup.length*.16f);}
    if(up&&pending){pending=false;Attack(charging);charging=false;return;}
    if(charging)return;
   }
   if(crouched)
   {
    if(greatCrouchGrip){overlay.SetInputWeight(1,1);Play(Idle());}
    else overlay.SetInputWeight(1,0);
    return;
   }
   Locomotion();
  }
  AnimationClip Idle()=>Clip(Group(),CurrentWeapon==Weapon.Sword?"sword and shield idle":CurrentWeapon==Weapon.Greatsword?"great sword idle":"standing idle 01");
  void Locomotion()
  {
   bool moving=input&&input.move.sqrMagnitude>.01f;bool crouch=posture&&posture.CurrentStance==CQCPosture.Stance.Crouching;
   if(movement&&!movement.Grounded){Play(CurrentWeapon==Weapon.Bow?Clip("Bow","fall a loop"):Clip(Group(),CurrentWeapon==Weapon.Sword?"sword and shield jump":"great sword jump"));return;}
   if(crouch&&CurrentWeapon!=Weapon.Bow){Play(Clip(Group(),CurrentWeapon==Weapon.Sword?(moving?"sword and shield crouching":"sword and shield crouch idle"):(moving?"great sword crouching (3)":"great sword crouching")));return;}
   if(!moving){Play(Idle());return;}
   string name=CurrentWeapon==Weapon.Sword?(input.sprint?"sword and shield run":"sword and shield walk"):CurrentWeapon==Weapon.Greatsword?(input.sprint?"great sword run":"great sword walk"):(input.sprint?"standing run forward":"standing walk forward");Play(Clip(Group(),name));
  }
  void BeginAction(AnimationClip clip,float damage,bool dodge)
  {
   if(!clip){Debug.LogWarning("Required weapon clip is missing. Run Tools > Weapons > Prepare Weapon Packs.",this);return;}
   if(CurrentWeapon==Weapon.Bow || posture && posture.CurrentStance==CQCPosture.Stance.Crouching){usingUpperBody=true;overlay.SetLayerMaskFromAvatarMask(1,upperMask);}
   heldSlashAction=false;overlay.SetInputWeight(1,1);Play(clip,true);actionLength=clip.length;actionTime=actionLength;attackDamage=damage;dodging=dodge;hits.Clear();
  }
  void BeginGreatHeldSlash()
  {
   var slash=Clip("Great","great sword held slash");
   if(!slash){greatHoldRequested=false;Debug.LogWarning("Great Sword Slash is still importing. Leave Play mode and allow Unity to finish importing it.",this);return;}
   charging=pending=queued=queuedCharged=false;FaceCamera();
   BeginAction(slash,GreatswordDamage,false);heldSlashAction=true;holdRecoveryUntil=Time.time+actionLength+.2f;
  }
  void Attack(bool charged)
  {
   FaceCamera();bool air=movement&&!movement.Grounded;bool run=input&&input.sprint&&input.move.sqrMagnitude>.01f;
   float damage=CurrentWeapon==Weapon.Greatsword?GreatswordDamage:SwordDamage;string name;
   if(charged){damage*=Mathf.Lerp(1.3f,2.3f,Mathf.Clamp01((Time.time-pressedAt)/ChargeSeconds));name=CurrentWeapon==Weapon.Greatsword?"great sword attack":"sword and shield slash (5)";combo=0;}
   else if(air){name=CurrentWeapon==Weapon.Greatsword?"great sword jump attack":"sword and shield attack (3)";damage*=1.35f;}
   else if(run){name=CurrentWeapon==Weapon.Greatsword?"great sword slide attack":"sword and shield attack (4)";damage*=1.2f;}
   else
   {
    if(Time.time-lastAttack>ComboResetSeconds)combo=0;
    string[] names=CurrentWeapon==Weapon.Greatsword?new[]{"great sword slash","great sword slash (3)","great sword slash (5)","great sword slash (4)"}:new[]{"sword and shield slash","sword and shield slash (3)","sword and shield slash (5)","sword and shield attack (2)"};name=names[combo];damage*=1+combo*.15f;combo=(combo+1)%names.Length;
   }
   BeginAction(Clip(Group(),name),damage,false);
  }
  void CancelAction()
  {
   heldSlashAction=false;holdRecoveryUntil=0;actionTime=attackDamage=0;pending=charging=queued=queuedCharged=dodging=greatHoldRequested=false;combo=0;
  }
  public Vector3 FilterActionTravel(Vector3 travel)
  {
   // Keep the travelled world position across held-slash cycle resets and its exit blend.
   if(heldSlashAction || Time.time<holdRecoveryUntil)
   {
    float backwards=Vector3.Dot(travel,transform.forward);
    if(backwards<0)travel-=transform.forward*backwards;
   }
   return travel;
  }
  void FaceCamera(){if(Camera.main){Vector3 f=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up);if(f.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(f);}}
  void CheckHit()
  {
   float reach=CurrentWeapon==Weapon.Greatsword?2.5f:1.8f;Vector3 center=transform.position+Vector3.up*(posture?posture.HitHeight:1.1f)+transform.forward*(reach*.65f);
   foreach(var col in Physics.OverlapSphere(center,reach*.55f,~0,QueryTriggerInteraction.Ignore))
   {
    var enemy=col.GetComponentInParent<CQCTrainingTarget>();if(!enemy||enemy.transform.IsChildOf(transform)||!hits.Add(enemy))continue;
    Vector3 toward=enemy.transform.position-transform.position;toward.y=0;if(Vector3.Dot(transform.forward,toward.normalized)<.1f){hits.Remove(enemy);continue;}
    bool blocked=false;Vector3 start=transform.position+Vector3.up,finish=enemy.transform.position+Vector3.up;
    foreach(var obstacle in Physics.RaycastAll(start,(finish-start).normalized,(finish-start).magnitude,~0,QueryTriggerInteraction.Ignore))
     if(!obstacle.transform.IsChildOf(transform)&&obstacle.collider.GetComponentInParent<CQCTrainingTarget>()!=enemy){blocked=true;break;}
    if(blocked){hits.Remove(enemy);continue;}
    bool sneak=posture&&posture.CurrentStance==CQCPosture.Stance.Crouching&&Vector3.Dot(enemy.transform.forward,-toward.normalized)<-.35f;enemy.ReceivePunch(attackDamage*(sneak?2.2f:1),true);
   }
  }
  void FireArrow(float charge)
  {
   Vector3 from=bow?bow.transform.position+transform.forward*.2f:transform.position+Vector3.up*1.25f+transform.forward*.7f;Vector3 target=from+transform.forward*100;
   if(Camera.main){Ray ray=Camera.main.ViewportPointToRay(new Vector3(.5f,.5f,0));target=ray.GetPoint(100);float best=100;foreach(var h in Physics.RaycastAll(ray,100,~0,QueryTriggerInteraction.Ignore))if(!h.transform.IsChildOf(transform)&&h.distance<best){best=h.distance;target=h.point;}}
   var arrow=new GameObject("Bow Arrow");arrow.transform.position=from;arrow.transform.rotation=Quaternion.LookRotation((target-from).normalized);Part(arrow.transform,"Arrow shaft",new Vector3(0,0,-.25f),new Vector3(.025f,.025f,.7f),wood);
   var flight=arrow.AddComponent<WeaponArrow>();flight.Owner=transform;flight.Damage=ArrowDamage*Mathf.Lerp(1,2,charge);flight.Velocity=(target-from).normalized*Mathf.Lerp(24,45,charge);
  }
  bool BowArmPoseActive=>CurrentWeapon==Weapon.Bow && (bowAiming || actionTime>0 && currentClip && currentClip.name.Contains("recoil"))
   && !(posture && posture.IsProne) && !(animationSource && animationSource.IsRolling);
  void OnAnimatorIK(int layer)
  {
   if(!ready || !BowArmPoseActive || bowArmBones==null)return;
   for(int i=0;i<bowArmBones.Length;i++)if(bowArmBones[i])bowArmRotations[i]=bowArmBones[i].localRotation;
   bowArmPoseFrame=Time.frameCount;
  }
  void UpdateBowFocus()
  {
   float desired=bowAiming && pending?bowCharge:0;
   focusBlend=Mathf.Lerp(focusBlend,desired,1-Mathf.Exp(-Mathf.Max(1,BowZoomSmoothness)*Time.deltaTime));
   if(!zoomCaptured && desired>.001f)
   {
    var camera=Camera.main;if(!camera)return;
    var brain=camera.GetComponent<CinemachineBrain>();
    zoomCamera=brain?brain.ActiveVirtualCamera as CinemachineCamera:null;
    if(!zoomCamera){var follow=GameObject.Find("PlayerFollowCamera");if(follow)zoomCamera=follow.GetComponent<CinemachineCamera>();}
    zoomFallback=zoomCamera?null:camera;
    originalFov=zoomCamera?zoomCamera.Lens.FieldOfView:camera.fieldOfView;zoomCaptured=true;
   }
   if(!zoomCaptured)return;
   float field=Mathf.Lerp(originalFov,Mathf.Max(20,originalFov*Mathf.Clamp(BowZoomRatio,.4f,1)),focusBlend);
   if(zoomCamera)zoomCamera.Lens.FieldOfView=field;else if(zoomFallback)zoomFallback.fieldOfView=field;
   if(desired==0 && focusBlend<.001f){RestoreBowZoom();focusBlend=0;}
  }
  void RestoreBowZoom()
  {
   if(zoomCaptured){if(zoomCamera)zoomCamera.Lens.FieldOfView=originalFov;else if(zoomFallback)zoomFallback.fieldOfView=originalFov;}
   zoomCaptured=false;zoomCamera=null;zoomFallback=null;
  }
  static void DrawFocusCircle(Vector2 center,float radius)
  {
   // Draw line segments at constant screen thickness as the circle contracts.
   Matrix4x4 saved=GUI.matrix;const int segments=64;
   for(int i=0;i<segments;i++)
   {
    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
    Vector2 start=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
    Vector2 end=center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius;
    Vector2 direction=end-start;
    GUI.matrix=saved;GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,start);
    GUI.DrawTexture(new Rect(start.x,start.y-.75f,direction.magnitude+.5f,1.5f),Texture2D.whiteTexture);
   }
   GUI.matrix=saved;
  }
  void OnDisable(){bowAiming=false;focusBlend=0;RestoreBowZoom();}
  void OnDestroy(){RestoreBowZoom();}
  void OnGUI()
  {
   if(CurrentWeapon!=Weapon.Bow)return;Color saved=GUI.color;GUI.color=Color.white;
   if(bowAiming)
   {
    float scale=Mathf.Clamp(Screen.height/1080f,.6f,2f);
    float radius=Mathf.Lerp(Mathf.Max(BowFocusCircleEnd,BowFocusCircleStart),Mathf.Max(8,BowFocusCircleEnd),focusBlend)*scale;
    GUI.color=Color.Lerp(new Color(1,1,1,.7f),new Color(.5f,1,.65f,.95f),focusBlend);
    DrawFocusCircle(new Vector2(Screen.width*.5f,Screen.height*.5f),radius);
    GUI.color=Color.white;
   }
   GUI.DrawTexture(new Rect(Screen.width*.5f-5,Screen.height*.5f-1,10,2),Texture2D.whiteTexture);
   GUI.DrawTexture(new Rect(Screen.width*.5f-1,Screen.height*.5f-5,2,10),Texture2D.whiteTexture);GUI.color=saved;
  }
  // Grip frames come from the character's palm and knuckles, not an assumed FBX bone axis.
  // The supplied models have their handle centre at the origin and blade/bow length along +Y.
  void LateUpdate()
  {
   UpdateBowFocus();
   if(!ready || !animator.isHuman)return;
   if(BowArmPoseActive && bowArmPoseFrame==Time.frameCount)
    for(int i=0;i<bowArmBones.Length;i++)if(bowArmBones[i])bowArmBones[i].localRotation=bowArmRotations[i];
   if(BowArmPoseActive)AlignImportedBowAim();
   if(!AutoFitGrips)return;
   FitGrip(sword,HumanBodyBones.RightHand,HumanBodyBones.RightIndexProximal,HumanBodyBones.RightLittleProximal,SwordGripOffset,SwordGripAdjustment);
   FitGrip(great,HumanBodyBones.RightHand,HumanBodyBones.RightIndexProximal,HumanBodyBones.RightLittleProximal,GreatswordGripOffset,GreatswordGripAdjustment);
   if(CurrentWeapon==Weapon.Bow && !(posture && posture.IsProne) && !animationSource.IsRolling)FitAimingBow(BowArmPoseActive);
   else FitGrip(bow,HumanBodyBones.LeftHand,HumanBodyBones.LeftIndexProximal,HumanBodyBones.LeftLittleProximal,BowGripOffset,BowGripAdjustment);
   if(CurrentWeapon==Weapon.Greatsword && great && posture && posture.CurrentStance==CQCPosture.Stance.Crouching
      && !animationSource.IsRolling && !animationSource.IsTransitioning && actionTime<=0)
    MatchCrouchedLeftHand(great.transform.TransformPoint(new Vector3(0,-.14f,0)));
  }
  // Solve after the original crouch pose so its arm adjustments cannot pull the support hand away.
  void MatchCrouchedLeftHand(Vector3 target)
  {
   var upper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
   var lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
   var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
   if(!upper || !lower || !hand)return;
   Vector3 delta=target-upper.position;float first=Vector3.Distance(upper.position,lower.position),second=Vector3.Distance(lower.position,hand.position);
   if(first<.001f || second<.001f || delta.sqrMagnitude<.0001f)return;
   float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(first-second)+.001f,first+second-.001f);
   Vector3 direction=delta.normalized;
   Vector3 pole=Vector3.ProjectOnPlane(lower.position-upper.position,direction).normalized;
   if(pole.sqrMagnitude<.01f)pole=Vector3.ProjectOnPlane(-transform.forward,direction).normalized;
   if(pole.sqrMagnitude<.01f)pole=Vector3.ProjectOnPlane(transform.right,direction).normalized;
   float along=(first*first+distance*distance-second*second)/(2*distance);
   Vector3 elbow=upper.position+direction*along+pole*Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
   upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
   lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
  }
  void AlignImportedBowAim()
  {
   var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
   var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
   var right=animator.GetBoneTransform(HumanBodyBones.RightHand);
   if(!spine || !left || !right)return;
   Vector3 sourceAim=left.position-right.position;
   if(sourceAim.sqrMagnitude<.01f)return;
   Vector3 targetAim=Camera.main?Camera.main.transform.forward:transform.forward;
   // Preserve the FBX aiming pose and correct its horizontal facing first.
   Vector3 sourceFlat=Vector3.ProjectOnPlane(sourceAim,Vector3.up);
   Vector3 targetFlat=Vector3.ProjectOnPlane(targetAim,Vector3.up);
   if(sourceFlat.sqrMagnitude<.0001f || targetFlat.sqrMagnitude<.0001f)return;
   spine.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(sourceFlat,targetFlat,Vector3.up),Vector3.up)*spine.rotation;
   sourceAim=(left.position-right.position).normalized;
   float sourcePitch=Mathf.Asin(Mathf.Clamp(sourceAim.y,-1,1))*Mathf.Rad2Deg;
   float targetPitch=Mathf.Asin(Mathf.Clamp(targetAim.y,-1,1))*Mathf.Rad2Deg;
   float pitchCorrection=sourcePitch-targetPitch;
   Vector3 hinge=Vector3.Cross(Vector3.up,targetFlat.normalized).normalized;
   // The torso contributes only a small, bounded bend; the arms supply the remaining elevation.
   float bodyPitch=Mathf.Clamp(pitchCorrection*Mathf.Clamp01(BowBodyPitchShare),-BowBodyPitchLimit,BowBodyPitchLimit);
   spine.rotation=Quaternion.AngleAxis(bodyPitch,hinge)*spine.rotation;
   Quaternion armPitch=Quaternion.AngleAxis(pitchCorrection-bodyPitch,hinge);
   var leftArm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
   var rightArm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
   if(leftArm)leftArm.rotation=armPitch*leftArm.rotation;
   if(rightArm)rightArm.rotation=armPitch*rightArm.rotation;
  }
  void FitAimingBow(bool aiming)
  {
   if(!bow || !bow.activeSelf)return;
   var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
   var index=animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
   var little=animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
   if(!hand)return;
   Vector3 grip=hand.position;
   if(index && little)grip=Vector3.Lerp(grip,(index.position+little.position)*.5f,.65f);
   Vector3 shot=aiming && Camera.main?Camera.main.transform.forward:transform.forward;
   Vector3 up=Vector3.ProjectOnPlane(Vector3.up,shot).normalized;
   if(aiming && index && little)
   {
    Vector3 cant=Vector3.ProjectOnPlane(index.position-little.position,shot).normalized;
    if(cant.sqrMagnitude>.01f){if(Vector3.Dot(cant,up)<0)cant=-cant;up=cant;}
   }
   if(up.sqrMagnitude<.01f)up=Vector3.ProjectOnPlane(transform.up,shot).normalized;
   // Mesh inspection: length is +Y, string lies on +X, and the built-in arrow points -X.
   // Map model -X to the shot direction so the bowstring stays toward the drawing hand.
   Vector3 normal=Vector3.Cross(up,shot).normalized;
   bow.transform.SetPositionAndRotation(grip+hand.TransformVector(BowGripOffset),Quaternion.LookRotation(normal,up)*Quaternion.Euler(BowGripAdjustment));
  }
  void FitGrip(GameObject model,HumanBodyBones handBone,HumanBodyBones indexBone,HumanBodyBones littleBone,Vector3 offset,Vector3 adjustment)
  {
   if(!model || !model.activeSelf)return;
   var hand=animator.GetBoneTransform(handBone);var index=animator.GetBoneTransform(indexBone);var little=animator.GetBoneTransform(littleBone);
   if(!hand || !index || !little)return;
   Vector3 knuckles=(index.position+little.position)*.5f;
   Vector3 shaft=(index.position-little.position).normalized;
   // In two-handed greatsword poses, both wrists define the handle's shaft.
   if(model==great && UsesWeaponPose)
   {
    var other=animator.GetBoneTransform(HumanBodyBones.LeftHand);
    if(other)
    {
     Vector3 between=hand.position-other.position;
     if(between.magnitude>.08f && between.magnitude<.6f && Vector3.Dot(between.normalized,shaft)>.25f)
      shaft=between.normalized;
    }
   }
   Vector3 normal=Vector3.Cross(shaft,knuckles-hand.position).normalized;
   if(normal.sqrMagnitude<.01f)return;
   model.transform.position=Vector3.Lerp(hand.position,knuckles,.65f)+hand.TransformVector(offset);
   model.transform.rotation=Quaternion.LookRotation(normal,shaft)*Quaternion.Euler(adjustment);
  }
  void ShowWeapons(){if(sword)sword.SetActive(CurrentWeapon==Weapon.Sword);if(great)great.SetActive(CurrentWeapon==Weapon.Greatsword);if(bow)bow.SetActive(CurrentWeapon==Weapon.Bow);}
  Material MakeMaterial(string name,Color color,float metal){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.35f);m.SetFloat("_Cull",0);return m;}
  void BuildWeapons()
  {
   steel=MakeMaterial("Weapon Steel",new Color(.66f,.69f,.72f),.75f);wood=MakeMaterial("Weapon Dark Wood",new Color(.25f,.12f,.055f),0);
   var right=animator.GetBoneTransform(HumanBodyBones.RightHand);var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
   sword=SwordModelPrefab?HeldModel(right,"Elucidator Sword",SwordModelPrefab,SwordGripOffset,SwordGripRotation):Blade(right,"Sword",1f);great=GreatswordModelPrefab?HeldModel(right,"Inferno Cleaver",GreatswordModelPrefab,GreatswordGripOffset,GreatswordGripRotation):Blade(right,"Greatsword",1.7f);
   if(BowModelPrefab){bow=HeldModel(left,"Great Eagle Bow",BowModelPrefab,BowGripOffset,BowGripRotation);return;}
   bow=new GameObject("Longbow");bow.transform.SetParent(left,false);bow.transform.localRotation=Quaternion.Euler(0,0,-90);var line=bow.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=17;line.startWidth=line.endWidth=.045f;line.sharedMaterial=wood;
   for(int i=0;i<17;i++){float y=(i/16f-.5f)*1.6f;line.SetPosition(i,new Vector3(.3f*(1-Mathf.Pow(y/.8f,2)),y,0));}
   var stringGo=new GameObject("Bowstring");stringGo.transform.SetParent(bow.transform,false);var str=stringGo.AddComponent<LineRenderer>();str.useWorldSpace=false;str.positionCount=2;str.startWidth=str.endWidth=.008f;str.sharedMaterial=steel;str.SetPosition(0,new Vector3(0,-.8f,0));str.SetPosition(1,new Vector3(0,.8f,0));
  }
  GameObject HeldModel(Transform hand,string name,GameObject prefab,Vector3 offset,Vector3 rotation)
  {
   var go=Instantiate(prefab,hand,false);go.name=name;go.transform.localPosition=offset;go.transform.localRotation=Quaternion.Euler(rotation);go.transform.localScale=Vector3.one;
   foreach(var col in go.GetComponentsInChildren<Collider>())Destroy(col);return go;
  }
  GameObject Blade(Transform hand,string name,float length)
  {
   var root=new GameObject(name);root.transform.SetParent(hand,false);root.transform.localRotation=Quaternion.Euler(0,0,-90);Part(root.transform,"Grip",new Vector3(0,-.12f,0),new Vector3(.065f,.3f,.065f),wood);Part(root.transform,"Crossguard",new Vector3(0,.06f,0),new Vector3(length*.32f,.055f,.09f),steel);
   var blade=new GameObject("Blade");blade.transform.SetParent(root.transform,false);var mesh=new Mesh();float w=length*.065f;mesh.vertices=new[]{new Vector3(-w,.08f,0),new Vector3(w,.08f,0),new Vector3(w,length*.85f,0),new Vector3(0,length,0),new Vector3(-w,length*.85f,0)};mesh.triangles=new[]{0,1,2,0,2,4,4,2,3};mesh.RecalculateNormals();blade.AddComponent<MeshFilter>().sharedMesh=mesh;blade.AddComponent<MeshRenderer>().sharedMaterial=steel;return root;
  }
  static GameObject Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;Destroy(go.GetComponent<Collider>());return go;}
 }
 public class WeaponArrow : MonoBehaviour
 {
  public Transform Owner;public Vector3 Velocity;public float Damage=35;float life;
  void Update(){float step=Time.deltaTime;Vector3 delta=Velocity*step;RaycastHit[] hits=Physics.RaycastAll(transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));foreach(var h in hits){if(Owner&&h.transform.IsChildOf(Owner))continue;transform.position=h.point;var enemy=h.collider.GetComponentInParent<CQCTrainingTarget>();if(enemy)enemy.ReceivePunch(Damage,true);enabled=false;Destroy(gameObject,2);return;}transform.position+=delta;Velocity+=Vector3.down*4.9f*step;transform.rotation=Quaternion.LookRotation(Velocity);life+=step;if(life>8)Destroy(gameObject);}
 }
}







