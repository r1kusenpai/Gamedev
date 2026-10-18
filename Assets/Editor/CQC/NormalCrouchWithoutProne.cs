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
    public static class NormalCrouchWithoutProne
    {
        const string Request="Assets/Editor/CQC/NormalCrouchWithoutProne-v1.request.txt";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static double next;
        static NormalCrouchWithoutProne(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating
                ||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception e)
            {
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/NormalCrouchWithoutProne-error.txt",e.ToString());Debug.LogException(e);
                AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");
            }
        }
        [MenuItem("Tools/Player/Normal Crouch With Sword And No Prone")]
        public static void Apply()
        {
            var player=GameObject.Find("PlayerArmature");var weapon=player?player.GetComponent<WeaponController>():null;
            if(!weapon||!player.GetComponent<CQCPosture>())throw new InvalidOperationException("Player sword and crouch controllers are required.");
            if(Enum.GetNames(typeof(CQCPosture.Stance)).Length!=2||Enum.IsDefined(typeof(CQCPosture.Stance),2))
                throw new InvalidOperationException("Only standing and crouching should remain.");
            Directory.CreateDirectory("Logs");var report=new StringBuilder("Normal crouch arms with sword; prone removed\n");
            foreach(int mode in new[]{0,1,2})
            {
                var normal=Sample(player,false,mode,report);
                var sword=Sample(player,true,mode,report);
                float maxError=0;
                if(normal.Length!=sword.Length)throw new InvalidOperationException("Crouch comparison is incomplete.");
                for(int i=0;i<normal.Length;i++)maxError=Mathf.Max(maxError,Quaternion.Angle(normal[i],sword[i]));
                if(maxError>.15f)throw new InvalidOperationException("Sword changed the normal crouch arm pose: mode="+mode+"; error="+maxError);
                report.AppendLine("Crouch "+(mode==0?"idle":mode==1?"walk":"run")+": normal versus sword arm pose error="+maxError.ToString("F3")+" degrees across 20 frames and six arm/hand bones.");
            }
            var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-normal-crouch-no-prone-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Crouch scene backup failed.");
            Undo.RecordObject(weapon,"Use normal crouch arms with sword");weapon.SwordUseNormalLocomotion=true;weapon.SwordRelaxedCarry=false;
            EditorUtility.SetDirty(weapon);PrefabUtility.RecordPrefabInstancePropertyModifications(weapon);
            // Reserialize posture to drop removed crawling settings from the saved scene.
            EditorUtility.SetDirty(player.GetComponent<CQCPosture>());EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save normal crouch setup.");
            report.AppendLine("Removed the third stance, Z binding, crawling/dive pose and movement, low camera offset, low collider path and prone-only weapon restrictions. C toggles standing/crouching. Normal crouch walk/run and all sword attack inputs retained. Existing head stability retained.");
            report.AppendLine("Backup: "+backup);File.WriteAllText("Logs/NormalCrouchWithoutProne-report.txt",report.ToString());
            Debug.Log("Normal crouch arms verified with sword attached. Prone removed; C crouch and crouch running retained. Scene saved.");
        }
        static Quaternion[] Sample(GameObject original,bool sword,int mode,StringBuilder report)
        {
            PreviewRenderUtility preview=null;PlayableGraph graph=default;WeaponController weapon=null;
            try
            {
                preview=new PreviewRenderUtility();var clone=UnityEngine.Object.Instantiate(original);
                clone.transform.SetPositionAndRotation(new Vector3(0,1000,0),Quaternion.identity);
                foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                foreach(var collider in clone.GetComponentsInChildren<Collider>())collider.enabled=false;
                preview.AddSingleGO(clone);
                var posture=clone.GetComponent<CQCPosture>();var clips=clone.GetComponent<CQCCrouchClips>();
                if(!clips){clips=clone.AddComponent<CQCCrouchClips>();clips.enabled=false;}
                var animator=clone.GetComponent<Animator>();var input=clone.GetComponent<StarterAssetsInputs>();
                var movement=clone.GetComponent<ThirdPersonController>();var controller=clone.GetComponent<CharacterController>();
                weapon=clone.GetComponent<WeaponController>();weapon.CurrentWeapon=sword?WeaponController.Weapon.Sword:WeaponController.Weapon.Unarmed;
                weapon.SwordUseNormalLocomotion=true;weapon.SwordRelaxedCarry=false;movement.Grounded=true;
                input.move=mode==0?Vector2.zero:Vector2.up;input.sprint=mode==2;
                Invoke(posture,"Awake");Invoke(clips,"Awake");Invoke(weapon,"Start");
                graph=clips.WeaponGraph;
                if(!graph.IsValid())throw new InvalidOperationException("Crouch graph did not initialize.");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);animator.applyRootMotion=false;
                clips.SetControllerBool(Animator.StringToHash("Grounded"),true);
                clips.SetControllerBool(Animator.StringToHash("Jump"),false);
                clips.SetControllerBool(Animator.StringToHash("FreeFall"),false);
                clips.SetControllerFloat(Animator.StringToHash("Speed"),0);
                clips.SetControllerFloat(Animator.StringToHash("MotionSpeed"),1);
                for(int frame=0;frame<30;frame++)graph.Evaluate(1f/60);
                Invoke(weapon,"CalibrateStableSwordGrip");
                float standingHeight=controller.height,bottom=controller.center.y-controller.height*.5f;
                if(posture.TryChangeStance((CQCPosture.Stance)2)||posture.CurrentStance!=CQCPosture.Stance.Standing||controller.height!=standingHeight)
                    throw new InvalidOperationException("Removed stance remains accessible.");
                if(!posture.TryChangeStance(CQCPosture.Stance.Crouching)||controller.height>=standingHeight
                    ||Mathf.Abs(controller.center.y-controller.height*.5f-bottom)>.001f)
                    throw new InvalidOperationException("Crouch collider failed to shrink with the same foot height.");
                Set(clips,"<IsTransitioning>k__BackingField",false);Set(clips,"transitionInput",0);Set(posture,"stanceBlend",1f);
                Set(clips,"walkWeight",mode>0&&(input.sprint||clips.UseWalkingClipWithoutShift)?1f:0f);
                Set(clips,"crouchArmWeight",mode==1&&!clips.UseWalkingClipWithoutShift?1f:0f);
                clips.SetControllerFloat(Animator.StringToHash("Speed"),mode==0?0:mode==1?posture.CrouchSpeed:posture.CrouchSprintSpeed);
                var armBones=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                    HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand};
                var poses=new Quaternion[20*armBones.Length];
                var head=clone.GetComponent<PlayerHeadStability>();
                var overlay=(AnimationLayerMixerPlayable)typeof(WeaponController).GetField("overlay",Private).GetValue(weapon);
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                var model=(GameObject)typeof(WeaponController).GetField("sword",Private).GetValue(weapon);
                var anchor=(Vector3)typeof(WeaponController).GetField("stableSwordGripPosition",Private).GetValue(weapon)+weapon.SwordGripOffset;
                float gripError=0;
                for(int frame=0;frame<20;frame++)
                {
                    Invoke(posture,"RestorePose");Invoke(clips,"Update");
                    if(sword)
                    {
                        Invoke(weapon,"ResetSwordLowerLayer");
                        typeof(WeaponController).GetMethod("UpdateSword",Private).Invoke(weapon,new object[]{false,false,false,true});
                        if(weapon.UsesWeaponPose||overlay.GetInputWeight(1)>.001f)throw new InvalidOperationException("Sword crouch pose still overrides normal crouching.");
                    }
                    graph.Evaluate(1f/30);Invoke(posture,"LateUpdate");if(head)head.StabilizeHead();
                    if(sword)
                    {
                        Invoke(weapon,"LateUpdate");
                        gripError=Mathf.Max(gripError,Vector3.Distance(hand.InverseTransformPoint(model.transform.position),anchor));
                    }
                    for(int bone=0;bone<armBones.Length;bone++)poses[frame*armBones.Length+bone]=animator.GetBoneTransform(armBones[bone]).localRotation;
                }
                if(gripError>.001f)throw new InvalidOperationException("Sword detached during normal crouch.");
                if(sword&&mode==0)
                {
                    preview.camera.transform.position=new Vector3(2.3f,1001.7f,3.4f);
                    preview.camera.transform.LookAt(new Vector3(0,1000.8f,0));preview.camera.fieldOfView=42;
                    preview.camera.nearClipPlane=.05f;preview.camera.farClipPlane=30;
                    preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);preview.lights[1].intensity=1;
                    FitSwordShoulder.Capture(preview,"Logs/NormalCrouchWithSword-preview.png",clone);
                }
                if(!posture.TryChangeStance(CQCPosture.Stance.Standing)||Mathf.Abs(controller.height-standingHeight)>.001f)
                    throw new InvalidOperationException("Standing collider was not restored after crouching.");
                report.AppendLine((sword?"Sword":"Unarmed")+" mode="+mode+": removed stance rejected, crouch/stand collider verified; grip error="+gripError.ToString("F5")+"m.");
                return poses;
            }
            finally
            {
                if(graph.IsValid())graph.Destroy();
                if(weapon)
                {
                    foreach(string field in new[]{"upperMask","fullMask","greatGripMask","steel","wood"})
                    {
                        var asset=typeof(WeaponController).GetField(field,Private).GetValue(weapon) as UnityEngine.Object;
                        if(asset&&!AssetDatabase.Contains(asset))UnityEngine.Object.DestroyImmediate(asset);
                    }
                }
                if(preview!=null)preview.Cleanup();
            }
        }
        static void Invoke(object target,string name){target.GetType().GetMethod(name,Private).Invoke(target,null);}
        static void Set(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
    }
}
#endif
