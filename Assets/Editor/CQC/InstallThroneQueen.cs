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
 [InitializeOnLoad] public static class InstallThroneQueen
 {
  const string Folder="Assets/CityExpansion/ThroneQueen",Request="Assets/Editor/CQC/InstallThroneQueen-v2.request.txt";
  static double next;
  static InstallThroneQueen(){EditorApplication.update+=Poll;}
  static void Poll()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   try{Apply();AssetDatabase.DeleteAsset(Request);}
   catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ThroneQueen-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}
  }
  [MenuItem("Tools/Domains/Add Queen Beside Throne")]
  public static void Apply()
  {
   var current=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(ChapelThroneDomain.ScenePath);bool opened=!scene.isLoaded;
   if(opened)scene=EditorSceneManager.OpenScene(ChapelThroneDomain.ScenePath,OpenSceneMode.Additive);
   GameObject root=null;Scene temporary=default;
   try
   {
    var domain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ThroneDomainRoom>()).FirstOrDefault();if(!domain)throw new InvalidOperationException("Throne domain is missing.");
    var seat=domain.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Throne Seat");if(!seat)throw new InvalidOperationException("Cannot locate the existing throne seat.");
    ConfigureImport();var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/ThroneQueen.fbx");
    temporary=EditorSceneManager.NewPreviewScene();root=new GameObject("Royal Queen With Fitted Cape");SceneManager.MoveGameObjectToScene(root,temporary);
    var model=UnityEngine.Object.Instantiate(source,root.transform);model.name="Animated Queen And Cape";
    var animator=model.GetComponentInChildren<Animator>();if(!animator||!animator.avatar||!animator.avatar.isHuman||!animator.avatar.isValid)throw new InvalidOperationException("Queen Humanoid avatar is invalid.");
    animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/CityExpansion/SyntyNpcPopulation1500/CityWalkIdle.controller");animator.applyRootMotion=false;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    var queen=Pbr("Queen");var cape=Solid("Cape PBR",new Color(.075f,.025f,.19f),.04f);cape.SetTexture("_BaseMap",null);cape.SetTexture("_BumpMap",null);cape.SetTexture("_MetallicGlossMap",null);cape.DisableKeyword("_NORMALMAP");cape.DisableKeyword("_METALLICSPECGLOSSMAP");cape.SetFloat("_Smoothness",.26f);cape.SetFloat("_Cull",0);var fur=Solid("Royal Fur",new Color(.77f,.72f,.61f),0);var gold=Solid("Royal Gold",new Color(.65f,.4f,.095f),.7f);var lining=Solid("Royal Lining",new Color(.26f,.015f,.045f),0);
    foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
    {
     var material=r.name.Contains("QueenBody")?queen:r.name.Contains("RoyalFur")?fur:r.name.Contains("RoyalGold")?gold:r.name.Contains("RoyalLining")?lining:cape;
     r.sharedMaterials=Enumerable.Repeat(material,r.sharedMesh.subMeshCount).ToArray();r.updateWhenOffscreen=false;
    }
    var idle=AssetDatabase.LoadAllAssetsAtPath("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx").OfType<AnimationClip>().First(c=>c.isHumanMotion&&!c.name.StartsWith("__preview__"));
    var report=new StringBuilder("Queen with custom fitted cape\n");var pose=BakeAndVerify(root,idle,report);
    var editorPose=new GameObject("Standing Queen - Editor Preview");editorPose.transform.SetParent(root.transform,false);
    for(int i=0;i<pose.Count;i++){var part=new GameObject(pose[i].Name);part.transform.SetParent(editorPose.transform,false);part.AddComponent<MeshFilter>().sharedMesh=pose[i].Mesh;part.AddComponent<MeshRenderer>().sharedMaterials=pose[i].Materials;}
    var visual=root.AddComponent<ThroneQueenVisual>();visual.AnimatedModel=model;visual.EditorPose=editorPose;visual.RefreshVisuals();
    var collision=root.AddComponent<CapsuleCollider>();collision.center=new Vector3(0,.89f,0);collision.height=1.78f;collision.radius=.26f;
    var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/RoyalQueen.prefab");
    string backup="Assets/ForestExpansion/Backups/ThroneDomain-before-queen-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
    if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up the edited throne room.");
    var existing=domain.transform.Find("Queen Beside Throne");if(existing)Undo.DestroyObjectImmediate(existing.gameObject);
    var actor=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);actor.name="Queen Beside Throne";actor.transform.SetParent(domain.transform,true);Undo.RegisterCreatedObjectUndo(actor,"Add queen beside throne");
    var position=seat.position+seat.right*2.25f+seat.forward*.2f;
    Physics.SyncTransforms();if(!Physics.Raycast(position+Vector3.up*4,Vector3.down,out var hit,8,~0,QueryTriggerInteraction.Ignore))throw new InvalidOperationException("No supported position beside throne.");
    position.y=hit.point.y+.05f;actor.transform.SetPositionAndRotation(position,Quaternion.LookRotation(Vector3.ProjectOnPlane(seat.forward,Vector3.up)));
    Physics.SyncTransforms();foreach(var obstacle in Physics.OverlapCapsule(position+Vector3.up*.38f,position+Vector3.up*1.5f,.28f,~0,QueryTriggerInteraction.Ignore))if(!obstacle.transform.IsChildOf(actor.transform))throw new InvalidOperationException("Queen placement blocked by "+obstacle.name);
    PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new IOException("Throne room save failed.");
    report.AppendLine("Standing beside the existing throne, as requested. Same domain room, entrance and return retained. Crown/body source retained; separately generated cape replaced with a fitted open-front mantle, narrower shoulders, shaped back drape, shortened train, fur collar and hem, gold edging and clasp, burgundy lining. Shared 22-bone Humanoid rig, looping idle, stable cloth weighting (no cloth simulation). Editor shows a baked relaxed stance; Play uses the animated character. The city population and role mix were not changed.");report.AppendLine("Position: "+position+"\nPrefab: "+Folder+"/RoyalQueen.prefab\nBackup: "+backup);File.WriteAllText("Logs/ThroneQueen-report.txt",report.ToString());Debug.Log("Queen and fitted royal cape added beside the throne. Domain saved.");
   }
   finally{if(root)UnityEngine.Object.DestroyImmediate(root);if(temporary.IsValid())EditorSceneManager.ClosePreviewScene(temporary);if(current.IsValid()&&current.isLoaded)SceneManager.SetActiveScene(current);if(opened)EditorSceneManager.CloseScene(scene,true);}
  }
  struct PosePart{public string Name;public Mesh Mesh;public Material[] Materials;}
  static List<PosePart> BakeAndVerify(GameObject original,AnimationClip idle,StringBuilder report)
  {
   var preview=new PreviewRenderUtility();PlayableGraph graph=default;var result=new List<PosePart>();
   try
   {
    var clone=UnityEngine.Object.Instantiate(original);preview.AddSingleGO(clone);var animator=clone.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=null;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
    graph=PlayableGraph.Create("Queen idle fitting verification");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var playable=AnimationClipPlayable.Create(graph,idle);playable.SetApplyFootIK(false);var output=AnimationPlayableOutput.Create(graph,"Idle",animator);output.SetSourcePlayable(playable);graph.Play();
    var skin=clone.GetComponentsInChildren<SkinnedMeshRenderer>();var scratch=new Mesh();float minHem=float.PositiveInfinity;float maxHeight=0;
    try
    {
     for(int sample=0;sample<24;sample++)
     {
      playable.SetTime(idle.length*(sample+.1f)/24);graph.Evaluate(0);var bounds=new Bounds();bool first=true;
      foreach(var r in skin){r.BakeMesh(scratch);foreach(var v in scratch.vertices){var p=r.transform.TransformPoint(v);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);if(r.name=="FittedRoyalCape")minHem=Mathf.Min(minHem,p.y);}}
      maxHeight=Mathf.Max(maxHeight,bounds.size.y);if(bounds.size.y<1.3f||bounds.size.y>2.6f||bounds.size.x>2.8f||float.IsNaN(bounds.size.x))throw new InvalidOperationException("Queen idle deformation bounds failed: "+bounds.size);
      var chest=animator.GetBoneTransform(HumanBodyBones.Chest);foreach(var hand in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand})if(animator.GetBoneTransform(hand).position.y>chest.position.y+.15f)throw new InvalidOperationException("Queen arms did not settle into a relaxed idle.");
     }
     if(minHem<-.045f)throw new InvalidOperationException("Cape hem extends below the floor: "+minHem);
     playable.SetTime(idle.length*.25f);graph.Evaluate(0);
     foreach(var r in skin)
     {
      var mesh=new Mesh{name=r.name+" Idle Preview"};r.BakeMesh(mesh);var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i]=r.transform.TransformPoint(v[i]);mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+r.name.Replace(" ","_")+"_Idle.asset"));result.Add(new PosePart{Name=r.name,Mesh=mesh,Materials=r.sharedMaterials});
     }
     foreach(bool back in new[]{false,true})
     {
      preview.camera.transform.position=back?new Vector3(-2.4f,1.6f,-4.5f):new Vector3(2.4f,1.6f,4.5f);preview.camera.transform.LookAt(new Vector3(0,.95f,0));preview.camera.fieldOfView=31;preview.camera.nearClipPlane=.05f;preview.camera.farClipPlane=30;preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(35,back?30:210,0);preview.lights[1].intensity=1.2f;Directory.CreateDirectory("Logs");FitSwordShoulder.Capture(preview,"Logs/ThroneQueen-"+(back?"back":"front")+".png",clone);
     }
    }
    finally{UnityEngine.Object.DestroyImmediate(scratch);}
    report.AppendLine("Valid Humanoid. Existing standing idle tested across 24 samples; relaxed arms and deformation bounds passed. Maximum animated height="+maxHeight.ToString("F3")+"m; cape lowest hem="+minHem.ToString("F3")+"m relative to origin. Front/back wardrobe previews generated.");return result;
   }
   finally{if(graph.IsValid())graph.Destroy();preview.Cleanup();}
  }
  static void ConfigureImport()
  {
   string path=Folder+"/ThroneQueen.fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=AssetImporter.GetAtPath(path) as ModelImporter;var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!importer||!model)throw new InvalidOperationException("Queen model missing.");
   var mapping=new Dictionary<string,string>{{"Hips","Hips"},{"Spine","Spine"},{"Chest","Chest"},{"UpperChest","UpperChest"},{"Neck","Neck"},{"Head","Head"}};foreach(string side in new[]{"Left","Right"})foreach(string part in new[]{"Shoulder","UpperArm","LowerArm","Hand","UpperLeg","LowerLeg","Foot","Toes"})mapping.Add(side+part,side+"_"+part);
   importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.humanDescription=new HumanDescription{human=mapping.Select(p=>new HumanBone{humanName=p.Key,boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),skeleton=model.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),armStretch=.05f,legStretch=.05f,upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f};importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;importer.isReadable=true;importer.SaveAndReimport();
  }
  static Material Pbr(string tag)
  {
   var material=Solid(tag+" PBR",Color.white,0);material.SetTexture("_BaseMap",Texture(tag+"Albedo",false,false));material.SetTexture("_BumpMap",Texture(tag+"Normal",true,true));material.EnableKeyword("_NORMALMAP");material.SetTexture("_MetallicGlossMap",Texture(tag+"MetalSmooth",false,true));material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Smoothness",1);material.SetFloat("_Cull",0);return material;
  }
  static Texture2D Texture(string name,bool normal,bool linear){string path=Folder+"/"+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal&&!linear;importer.maxTextureSize=2048;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
  static Material Solid(string name,Color color,float metal){string path=Folder+"/"+name.Replace(" ","")+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.name=name;mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",.2f);return mat;}
 }
}
#endif
