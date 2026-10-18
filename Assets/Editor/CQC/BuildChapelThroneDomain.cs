#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class BuildChapelThroneDomain
 {
  const string Request="Assets/Editor/CQC/BuildChapelThroneDomain-v1.request.txt",Folder="Assets/CityExpansion/ChapelThroneDomain";
  static double next;
  static BuildChapelThroneDomain(){EditorApplication.update+=Poll;}
  static void Poll()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
   if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||SceneManager.GetActiveScene().name!="MyForest")return;
   Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
   try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}
   catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ChapelThroneDomain-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}
  }
  [MenuItem("Tools/Domains/Create Chapel Throne Hall Entrance")]
  public static void Apply()
  {
   if(UnityEngine.Object.FindAnyObjectByType<ChapelThroneDomain>())return;
   var main=SceneManager.GetActiveScene();var mountain=GameObject.Find("Castle Mountain");var church=mountain?mountain.transform.Find("Large Upper Castle Church"):null;var player=GameObject.FindGameObjectWithTag("Player");
   var floor=mountain?mountain.transform.Find("Inner Castle Mountain/Flat Inner Castle Floor").GetComponent<MeshCollider>():null;
   if(!church||!player||!floor)throw new InvalidOperationException("Chapel, courtyard floor and player are required.");
   var original=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var matrices=original.Select(t=>t.localToWorldMatrix).ToArray();
   string backup="Assets/ForestExpansion/Backups/MyForest-before-chapel-throne-domain-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
   if(!EditorSceneManager.SaveScene(main,backup,true))throw new IOException("Could not back up MyForest.");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
   BuildRoom(main);
   SceneManager.SetActiveScene(main);Physics.SyncTransforms();
   var cb=BoundsOf(church.gameObject);var front=Vector3.ProjectOnPlane(-church.forward,Vector3.up).normalized;float ground=floor.bounds.max.y;
   var origin=new Vector3(cb.center.x,ground+1.4f,cb.center.z)+front*100;float nearest=200;Vector3 door=cb.center+front*(Mathf.Abs(front.x)*cb.extents.x+Mathf.Abs(front.z)*cb.extents.z);
   foreach(var collider in church.GetComponentsInChildren<Collider>())if(collider.Raycast(new Ray(origin,-front),out var hit,200)&&hit.distance<nearest){nearest=hit.distance;door=hit.point;}
   door.y=ground+.06f;door+=front*.45f;Vector3 back=door+front*3.2f;bool foundReturn=false;var playerController=player.GetComponent<CharacterController>();
   foreach(float side in new[]{0f,2f,-2f,3f,-3f})
   {
    var candidate=door+front*3.2f+Vector3.Cross(Vector3.up,front)*side;if(!floor.Raycast(new Ray(candidate+Vector3.up*5,Vector3.down),out var support,10))continue;candidate.y=support.point.y+.06f;
    if(IsClear(candidate,playerController)){back=candidate;foundReturn=true;break;}
   }
   if(!foundReturn)throw new InvalidOperationException("No clear supported return position beside the chapel main door.");
   var root=new GameObject("Chapel Throne Domain Entrance");Undo.RegisterCreatedObjectUndo(root,"Add chapel domain entrance");
   var portal=root.AddComponent<ChapelThroneDomain>();portal.Player=player.transform;portal.ChapelDoor=Anchor(root.transform,"Chapel Main Door Interaction",door,Quaternion.LookRotation(front));portal.OutsideReturn=Anchor(root.transform,"Safe Courtyard Return",back,Quaternion.LookRotation(front));portal.InteractionRange=5;
   Physics.SyncTransforms();CheckCapsule(back,player.GetComponent<CharacterController>(),"chapel return");
   for(int i=0;i<original.Length;i++)if(original[i]&&original[i].localToWorldMatrix!=matrices[i])throw new InvalidOperationException("An existing scene model moved.");
   var scenes=EditorBuildSettings.scenes.ToList();foreach(string path in new[]{main.path,ChapelThroneDomain.ScenePath}){int index=scenes.FindIndex(s=>s.path==path);if(index<0)scenes.Add(new EditorBuildSettingsScene(path,true));else scenes[index]=new EditorBuildSettingsScene(path,true);}EditorBuildSettings.scenes=scenes.ToArray();
   EditorSceneManager.MarkSceneDirty(main);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(main))throw new IOException("Could not save MyForest portal.");
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ChapelThroneDomain-setup.txt","Installed E interaction at the chapel main door, confirmation popup, additive throne-hall domain, fade transition and return popup. Same player retained. Extracted the exact throne hall shown in Modular Castle (camera 1), converted copies of materials to URP, added walkable/collision surfaces, lit candles and a royal throne. Domain isolated at (5000,0,5000); source package and existing city/castle transforms retained. Both scenes enabled in build settings.\nDoor: "+door+"\nReturn: "+back+"\nScene: "+ChapelThroneDomain.ScenePath+"\nBackup: "+backup);
   Selection.activeGameObject=root;Debug.Log("Chapel throne domain installed. Approach chapel main door and press E.");
  }
  static void BuildRoom(Scene main)
  {
   var source=EditorSceneManager.OpenScene("Assets/Modular Castle/DemoScene/castleDemo.unity",OpenSceneMode.Additive);var target=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(target);
   try
   {
    var root=new GameObject("Throne Hall Domain");root.transform.position=new Vector3(5000,0,5000);var marker=root.AddComponent<ThroneDomainRoom>();
    var demo=source.GetRootGameObjects().First(g=>g.name=="demo");var hall=UnityEngine.Object.Instantiate(demo);hall.name="Modular Castle Throne Hall";
    foreach(Transform child in hall.transform.Cast<Transform>().ToArray())
    {
     var renderers=child.GetComponentsInChildren<Renderer>();bool keep=renderers.Any(r=>r.bounds.center.x<-100&&r.bounds.center.z>15&&r.bounds.center.z<100);
     if(!keep)UnityEngine.Object.DestroyImmediate(child.gameObject);
    }
    hall.transform.SetParent(root.transform,false);hall.transform.localPosition=new Vector3(78.75f,0,-10.75f);hall.transform.localScale=Vector3.one*.5f;
    var converted=new Dictionary<Material,Material>();int meshColliders=0;
    foreach(var r in hall.GetComponentsInChildren<Renderer>())
    {
     r.sharedMaterials=r.sharedMaterials.Select(m=>Convert(m,converted)).ToArray();
     if(r.name.Equals("light",StringComparison.OrdinalIgnoreCase))r.enabled=false;
     string name=r.name.ToLowerInvariant();var filter=r.GetComponent<MeshFilter>();
     if(filter&&!r.GetComponent<Collider>()&&!name.Contains("fire")&&!name.Contains("light")&&!name.Contains("carpet")&&!name.Contains("candle")&&!name.Contains("pillow")){r.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;meshColliders++;}
    }
    // Retain only lights belonging to this room; source showcase cameras are excluded.
    foreach(var light in source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()))if(light.transform.position.x<-100)
    {
     var go=new GameObject("Hall Warm Light");go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(78.75f,0,-10.75f)+light.transform.position*.5f;var copy=go.AddComponent<Light>();copy.type=LightType.Point;copy.color=new Color(1,.65f,.32f);copy.intensity=2.5f;copy.range=12;copy.shadows=LightShadows.None;
    }
    var support=new GameObject("Hall Floor Support");support.transform.SetParent(root.transform,false);var box=support.AddComponent<BoxCollider>();box.center=new Vector3(0,-.27f,18.1f);box.size=new Vector3(25,.5f,36.2f);
    foreach(var side in new[]{-1,1}){Box(root.transform,"Side Boundary",new Vector3(side*12.8f,4,18),new Vector3(.3f,8,37));}
    Box(root.transform,"Front Boundary",new Vector3(0,4,36.5f),new Vector3(26,8,.3f));Box(root.transform,"Rear Boundary",new Vector3(0,4,-.5f),new Vector3(26,8,.3f));
    var stone=NewMaterial("Throne Stone",new Color(.18f,.20f,.22f),.05f);var red=NewMaterial("Throne Red Velvet",new Color(.38f,.025f,.035f),0);var gold=NewMaterial("Throne Aged Gold",new Color(.55f,.34f,.10f),.7f);var oak=NewMaterial("Domain Door Oak",new Color(.11f,.065f,.04f),.05f);
    Part(root.transform,"Throne Seat",new Vector3(0,1.55f,3.2f),new Vector3(1.7f,.35f,1.4f),stone);Part(root.transform,"Velvet Seat",new Vector3(0,1.76f,3.22f),new Vector3(1.35f,.16f,1.1f),red);
    Part(root.transform,"Throne Back",new Vector3(0,2.65f,2.65f),new Vector3(1.7f,2.6f,.35f),stone);Part(root.transform,"Velvet Back",new Vector3(0,2.75f,2.86f),new Vector3(1.26f,2,.1f),red);
    foreach(int side in new[]{-1,1}){Part(root.transform,"Throne Arm",new Vector3(side*.85f,2,3.2f),new Vector3(.28f,.85f,1.4f),stone);Part(root.transform,"Throne Gold Post",new Vector3(side*.77f,2.7f,2.64f),new Vector3(.12f,3,.48f),gold);}
    Part(root.transform,"Throne Crown",new Vector3(0,4,2.65f),new Vector3(1.9f,.16f,.48f),gold);
    Door(root.transform,new Vector3(0,0,36.05f),Quaternion.Euler(0,180,0),oak,gold);
    foreach(int side in new[]{-1,1})Door(root.transform,new Vector3(side*12.5f,0,30),Quaternion.Euler(0,side<0?90:-90,0),oak,gold);
    marker.Arrival=Anchor(root.transform,"Throne Hall Arrival",root.transform.position+new Vector3(0,.06f,32.3f),Quaternion.Euler(0,180,0));
    marker.ReturnDoor=Anchor(root.transform,"Return To Chapel Door",root.transform.position+new Vector3(0,.06f,35.5f),Quaternion.Euler(0,180,0));
    foreach(Vector3 position in new[]{new Vector3(0,7,15),new Vector3(0,7,29),new Vector3(0,6,5)}){var go=new GameObject("Throne Hall Soft Fill");go.transform.SetParent(root.transform,false);go.transform.localPosition=position;var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.82f,.62f);light.intensity=4;light.range=18;}
    foreach(var light in root.GetComponentsInChildren<Light>())light.shadows=LightShadows.None;
    RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.2f,.18f,.16f);RenderSettings.ambientEquatorColor=new Color(.1f,.095f,.09f);RenderSettings.ambientGroundColor=new Color(.05f,.045f,.04f);RenderSettings.ambientIntensity=1;
    Physics.SyncTransforms();CheckCapsule(marker.Arrival.position,GameObject.FindGameObjectWithTag("Player").GetComponent<CharacterController>(),"domain arrival");
    Capture(root);
    if(!EditorSceneManager.SaveScene(target,ChapelThroneDomain.ScenePath))throw new IOException("Throne scene save failed.");
   }
   finally{SceneManager.SetActiveScene(main);EditorSceneManager.CloseScene(target,true);EditorSceneManager.CloseScene(source,true);}
  }
  static Material Convert(Material original,Dictionary<Material,Material> converted)
  {
   if(!original)return null;if(converted.TryGetValue(original,out var cached))return cached;
   string path=Folder+"/"+original.name.Replace(" ","_")+"_URP.mat";bool fire=original.name.ToLowerInvariant().Contains("fire")||original.name.ToLowerInvariant().Contains("light");
   var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find(fire?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
   mat.SetTexture("_BaseMap",original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null);mat.SetColor("_BaseColor",original.HasProperty("_Color")?original.GetColor("_Color"):Color.white);mat.SetFloat("_Smoothness",.2f);
   if(!fire&&original.HasProperty("_BumpMap")&&original.GetTexture("_BumpMap")){mat.SetTexture("_BumpMap",original.GetTexture("_BumpMap"));mat.EnableKeyword("_NORMALMAP");}
   if(fire){mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",2);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.One);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;mat.SetFloat("_Cull",0);}
   mat.enableInstancing=true;converted[original]=mat;return mat;
  }
  static Material NewMaterial(string name,Color color,float metal){string path=Folder+"/"+name.Replace(" ","")+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",.2f);return mat;}
  static void Door(Transform parent,Vector3 position,Quaternion rotation,Material oak,Material gold){var root=new GameObject("Closed Hall Door");root.transform.SetParent(parent,false);root.transform.localPosition=position;root.transform.localRotation=rotation;Part(root.transform,"Oak Double Door",new Vector3(0,2,0),new Vector3(3.2f,4,.2f),oak);foreach(int side in new[]{-1,1})Part(root.transform,"Door Frame",new Vector3(side*1.7f,2,0),new Vector3(.16f,4.3f,.4f),gold);Part(root.transform,"Door Lintel",new Vector3(0,4.15f,0),new Vector3(3.55f,.18f,.4f),gold);foreach(int side in new[]{-1,1})Part(root.transform,"Door Handle",new Vector3(side*.18f,1.8f,-.15f),new Vector3(.08f,.32f,.12f),gold);}
  static GameObject Part(Transform parent,string name,Vector3 position,Vector3 scale,Material mat){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;return go;}
  static void Box(Transform parent,string name,Vector3 position,Vector3 size){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.AddComponent<BoxCollider>().size=size;}
  static Transform Anchor(Transform parent,string name,Vector3 position,Quaternion rotation){var go=new GameObject(name);go.transform.SetParent(parent,true);go.transform.SetPositionAndRotation(position,rotation);return go.transform;}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static void CheckCapsule(Vector3 point,CharacterController controller,string name)
  {
   float radius=controller.radius*.95f;foreach(var hit in Physics.OverlapCapsule(point+Vector3.up*(radius+.12f),point+Vector3.up*(controller.height-radius),radius,~0,QueryTriggerInteraction.Ignore))if(!hit.transform.IsChildOf(controller.transform))throw new InvalidOperationException(name+" blocked by "+hit.name);
  }
  static bool IsClear(Vector3 point,CharacterController controller){float radius=controller.radius*.95f;return Physics.OverlapCapsule(point+Vector3.up*(radius+.12f),point+Vector3.up*(controller.height-radius),radius,~0,QueryTriggerInteraction.Ignore).All(hit=>hit.transform.IsChildOf(controller.transform));}
  static void Capture(GameObject root)
  {
   var preview=new PreviewRenderUtility();try{var clone=UnityEngine.Object.Instantiate(root);clone.transform.position=Vector3.zero;preview.AddSingleGO(clone);preview.camera.transform.position=new Vector3(0,23,42);preview.camera.transform.LookAt(new Vector3(0,1,15));preview.camera.fieldOfView=50;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;preview.lights[0].intensity=1;preview.lights[0].transform.rotation=Quaternion.Euler(60,30,0);preview.lights[1].intensity=.8f;Directory.CreateDirectory("Logs");FitSwordShoulder.Capture(preview,"Logs/ChapelThroneDomain-preview.png",clone);}finally{preview.Cleanup();}
  }
 }
}
#endif
