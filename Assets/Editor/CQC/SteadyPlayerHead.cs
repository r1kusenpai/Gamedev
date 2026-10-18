#if UNITY_EDITOR
using System;
using System.IO;
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
    public static class SteadyPlayerHead
    {
        const string Request="Assets/Editor/CQC/SteadyPlayerHead-v1.request.txt";
        static double next;
        static SteadyPlayerHead(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||File.Exists("Assets/Editor/CQC/NormalSwordStance-v2.request.txt")
                ||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode
                ||SceneManager.GetActiveScene().name!="MyForest")return;
            try{Apply();AssetDatabase.DeleteAsset(Request);}
            catch(Exception e)
            {
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SteadyPlayerHead-error.txt",e.ToString());Debug.LogException(e);
                AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");
            }
        }
        [MenuItem("Tools/Player/Disable Extra Head And Neck Animation")]
        public static void Apply()
        {
            var player=GameObject.Find("PlayerArmature");var animator=player?player.GetComponent<Animator>():null;
            var weapon=player?player.GetComponent<WeaponController>():null;
            if(!animator||!animator.isHuman||!weapon||!weapon.SwordAnimations)
                throw new InvalidOperationException("The player's Humanoid avatar and sword set are required.");
            var report=new StringBuilder("Player neck/head animation disabled\n");Verify(player,weapon.SwordAnimations,report);
            var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-steady-player-head-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Player head backup failed.");
            var head=player.GetComponent<PlayerHeadStability>();if(!head)head=Undo.AddComponent<PlayerHeadStability>(player);
            Undo.RecordObject(head,"Keep player head neutral");
            if(!head.CaptureNeutralPose())throw new InvalidOperationException("Player head bone is missing.");
            head.HeadAnimationWeight=0;head.enabled=true;EditorUtility.SetDirty(head);
            PrefabUtility.RecordPrefabInstancePropertyModifications(head);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save head stability.");
            report.AppendLine("Player only. Neutral neck/head local rotations and positions are restored after other animation/procedural updates. Normal body movement and body turning remain. Camera, sword controls and NPCs retained. Head Animation Weight=0 (no separate neck/head animation); increase toward 1 to restore it.");
            report.AppendLine("Backup: "+backup);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SteadyPlayerHead-report.txt",report.ToString());
            Debug.Log("Player head and neck animation disabled; normal body movement retained. Scene saved.");
        }
        static void Verify(GameObject original,SwordAnimationSet set,StringBuilder report)
        {
            var scene=EditorSceneManager.NewPreviewScene();GameObject clone=null;PlayableGraph graph=default;
            try
            {
                clone=UnityEngine.Object.Instantiate(original);SceneManager.MoveGameObjectToScene(clone,scene);
                foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                foreach(var collider in clone.GetComponentsInChildren<Collider>())collider.enabled=false;
                var animator=clone.GetComponent<Animator>();animator.runtimeAnimatorController=null;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;animator.Rebind();
                var stable=clone.GetComponent<PlayerHeadStability>();if(!stable)stable=clone.AddComponent<PlayerHeadStability>();
                stable.HeadAnimationWeight=0;if(!stable.CaptureNeutralPose())throw new InvalidOperationException("No player head bone.");
                var head=animator.GetBoneTransform(HumanBodyBones.Head);var neck=animator.GetBoneTransform(HumanBodyBones.Neck);
                var chest=animator.GetBoneTransform(HumanBodyBones.Chest);var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                Quaternion neutralHead=head.localRotation,neutralNeck=neck?neck.localRotation:Quaternion.identity;
                foreach(var clip in new[]{set.Idle,set.Walk,set.Run,set.Jump,set.CrouchIdle,set.CrouchWalk,
                    set.Combo1,set.Combo2,set.Combo3,set.ChargedAttack,set.RunningAttack,set.AirAttack})
                {
                    graph=PlayableGraph.Create("Player Head Verification");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);
                    var output=AnimationPlayableOutput.Create(graph,"Head preview",animator);output.SetSourcePlayable(playable);graph.Play();
                    float error=0;
                    for(int frame=0;frame<16;frame++)
                    {
                        playable.SetTime(clip.length*frame/16f);graph.Evaluate(0);
                        Quaternion chestBefore=chest.localRotation,handBefore=hand.localRotation;
                        stable.StabilizeHead();
                        error=Mathf.Max(error,Quaternion.Angle(head.localRotation,neutralHead));
                        if(neck)error=Mathf.Max(error,Quaternion.Angle(neck.localRotation,neutralNeck));
                        if(Quaternion.Angle(chestBefore,chest.localRotation)>.05f||Quaternion.Angle(handBefore,hand.localRotation)>.05f)
                            throw new InvalidOperationException("Head adjustment altered the body or sword hand.");
                    }
                    if(error>.1f)throw new InvalidOperationException("Head animation was not suppressed: "+clip.name);
                    report.AppendLine(clip.name+": 16 samples; neutral neck/head rotation error="+error.ToString("F3")+" degrees; chest and sword hand unchanged.");
                    graph.Destroy();
                }
            }
            finally{if(graph.IsValid())graph.Destroy();if(clone)UnityEngine.Object.DestroyImmediate(clone);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
#endif
