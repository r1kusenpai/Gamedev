#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad]
    public static class FixGreatswordCrouchAttacks
    {
        const string Request="Assets/Editor/CQC/FixGreatswordCrouchAttacks-v1.request.txt";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static double next;
        static FixGreatswordCrouchAttacks(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating
                ||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception e)
            {
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GreatswordCrouchAttacks-error.txt",e.ToString());Debug.LogException(e);
                AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");
            }
        }
        [MenuItem("Tools/Weapons/Verify Fixed Greatsword Crouch Attacks")]
        public static void Apply()
        {
            var player=GameObject.Find("PlayerArmature");
            if(!player||!player.GetComponent<WeaponController>())throw new InvalidOperationException("Player weapon controller is required.");
            Directory.CreateDirectory("Logs");var report=new StringBuilder("Greatsword crouch attack fix\n");
            foreach(int mode in new[]{0,1,2})Verify(player,mode,report);
            var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-greatsword-crouch-fix-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true)||!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save greatsword crouch backup/scene.");
            report.AppendLine("Crouched greatsword actions always use an upper-body layer. Procedural crouching keeps its hip drop and leg IK but preserves animated torso rotation and both arms. Crouch + Shift no longer selects the full-body slide attack. Crouched attacks suppress action root motion. Standing run/jump selections, held-repeat timing, sword, head stability and removed-prone state retained.");
            report.AppendLine("Backup: "+backup);File.WriteAllText("Logs/GreatswordCrouchAttacks-report.txt",report.ToString());
            Debug.Log("Greatsword crouch attacks verified: animated torso/arms retained, crouch legs stable, no crouched slide/root motion. Scene saved.");
        }
        static void Verify(GameObject original,int mode,StringBuilder report)
        {
            PreviewRenderUtility preview=null;PlayableGraph graph=default;WeaponController weapon=null;
            try
            {
                preview=new PreviewRenderUtility();var clone=UnityEngine.Object.Instantiate(original);
                clone.transform.SetPositionAndRotation(new Vector3(0,1000,0),Quaternion.identity);
                foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                foreach(var partCollider in clone.GetComponentsInChildren<Collider>())partCollider.enabled=false;
                preview.AddSingleGO(clone);
                var animator=clone.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var posture=clone.GetComponent<CQCPosture>();var source=clone.GetComponent<CQCCrouchClips>();
                if(!source){source=clone.AddComponent<CQCCrouchClips>();source.enabled=false;}
                var input=clone.GetComponent<StarterAssetsInputs>();var motor=clone.GetComponent<ThirdPersonController>();
                var collider=clone.GetComponent<CharacterController>();
                weapon=clone.GetComponent<WeaponController>();weapon.CurrentWeapon=WeaponController.Weapon.Greatsword;
                input.move=mode==0?Vector2.zero:Vector2.up;input.sprint=mode==2;motor.Grounded=true;
                Call(posture,"Awake");Call(source,"Awake");Call(weapon,"Start");
                graph=source.WeaponGraph;graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);animator.applyRootMotion=false;
                source.SetControllerBool(Animator.StringToHash("Grounded"),true);
                source.SetControllerBool(Animator.StringToHash("Jump"),false);source.SetControllerBool(Animator.StringToHash("FreeFall"),false);
                source.SetControllerFloat(Animator.StringToHash("MotionSpeed"),1);
                source.SetControllerFloat(Animator.StringToHash("Speed"),mode==0?0:mode==1?posture.CrouchSpeed:posture.CrouchSprintSpeed);
                graph.Evaluate(.5f);
                if(!posture.TryChangeStance(CQCPosture.Stance.Crouching))throw new InvalidOperationException("Cannot enter crouch.");
                Put(source,"<IsTransitioning>k__BackingField",false);Put(source,"transitionInput",0);Put(posture,"stanceBlend",1f);
                Put(source,"walkWeight",mode>0&&(input.sprint||source.UseWalkingClipWithoutShift)?1f:0f);
                Put(source,"crouchArmWeight",mode==1&&!source.UseWalkingClipWithoutShift?1f:0f);
                float crouchHeight=collider.height;
                var bones=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                    HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand};
                var chest=animator.GetBoneTransform(HumanBodyBones.Chest);var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                var leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var head=clone.GetComponent<PlayerHeadStability>();
                foreach(string name in new[]{"great sword slash","great sword slash (3)","great sword slash (5)","great sword slash (4)","great sword held slash","great sword attack","draw a great sword 1"})
                {
                    var clip=Call(weapon,"Clip","Great",name) as AnimationClip;
                    if(!clip||!clip.isHumanMotion)throw new InvalidOperationException("Greatsword clip is missing: "+name);
                    Call(weapon,"BeginAction",clip,name.Contains("draw")?0f:weapon.GreatswordDamage,false);
                    var players=(AnimationClipPlayable[])Field(weapon,"players");int slot=(int)Field(weapon,"currentSlot");
                    var mixer=(AnimationMixerPlayable)Field(weapon,"clips");mixer.SetInputWeight(slot,1);mixer.SetInputWeight(1-slot,0);
                    float maxArm=0,maxChest=0,maxFeet=0;
                    for(int frame=0;frame<16;frame++)
                    {
                        Call(posture,"RestorePose");Call(source,"Update");Call(weapon,"ConfigureWeaponPoseMask",true);
                        players[slot].SetTime(clip.length*(frame+.25f)/16f);graph.Evaluate(0);
                        var arms=new Quaternion[bones.Length];for(int i=0;i<bones.Length;i++)arms[i]=animator.GetBoneTransform(bones[i]).rotation;
                        var torso=chest.rotation;var left=leftFoot.position;var right=rightFoot.position;
                        float handSpan=Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position,animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                        float hipHeight=hips.position.y;bool imported=source.UsesImportedPose;
                        Call(posture,"LateUpdate");
                        for(int i=0;i<bones.Length;i++)maxArm=Mathf.Max(maxArm,Quaternion.Angle(arms[i],animator.GetBoneTransform(bones[i]).rotation));
                        maxChest=Mathf.Max(maxChest,Quaternion.Angle(torso,chest.rotation));
                        maxFeet=Mathf.Max(maxFeet,Vector3.Distance(left,leftFoot.position),Vector3.Distance(right,rightFoot.position));
                        float span=Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position,animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                        if(Mathf.Abs(span-handSpan)>.002f)throw new InvalidOperationException("Crouch correction separated the two-handed grip: "+name);
                        if(!imported&&Mathf.Abs(hipHeight-hips.position.y-posture.CrouchDrop)>.002f)
                            throw new InvalidOperationException("Crouch hip lowering was lost: "+name);
                        if(!weapon.PreservesGreatswordCrouchArms||!weapon.PreservesGreatswordCrouchTorso||weapon.UseActionRootMotion||Mathf.Abs(collider.height-crouchHeight)>.001f)
                            throw new InvalidOperationException("Greatsword crouch layer/root motion/collider contract failed: "+name);
                        if(head)head.StabilizeHead();Call(weapon,"LateUpdate");
                        if(mode==0&&name=="great sword held slash"&&frame==6)
                        {
                            preview.camera.transform.position=clone.transform.position+new Vector3(2.6f,1.7f,3.8f);
                            preview.camera.transform.LookAt(clone.transform.position+new Vector3(0,.9f,0));preview.camera.fieldOfView=45;
                            preview.camera.nearClipPlane=.05f;preview.camera.farClipPlane=30;
                            preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);preview.lights[1].intensity=1;
                            FitSwordShoulder.Capture(preview,"Logs/GreatswordCrouchAttack-preview.png",clone);
                        }
                    }
                    if(maxArm>.15f||maxChest>.15f||maxFeet>.015f)throw new InvalidOperationException("Crouching distorted attack or feet: "+name+"; arms="+maxArm+"; chest="+maxChest+"; feet="+maxFeet);
                    report.AppendLine("Mode="+mode+" "+name+": 16 samples; upper-body rotation change="+Mathf.Max(maxArm,maxChest).ToString("F3")+"deg; foot error="+maxFeet.ToString("F4")+"m; two-hand span/crouch collider retained, root motion suppressed.");
                }
                motor.Grounded=true;input.move=Vector2.up;input.sprint=true;
                Call(weapon,"Attack",false);string crouchedClip=((AnimationClip)Field(weapon,"currentClip")).name;
                if(crouchedClip.Contains("slide")||crouchedClip.Contains("jump"))throw new InvalidOperationException("Crouched sprint still selects a full-body slide/jump.");
                motor.Grounded=false;Call(weapon,"Attack",false);
                if(((AnimationClip)Field(weapon,"currentClip")).name.Contains("jump"))throw new InvalidOperationException("Crouch selected a standing jump attack.");
                if(!posture.TryChangeStance(CQCPosture.Stance.Standing))throw new InvalidOperationException("Cannot stand after greatsword crouch attack.");
                motor.Grounded=true;Call(weapon,"Attack",false);
                if(!((AnimationClip)Field(weapon,"currentClip")).name.Contains("slide"))throw new InvalidOperationException("Standing sprint attack changed.");
                motor.Grounded=false;Call(weapon,"Attack",false);
                if(!((AnimationClip)Field(weapon,"currentClip")).name.Contains("jump"))throw new InvalidOperationException("Standing air attack changed.");
                report.AppendLine("Mode="+mode+": crouched sprint/air special selection rejected; standing slide and jump attacks retained.");
            }
            finally
            {
                if(graph.IsValid())graph.Destroy();
                if(weapon)foreach(string field in new[]{"upperMask","fullMask","greatGripMask","steel","wood"})
                {
                    var asset=Field(weapon,field) as UnityEngine.Object;if(asset&&!AssetDatabase.Contains(asset))UnityEngine.Object.DestroyImmediate(asset);
                }
                if(preview!=null)preview.Cleanup();
            }
        }
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
        static object Field(object target,string name)=>target.GetType().GetField(name,Private).GetValue(target);
        static void Put(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    }
}
#endif
