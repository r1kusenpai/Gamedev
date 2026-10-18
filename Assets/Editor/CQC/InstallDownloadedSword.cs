#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace StarterAssets.CQCEditorTools
{
    public static class InstallDownloadedSword
    {
        const string Source = "Assets/ImportedSwordAnimations/Source/";
        const string Folder = "Assets/Resources/WeaponAnimations/DownloadedSword";
        const string ReportPath = "Logs/DownloadedSword-report.txt";
        static readonly string[] Names = {"sword_regular_b","sword_regular_b_reversed","sword_regular_b_mirrored","sword_regular_b_reversed_mirrored","sword_regular_combo","sword_regular_combo_mirrored"};
        [MenuItem("Tools/Weapons/Install Original Downloaded Sword")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before importing.");
            var report = new StringBuilder("Downloaded right-handed sword integration\n");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Directory.CreateDirectory(Folder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var name in Names) Configure(name, report);
                var old = Resources.Load<SwordAnimationSet>("WeaponAnimations/DoubleLSword/RecommendedSword");
                if (!old || !old.IsReady) throw new InvalidOperationException("Existing locomotion/air/held sword clips are missing.");
                var set = ScriptableObject.CreateInstance<SwordAnimationSet>(); EditorUtility.CopySerialized(old,set);set.name="DownloadedSword";
                set.Combo1 = Original("sword_regular_b");
                set.Combo2 = Original("sword_regular_b_reversed");
                set.Combo3 = Original("sword_regular_combo"); set.Combo4 = null;
                set.FullComboFinisher=true;set.MovingTorsoWeight=.65f;
                set.RunningAttack = set.Combo1;
                set.ChargedAttack = set.Combo1; set.AirAttack = set.Combo1;
                set.AttackSpeed=1f;set.ResponsiveInput=true;set.UseAuthoredRootTravel=true;
                set.OriginalDownloadedPlayback=false;set.ResponsiveDownloadedCombos=true;
                set.AttackBlendSeconds=.045f;set.InputBufferSeconds=.45f;
                set.ComboLink=new Vector4(.82f,.82f,.62f,.82f);
                set.HitStart=new Vector4(.28f,.28f,.28f,.28f);set.HitEnd=new Vector4(.7f,.7f,.7f,.7f);
                VerifyRetarget(set,report);
                string path=Folder+"/DownloadedSword.asset";var saved=AssetDatabase.LoadAssetAtPath<SwordAnimationSet>(path);
                if(saved){EditorUtility.CopySerialized(set,saved);UnityEngine.Object.DestroyImmediate(set);EditorUtility.SetDirty(saved);}
                else AssetDatabase.CreateAsset(set,path);
                AssetDatabase.SaveAssets();
                report.AppendLine("Ready: basic A1 -> A2 -> full combo; sprint one press A1 -> A2, next press full combo; 1x speed, buffered links, no held substitution. Full combo has four hit windows.");
                report.AppendLine("Standing uses full-body attacks and authored root travel. Walking/running retains normal locomotion legs and steering, 65% attack torso, and full attack arms.");
                report.AppendLine("Existing locomotion retained outside attacks. Mirrored sources retained as left-handed alternatives. Source animation keys unchanged; neutral bind reference fixes humanoid posture. No scene changes.");
                Directory.CreateDirectory("Logs");File.WriteAllText(ReportPath,report.ToString());
                Debug.Log("DOWNLOADED_SWORD_INSTALL_OK\n"+report);
            }
            catch(Exception e)
            {
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DownloadedSword-error.txt",report+"\n"+e);Debug.LogException(e);throw;
            }
        }
        static void Configure(string name,StringBuilder report)
        {
            string path=Source+name+".fbx";
            var im=AssetImporter.GetAtPath(path) as ModelImporter;if(!im)throw new InvalidOperationException("Missing converted FBX: "+path);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var pairs=new Dictionary<HumanBodyBones,string>{{HumanBodyBones.Hips,"pelvis"},{HumanBodyBones.Spine,"spine_01"},{HumanBodyBones.Chest,"spine_02"},{HumanBodyBones.UpperChest,"spine_03"},{HumanBodyBones.Neck,"neck_01"},{HumanBodyBones.Head,"Head"}};
            foreach(var side in new[]{"Left","Right"})
            {
                string suffix=side=="Left"?"_l":"_r";
                var parts=new Dictionary<string,string>{{"Shoulder","clavicle"},{"UpperArm","upperarm"},{"LowerArm","lowerarm"},{"Hand","hand"},{"UpperLeg","thigh"},{"LowerLeg","calf"},{"Foot","foot"},{"Toes","ball"}};
                foreach(var part in parts)pairs.Add((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+part.Key),part.Value+suffix);
                foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Little"})
                    for(int j=0;j<3;j++)pairs.Add((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+finger+new[]{"Proximal","Intermediate","Distal"}[j]),(finger=="Little"?"pinky":finger.ToLowerInvariant())+"_0"+(j+1)+suffix);
            }
            im.animationType=ModelImporterAnimationType.Human;im.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            im.humanDescription=new HumanDescription
            {
                human=pairs.Select(p=>new HumanBone{humanName=HumanTrait.BoneName[(int)p.Key],boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),
                skeleton=BindSkeleton(model.name),
                armStretch=.05f,legStretch=.05f,upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,hasTranslationDoF=false
            };
            im.importAnimation=true;im.importCameras=false;im.importLights=false;im.optimizeGameObjects=false;
            im.animationCompression=ModelImporterAnimationCompression.Off;
            im.materialImportMode=ModelImporterMaterialImportMode.None;
            ModelImporterClipAnimation Clip(string clip,float first,float last)=>new ModelImporterClipAnimation
            {
                name=clip,takeName=im.defaultClipAnimations.First().takeName,firstFrame=first,lastFrame=last,
                loopTime=false,loopPose=false,lockRootRotation=true,keepOriginalOrientation=true,
                lockRootHeightY=true,keepOriginalPositionY=false,heightFromFeet=true,
                lockRootPositionXZ=false,keepOriginalPositionXZ=false
            };
            bool combo=name.Contains("combo");
            var clips=new List<ModelImporterClipAnimation>{Clip(name+"_Full",0,combo?180:32)};
            if(name=="sword_regular_combo")
            {
                clips.Add(Clip("Downloaded_Combo1",0,28));clips.Add(Clip("Downloaded_Combo2",28,59));
                clips.Add(Clip("Downloaded_Combo3",59,84));clips.Add(Clip("Downloaded_Combo4",84,126));
            }
            if(name=="sword_regular_b")clips.Add(Clip("Downloaded_RunningSlash",0,32));
            im.clipAnimations=clips.ToArray();im.userData="DownloadedSword-v1";im.SaveAndReimport();
            model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var avatar=model.GetComponent<Animator>()?.avatar;
            if(!avatar || !avatar.isValid || !avatar.isHuman)throw new InvalidOperationException("Humanoid avatar invalid: "+name);
            report.AppendLine(name+": valid Humanoid, "+clips.Count+" clips, explicit UE skeleton mapping.");
        }
        static SkeletonBone[] BindSkeleton(string rootName)
        {
            var reference=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"Sword_BindReference.fbx");
            if(!reference)throw new InvalidOperationException("The static neutral bind-reference FBX is required. Do not build an avatar from an animated first frame.");
            var bones=reference.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
            bones[0].name=rootName;return bones;
        }
        static AnimationClip Original(string name) => AssetDatabase.LoadAllAssetsAtPath(Source+name+".fbx").OfType<AnimationClip>().First(c=>c.name==name+"_Full");
        static AnimationClip Copy(string source,string name)
        {
            var imported=AssetDatabase.LoadAllAssetsAtPath(Source+source+".fbx").OfType<AnimationClip>().FirstOrDefault(c=>c.name==name);
            if(!imported||!imported.isHumanMotion)throw new InvalidOperationException("Missing Humanoid clip "+name);
            var clip=UnityEngine.Object.Instantiate(imported);clip.name=name;AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
            string path=Folder+"/"+name+".anim";var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(saved){EditorUtility.CopySerialized(clip,saved);UnityEngine.Object.DestroyImmediate(clip);EditorUtility.SetDirty(saved);}else{saved=clip;AssetDatabase.CreateAsset(saved,path);}return saved;
        }
        static void VerifyRetarget(SwordAnimationSet set,StringBuilder report)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab");
            var clone=UnityEngine.Object.Instantiate(prefab);PlayableGraph graph=default;
            try
            {
                clone.hideFlags=HideFlags.HideAndDontSave;clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                foreach(var b in clone.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                foreach(var c in clone.GetComponentsInChildren<Collider>())c.enabled=false;
                var animator=clone.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                foreach(var clip in new[]{set.Combo1,set.Combo2,set.Combo3,set.Combo4,set.RunningAttack}.Where(c=>c))
                {
                    graph=PlayableGraph.Create("Downloaded sword retarget check");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);var output=AnimationPlayableOutput.Create(graph,"Character",animator);output.SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
                    var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);Vector3 lastHand=hand.position,lastFoot=foot.position;float handTravel=0,footTravel=0;Vector3 rootTravel=Vector3.zero;
                    for(int i=0;i<Mathf.CeilToInt(clip.length*60);i++)
                    {
                        graph.Evaluate(1f/60);var h=hand.position;
                        if(!float.IsFinite(h.x)||!float.IsFinite(h.y)||!float.IsFinite(h.z))throw new InvalidOperationException("Non-finite retarget "+clip.name);
                        handTravel+=Vector3.Distance(lastHand,h);footTravel+=Vector3.Distance(lastFoot,foot.position);lastHand=h;lastFoot=foot.position;rootTravel+=animator.deltaPosition;
                        if(animator.deltaPosition.magnitude>.5f)throw new InvalidOperationException("Discontinuous root motion: "+clip.name);
                    }
                    if(handTravel<.1f||footTravel<.01f)throw new InvalidOperationException("Retargeted hand or foot is static: "+clip.name);
                    report.AppendLine(clip.name+": "+clip.length.ToString("F3")+"s; right-hand travel "+handTravel.ToString("F3")+"m; left-foot travel "+footTravel.ToString("F3")+"m; authored root "+rootTravel.ToString("F3"));
                    graph.Destroy();clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);animator.Rebind();
                }
            }
            finally{if(graph.IsValid())graph.Destroy();UnityEngine.Object.DestroyImmediate(clone);}
        }
    }
}
#endif

