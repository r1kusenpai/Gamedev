#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad] public static class SteadyGreatswordBody
    {
        const string Request="Assets/Editor/CQC/SteadyGreatswordBody-v1.request.txt";
        static double next;
        static SteadyGreatswordBody(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}
            catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SteadyGreatswordBody-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}
        }
        [MenuItem("Tools/Weapons/Reduce Greatsword Body Bending")]
        public static void Apply()
        {
            var player=GameObject.Find("PlayerArmature");if(!player||!player.GetComponent<WeaponController>())throw new InvalidOperationException("Player missing.");
            var report=new StringBuilder("Greatsword torso motion reduction\n");Verify(player,report);
            string backup="Assets/ForestExpansion/Backups/MyForest-before-steady-greatsword-body-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            var scene=SceneManager.GetActiveScene();if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
            var stable=player.GetComponent<GreatswordBodyStability>();if(!stable)stable=Undo.AddComponent<GreatswordBodyStability>(player);
            Undo.RecordObject(stable,"Reduce exaggerated greatsword torso bending");
            if(!stable.CaptureNeutralPose())throw new InvalidOperationException("Torso bones missing.");
            stable.BodyAnimationWeight=.65f;stable.SpineLimit=14;stable.ChestLimit=14;stable.UpperChestLimit=10;stable.enabled=true;
            EditorUtility.SetDirty(stable);PrefabUtility.RecordPrefabInstancePropertyModifications(stable);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");
            report.AppendLine("Torso animation reduced to 65%, with local spine/chest limits of 14 degrees and upper-chest limit of 10 degrees. Applied before crouch correction and weapon fitting. Other weapons, arm/leg local poses, head setting, attack timing and root motion retained. Roll and stance transitions bypass this adjustment.");
            report.AppendLine("Backup: "+backup);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SteadyGreatswordBody-report.txt",report.ToString());Debug.Log("Greatsword body bending reduced and verified. Scene saved.");
        }
        static void Verify(GameObject original,StringBuilder report)
        {
            var scene=EditorSceneManager.NewPreviewScene();GameObject clone=null;PlayableGraph graph=default;
            try
            {
                clone=UnityEngine.Object.Instantiate(original);SceneManager.MoveGameObjectToScene(clone,scene);
                foreach(var b in clone.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                foreach(var c in clone.GetComponentsInChildren<Collider>())c.enabled=false;
                var animator=clone.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;animator.Rebind();
                var stable=clone.GetComponent<GreatswordBodyStability>();if(!stable)stable=clone.AddComponent<GreatswordBodyStability>();stable.enabled=false;
                stable.BodyAnimationWeight=.65f;stable.SpineLimit=14;stable.ChestLimit=14;stable.UpperChestLimit=10;
                if(!stable.CaptureNeutralPose())throw new InvalidOperationException("Neutral body capture failed.");
                var torso=new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest};var rest=new Quaternion[3];
                for(int i=0;i<3;i++){var bone=animator.GetBoneTransform(torso[i]);if(bone)rest[i]=bone.localRotation;}
                var weapon=clone.GetComponent<WeaponController>();weapon.CurrentWeapon=WeaponController.Weapon.Greatsword;
                var preserved=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand};
                foreach(string name in new[]{"great sword slash","great sword slash (3)","great sword slash (5)","great sword slash (4)","great sword held slash","great sword attack","great sword slide attack","great sword jump attack","draw a great sword 1"})
                {
                    AnimationClip clip=null;foreach(var c in Resources.LoadAll<AnimationClip>("WeaponAnimations/Great/"+name))if(c.isHumanMotion&&!c.name.StartsWith("__preview__")){clip=c;break;}
                    if(!clip)throw new InvalidOperationException("Missing greatsword clip: "+name);
                    graph=PlayableGraph.Create("Greatsword body verification");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);
                    var output=AnimationPlayableOutput.Create(graph,"Body preview",animator);output.SetSourcePlayable(playable);graph.Play();float beforeMax=0,afterMax=0;
                    for(int frame=0;frame<20;frame++)
                    {
                        playable.SetTime(clip.length*(frame+.25f)/20);graph.Evaluate(0);
                        var local=new Quaternion[preserved.Length];var positions=new Vector3[preserved.Length];
                        for(int i=0;i<preserved.Length;i++){var bone=animator.GetBoneTransform(preserved[i]);local[i]=bone.localRotation;positions[i]=bone.localPosition;}
                        for(int i=0;i<3;i++){var bone=animator.GetBoneTransform(torso[i]);if(bone)beforeMax=Mathf.Max(beforeMax,Quaternion.Angle(rest[i],bone.localRotation));}
                        stable.StabilizeBody();
                        for(int i=0;i<3;i++){var bone=animator.GetBoneTransform(torso[i]);if(!bone)continue;float angle=Quaternion.Angle(rest[i],bone.localRotation);afterMax=Mathf.Max(afterMax,angle);if(angle>(i==2?10:14)+.1f)throw new InvalidOperationException("Body bend limit failed.");}
                        for(int i=0;i<preserved.Length;i++){var bone=animator.GetBoneTransform(preserved[i]);if(Quaternion.Angle(local[i],bone.localRotation)>.1f||Vector3.Distance(positions[i],bone.localPosition)>.0001f)throw new InvalidOperationException("Body correction changed limb/hip pose.");}
                        weapon.CurrentWeapon=WeaponController.Weapon.Sword;
                        var spine=animator.GetBoneTransform(HumanBodyBones.Spine);var prior=spine.localRotation;stable.StabilizeBody();if(Quaternion.Angle(prior,spine.localRotation)>.1f)throw new InvalidOperationException("Sword pose changed.");weapon.CurrentWeapon=WeaponController.Weapon.Greatsword;
                    }
                    report.AppendLine(name+": 20 samples passed; largest per-joint torso bend "+beforeMax.ToString("F1")+" -> "+afterMax.ToString("F1")+" degrees; arm/leg/hip local poses preserved.");graph.Destroy();
                }
            }
            finally{if(graph.IsValid())graph.Destroy();if(clone)UnityEngine.Object.DestroyImmediate(clone);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
#endif
