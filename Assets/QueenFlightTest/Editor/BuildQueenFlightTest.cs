#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace QueenFlightTest.Editor
{
    [InitializeOnLoad]
    public static class BuildQueenFlightTest
    {
        public const string Folder="Assets/QueenFlightTest";
        public const string ScenePath=Folder+"/QueenFlightTest.unity";
        const string Request=Folder+"/Build.request.txt";
        static double next;
        static BuildQueenFlightTest(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            try{Build();AssetDatabase.DeleteAsset(Request);}
            catch(Exception e){File.WriteAllText("Logs/QueenFlightTest-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}
        }
        [MenuItem("Tools/Queen Flight Test/Build Clean Test Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Effects");
            const string modelPath=Folder+"/Models/QueenFlight.fbx";
            AssetDatabase.ImportAsset(modelPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;
            importer.importCameras=false;importer.importLights=false;importer.optimizeGameObjects=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.isReadable=true;importer.preserveHierarchy=true;
            var clipSettings=importer.defaultClipAnimations;
            if(clipSettings.Length!=7)throw new InvalidOperationException("Expected seven animation takes, found "+clipSettings.Length);
            foreach(var clip in clipSettings)
            {
                string name=clip.name.Split('|').Last();clip.name=name;
                clip.loopTime=name.StartsWith("01_")||name.StartsWith("02_");clip.loopPose=false;
                clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;
                clip.keepOriginalOrientation=true;clip.keepOriginalPositionY=true;clip.keepOriginalPositionXZ=true;
            }
            importer.clipAnimations=clipSettings;importer.SaveAndReimport();
            var clips=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).OrderBy(c=>c.name).ToArray();
            if(clips.Length!=7||clips.Any(c=>c.length<=0))throw new InvalidOperationException("Invalid imported clips.");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);if(!source)throw new InvalidOperationException("Queen model import failed.");
            var glow=Material("Spell Glow",new Color(1,.4f,.05f),true);
            var effects=new GameObject[3];
            for(int i=0;i<3;i++)
            {
                var go=new GameObject(((QueenSpellEffect.Spell)i).ToString());var effect=go.AddComponent<QueenSpellEffect>();effect.Kind=(QueenSpellEffect.Spell)i;effect.GlowMaterial=glow;effect.Lifetime=i==0?2.5f:i==1?.7f:1.1f;
                effects[i]=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Effects/"+go.name+".prefab");UnityEngine.Object.DestroyImmediate(go);
            }
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var testObject=new GameObject("Queen Flight Test");var test=testObject.AddComponent<QueenAnimationTest>();test.Clips=clips;test.EffectPrefabs=effects;
                var queen=UnityEngine.Object.Instantiate(source);queen.name="Queen - Latest Pose And Attacks";
                test.Queen=queen.GetComponentInChildren<Animator>();if(!test.Queen)test.Queen=queen.AddComponent<Animator>();
                test.Queen.runtimeAnimatorController=null;test.Queen.applyRootMotion=false;
                foreach(var r in queen.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    string file=r.name.Contains("QueenBody")?"QueenPBR":r.name.Contains("RoyalFur")?"RoyalFur":r.name.Contains("RoyalLining")?"RoyalLining":"CapePBR";
                    var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/CityExpansion/ThroneQueen/"+file+".mat");
                    if(!material)throw new InvalidOperationException("Missing queen material "+file);
                    r.sharedMaterials=Enumerable.Repeat(material,r.sharedMesh.subMeshCount).ToArray();r.updateWhenOffscreen=true;
                }
                var target=GameObject.CreatePrimitive(PrimitiveType.Capsule);target.name="Movable Spell Target - WASD";target.transform.position=new Vector3(0,0,3.5f);
                // Keep the target origin at floor height, matching a real character.
                target.GetComponent<MeshFilter>().sharedMesh=null;UnityEngine.Object.DestroyImmediate(target.GetComponent<Collider>());
                var visible=GameObject.CreatePrimitive(PrimitiveType.Capsule);visible.transform.SetParent(target.transform,false);visible.transform.localPosition=Vector3.up*.9f;visible.transform.localScale=new Vector3(.55f,.9f,.55f);visible.GetComponent<Renderer>().sharedMaterial=Material("Target",new Color(.1f,.6f,.8f));test.Target=target.transform;
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Test arena floor";ground.transform.position=new Vector3(0,-.15f,1.5f);ground.transform.localScale=new Vector3(12,.3f,12);ground.GetComponent<Renderer>().sharedMaterial=Material("Arena",new Color(.065f,.08f,.12f));
                var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(5,3.5f,7.5f);camera.transform.LookAt(new Vector3(0,1,1.25f));camera.orthographic=true;camera.orthographicSize=3f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.045f);camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.gameObject.AddComponent<AudioListener>();
                camera.allowHDR=true;var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
                var volume=new GameObject("Spell bloom").AddComponent<Volume>();volume.isGlobal=true;
                string profilePath=Folder+"/Materials/SpellBloom.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
                if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}
                bloom.intensity.Override(1.3f);bloom.threshold.Override(1);bloom.scatter.Override(.6f);volume.sharedProfile=profile;
                var light=new GameObject("Arena key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(40,-35,0);
                RenderSettings.ambientLight=new Color(.35f,.4f,.5f);
                var report=ValidateClips(test);
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Could not save clean test scene.");
                AssetDatabase.SaveAssets();File.WriteAllText("Logs/QueenFlightTest-setup.txt",report+"\nScene: "+ScenePath+"\nSeven latest clips imported. Three standalone Unity effect prefabs. Existing throne and world scenes untouched.\n");
                Debug.Log("Clean queen test ready: Tools > Queen Flight Test > Open Clean Test Scene. Press Play for all seven clips and spells.");
            }
            finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        static Material Material(string name,Color color,bool glow=false)
        {
            string path=Folder+"/Materials/"+name.Replace(" ","")+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find(glow?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;if(glow)m.color=color*3;return m;
        }
        static string ValidateClips(QueenAnimationTest test)
        {
            var report=new StringBuilder();var graph=PlayableGraph.Create("Queen import verification");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output=AnimationPlayableOutput.Create(graph,"Verify",test.Queen);graph.Play();
            try
            {
                foreach(var clip in test.Clips)
                {
                    var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);output.SetSourcePlayable(p);
                    var hand=test.Queen.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Right_Hand");Vector3 first=default;float movement=0;
                    for(int i=0;i<9;i++)
                    {
                        p.SetTime(clip.length*i/8f);graph.Evaluate(0);if(i==0&&hand)first=hand.position;if(hand)movement=Mathf.Max(movement,Vector3.Distance(first,hand.position));
                        foreach(var r in test.Queen.GetComponentsInChildren<SkinnedMeshRenderer>())if(float.IsNaN(r.bounds.center.y)||r.bounds.size.magnitude>25)throw new InvalidOperationException("Invalid deformation: "+clip.name);
                    }
                    if(movement<.00001f)throw new InvalidOperationException("Imported clip does not animate the hand: "+clip.name);
                    report.AppendLine(clip.name+" duration="+clip.length.ToString("F3")+"s; hand movement="+movement.ToString("F3")+"m; "+(clip.isLooping?"loop":"one-shot"));graph.DestroyPlayable(p);
                }
                var idle=AnimationClipPlayable.Create(graph,test.Clips[0]);output.SetSourcePlayable(idle);idle.SetTime(0);graph.Evaluate(0);
            }
            finally{graph.Destroy();}
            return report.ToString();
        }
        [MenuItem("Tools/Queen Flight Test/Open Clean Test Scene")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
    }
}
#endif
