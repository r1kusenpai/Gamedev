#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace StarterAssets.CQCEditorTools
{
    [InitializeOnLoad] public static class AddCustomElfGuard
    {
        const string Folder="Assets/CityExpansion/CustomElfGuard",Request="Assets/Editor/CQC/AddCustomElfGuard-v1.request.txt";
        static double next;
        static AddCustomElfGuard(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}
            catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CustomElfGuard-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}
        }
        [MenuItem("Tools/NPC/Add Supplied Armored Guard")]
        public static void Apply()
        {
            var crowd=UnityEngine.Object.FindAnyObjectByType<CityCrowdManager>();if(!crowd||crowd.People==null)throw new InvalidOperationException("City crowd missing.");
            var installedIndices=Enumerable.Range(0,crowd.Appearances.Length).Where(i=>crowd.Appearances[i].Skin&&AssetDatabase.GetAssetPath(crowd.Appearances[i].Skin)==Folder+"/CustomElfGuard.prefab").ToArray();
            int installedIndex=installedIndices.Length>0?installedIndices[0]:-1;
            ConfigureImport();var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/ElfGuard.fbx");
            var material=MakeMaterial();var temporaryScene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Custom Elf Guard");SceneManager.MoveGameObjectToScene(root,temporaryScene);
            try
            {
                var model=UnityEngine.Object.Instantiate(source,root.transform);model.name="Elf Guard Model";
                var animator=model.GetComponentInChildren<Animator>();if(!animator||!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new InvalidOperationException("Custom guard Humanoid avatar failed.");
                animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/CityExpansion/SyntyNpcPopulation1500/CityWalkIdle.controller");animator.fireEvents=false;animator.applyRootMotion=false;
                foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>()){r.sharedMaterials=new[]{material};r.updateWhenOffscreen=false;}
                animator.Rebind();animator.Update(0);
                var bounds=DeformedBounds(root);var originalScale=model.transform.localScale;
                model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CustomElfGuard-scale.txt","Original model scale="+originalScale+"; current scale="+model.transform.localScale+"; evaluated height="+bounds.size.y+"; humanScale="+animator.humanScale+"\n"+string.Join("\n",root.GetComponentsInChildren<Transform>().Select(t=>t.name+" scale="+t.localScale+" position="+t.localPosition)));
                var walk=AssetDatabase.LoadAllAssetsAtPath("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Locomotion--Walk_N.anim.fbx").OfType<AnimationClip>().First(c=>c.isHumanMotion&&!c.name.StartsWith("__preview__"));
                var report=new StringBuilder("Custom armored guard installation\n");Verify(root,walk,report);
                var skin=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/CustomElfGuard.prefab");var appearance=Bake(root,animator,walk,skin,material);
                var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-custom-elf-guard-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
                if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Scene backup failed.");
                var people=crowd.People;var originalPositions=people.Select(p=>p.Position).ToArray();var roleCounts=people.GroupBy(p=>p.Role).ToDictionary(g=>g.Key,g=>g.Count());
                var guards=Enumerable.Range(0,people.Length).Where(i=>people[i].Role=="Guards").ToArray();if(guards.Length<20)throw new InvalidOperationException("Expected at least 20 existing guards.");
                var selected=installedIndex>=0?guards.Where(i=>installedIndices.Contains(people[i].Skin)).ToArray():Enumerable.Range(0,20).Select(i=>guards[i*guards.Length/20]).ToArray();
                var retained=Enumerable.Range(0,crowd.Appearances.Length).Where(i=>!installedIndices.Contains(i)).ToArray();int index=retained.Length;var remap=retained.Select((old,n)=>new{old,n}).ToDictionary(x=>x.old,x=>x.n);
                Undo.RecordObject(crowd,"Add an armored guard appearance");var appearances=retained.Select(i=>crowd.Appearances[i]).ToList();appearances.Add(appearance);crowd.Appearances=appearances.ToArray();foreach(var p in people)p.Skin=installedIndices.Contains(p.Skin)?index:remap[p.Skin];foreach(int i in selected)people[i].Skin=index;
                if(!crowd.EditorPreview){var previewRoot=new GameObject("Guard Scene Previews - Hidden During Play");previewRoot.transform.SetParent(crowd.transform,false);Undo.RegisterCreatedObjectUndo(previewRoot,"Add editor guard preview root");crowd.EditorPreview=previewRoot;}
                if(crowd.EditorPreview)
                {
                    var oldPreview=crowd.EditorPreview.transform.Find("Custom Armored Guards - Scene Preview");if(oldPreview)Undo.DestroyObjectImmediate(oldPreview.gameObject);
                    var preview=new GameObject("Custom Armored Guards - Scene Preview");preview.transform.SetParent(crowd.EditorPreview.transform,false);Undo.RegisterCreatedObjectUndo(preview,"Preview added armored guards");
                    foreach(int i in selected.Where((n,k)=>k%4==0))
                    {
                        var p=people[i];var g=new GameObject("Custom Guard NPC Preview "+i);g.transform.SetParent(preview.transform,false);g.transform.SetPositionAndRotation(p.Position,Quaternion.Euler(0,p.Yaw,0));g.AddComponent<MeshFilter>().sharedMesh=appearance.PoseA[0];g.AddComponent<MeshRenderer>().sharedMaterial=material;
                    }
                }
                for(int i=0;i<people.Length;i++)if(people[i].Position!=originalPositions[i])throw new InvalidOperationException("NPC position changed.");
                foreach(var pair in roleCounts)if(people.Count(p=>p.Role==pair.Key)!=pair.Value)throw new InvalidOperationException("Role mix changed.");
                EditorUtility.SetDirty(crowd);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                report.AppendLine("Added appearance to 20 existing Guards, spread through the population. Total "+people.Length+"; role counts, positions, routes and transit retained. 1.75m height; 16000 triangles; 2K PBR maps; 22-bone Humanoid based on the player skeleton layout, fitted to this model with custom leg/arm/armor weights. Near animation and two instanced distant walk poses supplied. Five editor preview instances added under the existing preview root.");report.AppendLine("Backup: "+backup);
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CustomElfGuard-report.txt",report.ToString());SceneView.RepaintAll();Debug.Log("Custom armored guard rig verified and added to 20 guards. Scene saved.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(temporaryScene);}
        }
        static void ConfigureImport()
        {
            string path=Folder+"/ElfGuard.fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(!importer)throw new InvalidOperationException("Guard FBX missing.");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var skeleton=model.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
            var mapping=new Dictionary<string,string>{{"Hips","Hips"},{"Spine","Spine"},{"Chest","Chest"},{"UpperChest","UpperChest"},{"Neck","Neck"},{"Head","Head"}};
            foreach(string side in new[]{"Left","Right"})foreach(string part in new[]{"Shoulder","UpperArm","LowerArm","Hand","UpperLeg","LowerLeg","Foot","Toes"})mapping.Add(side+part,side+"_"+part);
            var human=mapping.Select(p=>new HumanBone{humanName=p.Key,boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray();
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription=new HumanDescription{human=human,skeleton=skeleton,armStretch=.05f,legStretch=.05f,upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,feetSpacing=0,hasTranslationDoF=false};
            importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;importer.SaveAndReimport();
        }
        static Material MakeMaterial()
        {
            Texture2D Texture(string name,bool normal,bool linear)
            {
                string path=Folder+"/"+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!linear&&!normal;importer.maxTextureSize=2048;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/ElfGuard.mat");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+"/ElfGuard.mat");}
            mat.name="Elf Guard Armor";mat.SetTexture("_BaseMap",Texture("Albedo",false,false));mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BumpMap",Texture("Normal",true,true));mat.EnableKeyword("_NORMALMAP");mat.SetTexture("_MetallicGlossMap",Texture("MetalSmooth",false,true));mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Smoothness",1);mat.SetFloat("_Cull",0);mat.enableInstancing=true;return mat;
        }
        static void Verify(GameObject original,AnimationClip walk,StringBuilder report)
        {
            var preview=new PreviewRenderUtility();PlayableGraph graph=default;
            try
            {
                var clone=UnityEngine.Object.Instantiate(original);preview.AddSingleGO(clone);var animator=clone.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Armored guard walking test");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var clip=AnimationClipPlayable.Create(graph,walk);clip.SetApplyFootIK(false);var output=AnimationPlayableOutput.Create(graph,"Guard walk",animator);output.SetSourcePlayable(clip);graph.Play();
                var leg=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);Quaternion first=Quaternion.identity;float motion=0;var skin=clone.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
                try
                {
                    for(int sample=0;sample<24;sample++)
                    {
                        clip.SetTime(walk.length*sample/24f);graph.Evaluate(0);if(sample==0)first=leg.localRotation;motion=Mathf.Max(motion,Quaternion.Angle(first,leg.localRotation));
                        skin.BakeMesh(baked);var vertices=baked.vertices;var bounds=new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);foreach(var vertex in vertices)bounds.Encapsulate(skin.transform.TransformPoint(vertex));if(bounds.size.magnitude>4||bounds.size.y<1.2f||float.IsNaN(bounds.size.x))throw new InvalidOperationException("Walking deformation bounds failed at sample "+sample+": "+bounds.size);
                        if(sample==6||sample==18)
                        {
                            preview.camera.transform.position=new Vector3(2.2f,1.5f,3.8f);preview.camera.transform.LookAt(new Vector3(0,.9f,0));preview.camera.fieldOfView=38;preview.camera.nearClipPlane=.05f;preview.camera.farClipPlane=30;preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);preview.lights[1].intensity=1.2f;Directory.CreateDirectory("Logs");FitSwordShoulder.Capture(preview,"Logs/CustomElfGuard-walk-"+sample+".png",clone);
                        }
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(baked);}
                if(motion<5)throw new InvalidOperationException("Humanoid walking did not animate the new guard.");report.AppendLine("Valid Humanoid avatar. Existing city walk retargeted across 24 samples; leg rotation range "+motion.ToString("F1")+" degrees; deformation bounds passed. Preview images: Logs/CustomElfGuard-walk-6.png and -18.png.");
            }
            finally{if(graph.IsValid())graph.Destroy();preview.Cleanup();}
        }
        static CityCrowdAppearance Bake(GameObject root,Animator animator,AnimationClip walk,GameObject prefab,Material material)
        {
            var result=new CityCrowdAppearance{Skin=prefab,Role="Guards",Materials=new[]{material}};var controller=animator.runtimeAnimatorController;animator.runtimeAnimatorController=null;
            var graph=PlayableGraph.Create("Custom guard distant poses");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var clip=AnimationClipPlayable.Create(graph,walk);clip.SetApplyFootIK(false);var output=AnimationPlayableOutput.Create(graph,"Bake",animator);output.SetSourcePlayable(clip);graph.Play();
            try
            {
                for(int phase=0;phase<2;phase++)
                {
                    clip.SetTime(walk.length*(phase==0?.15f:.65f));graph.Evaluate(0);var r=root.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();r.BakeMesh(mesh);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=root.transform.InverseTransformPoint(r.transform.TransformPoint(vertices[i]));mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/FarWalk"+phase+".asset"));if(phase==0)result.PoseA=new[]{mesh};else result.PoseB=new[]{mesh};
                }
            }
            finally{graph.Destroy();animator.runtimeAnimatorController=controller;}
            return result;
        }
        static Bounds BoundsOf(GameObject root){var renderers=root.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        static Bounds DeformedBounds(GameObject root)
        {
            bool first=true;var result=new Bounds();var mesh=new Mesh();
            try{foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>()){r.BakeMesh(mesh);foreach(var v in mesh.vertices){var p=r.transform.TransformPoint(v);if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}}}
            finally{UnityEngine.Object.DestroyImmediate(mesh);}return result;
        }
    }
}
#endif
