#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class SurveyChapelDomain
 {
  const string Request="Assets/Editor/CQC/SurveyChapelDomain.request.txt";static double next;
  static SurveyChapelDomain(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){File.WriteAllText("Logs/ChapelDomain-survey-error.txt",e.ToString());AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [Serializable] class Item{public string name;public Vector3 position,size,front;}
  [Serializable] class Report{public Item chapel;public List<Item> pieces=new List<Item>();public List<Item> cameras=new List<Item>();}
  public static void Apply()
  {
   var main=SceneManager.GetActiveScene();var church=GameObject.Find("Castle Mountain").transform.Find("Large Upper Castle Church");var cb=BoundsOf(church.gameObject);var report=new Report{chapel=new Item{name=church.name,position=cb.center,size=cb.size,front=-church.forward}};
   var scene=EditorSceneManager.OpenScene("Assets/Modular Castle/DemoScene/castleDemo.unity",OpenSceneMode.Additive);var preview=new PreviewRenderUtility();var materials=new Dictionary<Material,Material>();
   try
   {
    foreach(var root in scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<Renderer>())report.pieces.Add(new Item{name=r.name,position=r.bounds.center,size=r.bounds.size});
    var demo=scene.GetRootGameObjects().First(g=>g.name=="demo");var clone=UnityEngine.Object.Instantiate(demo);preview.AddSingleGO(clone);
    foreach(var r in clone.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>{if(!m)return m;if(!materials.TryGetValue(m,out var n)){n=new Material(Shader.Find("Universal Render Pipeline/Lit"));if(m.HasProperty("_MainTex"))n.SetTexture("_BaseMap",m.GetTexture("_MainTex"));n.SetColor("_BaseColor",m.HasProperty("_Color")?m.GetColor("_Color"):Color.white);n.SetFloat("_Smoothness",.15f);materials[m]=n;}return n;}).ToArray();
    preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(55,30,0);preview.lights[1].intensity=1;
    foreach(var camera in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()))
    {
     report.cameras.Add(new Item{name=camera.name,position=camera.transform.position,front=camera.transform.forward});preview.camera.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);preview.camera.fieldOfView=camera.fieldOfView;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=500;
     FitSwordShoulder.Capture(preview,"Logs/DomainSurvey-"+camera.name.Replace(" ","_")+".png",clone);
    }
    File.WriteAllText("Logs/ChapelDomain-survey.json",JsonUtility.ToJson(report,true));
   }
   finally{preview.Cleanup();foreach(var m in materials.Values)UnityEngine.Object.DestroyImmediate(m);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(main);}
  }
  static Bounds BoundsOf(GameObject g){var r=g.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var x in r)b.Encapsulate(x.bounds);return b;}
 }
}
#endif
