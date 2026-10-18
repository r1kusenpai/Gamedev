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
    public static class NormalSwordStance
    {
        const string Request="Assets/Editor/CQC/NormalSwordStance-v4.request.txt";
        static double next;
        static NormalSwordStance(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating
                ||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception e)
            {
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/NormalSwordStance-error.txt",e.ToString());Debug.LogException(e);
                AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");
            }
        }
        [MenuItem("Tools/Weapons/Use Normal Body Pose With Sword")]
        public static void Apply()
        {
            var player=GameObject.Find("PlayerArmature");var weapon=player?player.GetComponent<WeaponController>():null;
            if(!weapon||!weapon.SwordModelPrefab||!weapon.SwordAnimations||!weapon.SwordAnimations.IsReady)
                throw new InvalidOperationException("The player's sword model and attack animations are required.");
            Directory.CreateDirectory("Logs");var report=new StringBuilder("Normal character posture with sword in right hand\n");
            Verify(player,report);
            var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-normal-sword-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Normal sword stance backup failed.");
            Undo.RecordObject(weapon,"Use normal stance holding sword");
            weapon.SwordUseNormalLocomotion=true;weapon.SwordRelaxedCarry=false;
            weapon.SwordNormalBladeDirection=new Vector3(.16f,-.55f,.80f);
            weapon.CurrentWeapon=WeaponController.Weapon.Sword;
            EditorUtility.SetDirty(weapon);PrefabUtility.RecordPrefabInstancePropertyModifications(weapon);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save normal sword stance.");
            report.AppendLine("Normal unarmed standing/walking/running body, arm and hand pose with only the sword attached to the right palm. No procedural shoulder, hip, arm or wrist adjustment. Sword uses a fixed hand-local grip calibrated from normal idle. Steady head retained. Sword attacks, crouching and held-repeat inputs remain.");
            report.AppendLine("Backup: "+backup);File.WriteAllText("Logs/NormalSwordStance-report.txt",report.ToString());
            Debug.Log("Normal character stance restored with sword held in the right hand. Attacks retained. Scene saved.");
        }
        static void Verify(GameObject original,StringBuilder report)
        {
            PreviewRenderUtility preview=null;PlayableGraph graph=default;
            try
            {
                preview=new PreviewRenderUtility();var clone=UnityEngine.Object.Instantiate(original);
                clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                foreach(var collider in clone.GetComponentsInChildren<Collider>())collider.enabled=false;
                preview.AddSingleGO(clone);
                var animator=clone.GetComponent<Animator>();var weapon=clone.GetComponent<WeaponController>();
                if(!animator||!animator.runtimeAnimatorController||!animator.isHuman)throw new InvalidOperationException("Original character locomotion controller is missing.");
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                var index=animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                var little=animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
                var model=UnityEngine.Object.Instantiate(weapon.SwordModelPrefab,hand,false);model.transform.localScale=Vector3.one;
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(WeaponController).GetField("animator",flags).SetValue(weapon,animator);
                typeof(WeaponController).GetField("sword",flags).SetValue(weapon,model);
                var fit=typeof(WeaponController).GetMethod("FitGrip",flags);
                var calibrate=typeof(WeaponController).GetMethod("CalibrateStableSwordGrip",flags);
                var headStability=clone.GetComponent<PlayerHeadStability>();
                bool calibrated=false;Vector3 localAnchor=Vector3.zero;Quaternion localRotation=Quaternion.identity;
                object[] args={model,HumanBodyBones.RightHand,HumanBodyBones.RightIndexProximal,HumanBodyBones.RightLittleProximal,weapon.SwordGripOffset,weapon.SwordGripAdjustment};
                graph=PlayableGraph.Create("Original Sword Stance Verification");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var controller=AnimatorControllerPlayable.Create(graph,animator.runtimeAnimatorController);
                controller.SetBool("Grounded",true);controller.SetBool("Jump",false);controller.SetBool("FreeFall",false);
                controller.SetFloat("MotionSpeed",1);
                var output=AnimationPlayableOutput.Create(graph,"Normal posture",animator);output.SetSourcePlayable(controller);graph.Play();
                preview.camera.transform.position=new Vector3(2.3f,1.9f,3.6f);
                preview.camera.transform.LookAt(new Vector3(.15f,1f,.10f));preview.camera.fieldOfView=42;
                preview.camera.nearClipPlane=.05f;preview.camera.farClipPlane=30;
                preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);preview.lights[1].intensity=1f;
                foreach(float speed in new[]{0f,2f,5.3f})
                {
                    controller.SetFloat("Speed",speed);
                    for(int frame=0;frame<90;frame++)graph.Evaluate(1f/60);
                    float maxGripError=0,maxLean=0,lowestTip=float.MaxValue;
                    for(int frame=0;frame<12;frame++)
                    {
                        graph.Evaluate(1f/30);
                        if(headStability&&headStability.enabled==false)headStability.StabilizeHead();
                        if(!calibrated)
                        {
                            calibrate.Invoke(weapon,null);calibrated=true;
                            localAnchor=(Vector3)typeof(WeaponController).GetField("stableSwordGripPosition",flags).GetValue(weapon);
                            localRotation=(Quaternion)typeof(WeaponController).GetField("stableSwordGripRotation",flags).GetValue(weapon)*Quaternion.Euler(weapon.SwordGripAdjustment);
                        }
                        Quaternion handBefore=hand.localRotation;
                        Vector3 handPositionBefore=hand.position;
                        fit.Invoke(weapon,args);
                        if(Quaternion.Angle(hand.localRotation,handBefore)>.05f||Vector3.Distance(hand.position,handPositionBefore)>.0001f)
                            throw new InvalidOperationException("Sword attachment changed the normal hand pose.");
                        Vector3 palm=Vector3.Lerp(hand.position,(index.position+little.position)*.5f,.65f)+hand.TransformVector(weapon.SwordGripOffset);
                        maxGripError=Mathf.Max(maxGripError,Vector3.Distance(model.transform.position,palm));
                        maxLean=Mathf.Max(maxLean,Vector3.Angle(chest.position-hips.position,clone.transform.up));
                        lowestTip=Mathf.Min(lowestTip,clone.transform.InverseTransformPoint(model.transform.TransformPoint(new Vector3(0,1.2f,0))).y);
                        VerifyAttachment(hand,model.transform,localAnchor+weapon.SwordGripOffset,localRotation);
                        if(speed==0&&frame==0)FitSwordShoulder.Capture(preview,"Logs/NormalSwordStance-preview.png",clone);
                    }
                    if(maxGripError>.025f||maxLean>30||lowestTip<.03f)throw new InvalidOperationException("Normal sword stance failed grip/upright/ground verification: speed="+speed+"; grip="+maxGripError+"; lean="+maxLean+"; tip="+lowestTip);
                    report.AppendLine("Original locomotion speed="+speed+": 12 samples; palm error="+maxGripError.ToString("F5")+"m; maximum torso lean="+maxLean.ToString("F1")+" degrees; lowest tip="+lowestTip.ToString("F3")+"m.");
                }
                var set=weapon.SwordAnimations;
                foreach(var clip in new[]{set.Combo1,set.Combo2,set.Combo3,set.ChargedAttack,set.RunningAttack,set.AirAttack,set.CrouchIdle,set.CrouchWalk,set.Jump})
                {
                    var action=AnimationClipPlayable.Create(graph,clip);action.SetApplyFootIK(false);output.SetSourcePlayable(action);
                    for(int frame=0;frame<16;frame++)
                    {
                        action.SetTime(clip.length*frame/16f);graph.Evaluate(0);
                        if(headStability)headStability.StabilizeHead();
                        fit.Invoke(weapon,args);
                        VerifyAttachment(hand,model.transform,localAnchor+weapon.SwordGripOffset,localRotation);
                    }
                    report.AppendLine(clip.name+": 16 samples with steady head; fixed sword-to-hand position and rotation verified.");
                }
            }
            finally{if(graph.IsValid())graph.Destroy();if(preview!=null)preview.Cleanup();}
        }
        static void VerifyAttachment(Transform hand,Transform sword,Vector3 position,Quaternion rotation)
        {
            if(Vector3.Distance(hand.InverseTransformPoint(sword.position),position)>.001f
                ||Quaternion.Angle(Quaternion.Inverse(hand.rotation)*sword.rotation,rotation)>.1f)
                throw new InvalidOperationException("Sword slipped or rotated relative to the right-hand attachment.");
        }
    }
}
#endif
