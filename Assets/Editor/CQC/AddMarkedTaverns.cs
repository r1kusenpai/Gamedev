#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class AddMarkedTaverns
 {
  const string Folder="Assets/Building/ImportedTaverns",Request="Assets/Editor/CQC/AddMarkedTaverns.request.txt";
  [Serializable] public class Model {public string name;public Mat[] materials;public Primitive[] primitives;}
  [Serializable] public class Mat {public string name,albedo,normal,packed,occlusion,emission,alphaMode;public float[] baseFactor,emissionFactor;public float metal,rough,alphaCutoff;public bool doubleSided;}
  [Serializable] public class Primitive {public string file;public int material;}
  static AddMarkedTaverns(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating||File.Exists("Assets/Editor/CQC/CastleInnerOutline.request.txt")){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddMarkedTaverns-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Add Two Tavern Types In Marked Gaps")] public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");
   if(mountain.transform.Find("Added Marked Taverns"))return;
   var houses=mountain.transform.Find("One Ring Of Houses");var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshCollider>();
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-marked-taverns-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var prefabs=new[]{Build("MedievalTavern"),Build("TallTavern")};Bounds area=floor.bounds;Vector3 center=area.center;
   var obstacles=new List<Bounds>();var angles=new List<float>();
   foreach(Transform h in houses){Bounds b=BoundsOf(h.gameObject);obstacles.Add(b);float a=Mathf.Atan2((b.center.z-center.z)/area.extents.z,(b.center.x-center.x)/area.extents.x);if(a<0)a+=Mathf.PI*2;angles.Add(a);}
   angles.Sort();if(angles.Count<2)throw new InvalidOperationException("Existing ring missing.");
   var root=new GameObject("Added Marked Taverns");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add marked taverns");int count=0;
   for(int gap=0;gap<angles.Count;gap++)for(int kind=0;kind<2;kind++)
   {
    float next=gap+1<angles.Count?angles[gap+1]:angles[0]+Mathf.PI*2;float a=Mathf.Lerp(angles[gap],next,(kind+1)/3f);
    Vector3 p=center+new Vector3(Mathf.Cos(a)*area.extents.x*.85f,0,Mathf.Sin(a)*area.extents.z*.85f);
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind]);go.transform.SetParent(root.transform,false);go.name=(kind==0?"Medieval Tavern ":"Tall Tavern ")+(gap+1).ToString("00");
    go.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(p-center,Vector3.up));Bounds b=BoundsOf(go);
    float clearance=100;foreach(Bounds other in obstacles){var flat=other;flat.center=new Vector3(other.center.x,0,other.center.z);flat.size=new Vector3(other.size.x,0,other.size.z);Vector3 q=new Vector3(p.x,0,p.z);clearance=Mathf.Min(clearance,Vector3.Distance(q,flat.ClosestPoint(q)));}
    float maxRadius=Mathf.Max(.6f,clearance-.6f);float radius=new Vector2(b.extents.x,b.extents.z).magnitude;
    float scale=Mathf.Min(10f/b.size.y,8f/Mathf.Max(b.size.x,b.size.z),maxRadius/radius);go.transform.localScale*=scale;b=BoundsOf(go);
    if(!floor.Raycast(new Ray(new Vector3(p.x,area.max.y+100,p.z),Vector3.down),out RaycastHit hit,150))throw new InvalidOperationException("Marked spot not on castle floor.");
    go.transform.position+=new Vector3(p.x-b.center.x,hit.point.y+.02f-b.min.y,p.z-b.center.z);b=BoundsOf(go);
    foreach(Vector3 corner in new[]{new Vector3(b.min.x,area.max.y+100,b.min.z),new Vector3(b.max.x,area.max.y+100,b.min.z),new Vector3(b.min.x,area.max.y+100,b.max.z),new Vector3(b.max.x,area.max.y+100,b.max.z)})if(!floor.Raycast(new Ray(corner,Vector3.down),out hit,150))throw new InvalidOperationException("Tavern footprint outside floor.");
    obstacles.Add(b);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);count++;
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Selection.activeGameObject=root;SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddMarkedTaverns-report.txt","Added "+count+" taverns, alternating the two supplied GLB models between original houses.\nEmbedded colours and PBR maps retained in URP materials; reusable prefabs and mesh colliders created.\nAll building footprint corners verified on outer floor. Existing houses preserved.\nBackup: "+backup);Debug.Log("Both new tavern types added around marked house ring.");
  }
  public static GameObject Build(string key, bool addColliders = true)
  {
   string path=Folder+"/"+key;var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path+"/"+key+".prefab");if(existing)return existing;
   var model=JsonUtility.FromJson<Model>(File.ReadAllText(path+"/Model.json"));var materials=new Material[model.materials.Length];
   for(int i=0;i<materials.Length;i++)
   {
    var d=model.materials[i];var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=key+"_"+d.name};mat.SetColor("_BaseColor",new Color(d.baseFactor[0],d.baseFactor[1],d.baseFactor[2],d.baseFactor[3]));mat.SetTexture("_BaseMap",Tex(path,d.albedo,false));mat.SetFloat("_Metallic",d.metal);mat.SetFloat("_Smoothness",1-d.rough);
    if(!string.IsNullOrEmpty(d.normal)){mat.SetTexture("_BumpMap",Tex(path,d.normal,true));mat.EnableKeyword("_NORMALMAP");}
    if(!string.IsNullOrEmpty(d.packed)){mat.SetTexture("_MetallicGlossMap",Tex(path,d.packed,false,true));mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");}
    if(!string.IsNullOrEmpty(d.occlusion)){mat.SetTexture("_OcclusionMap",Tex(path,d.occlusion,false,true));mat.EnableKeyword("_OCCLUSIONMAP");}
    if(!string.IsNullOrEmpty(d.emission)){mat.SetTexture("_EmissionMap",Tex(path,d.emission,false));mat.SetColor("_EmissionColor",new Color(d.emissionFactor[0],d.emissionFactor[1],d.emissionFactor[2]));mat.EnableKeyword("_EMISSION");}
    mat.SetFloat("_Cull",d.doubleSided?0:2);mat.doubleSidedGI=d.doubleSided;mat.enableInstancing=true;
    if(d.alphaMode=="MASK"){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",d.alphaCutoff>0?d.alphaCutoff:.5f);mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;}
    if(d.alphaMode=="BLEND"){mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=3000;}
    AssetDatabase.CreateAsset(mat,path+"/Material"+i+".mat");materials[i]=mat;
   }
   var root=new GameObject(key);
   try{for(int i=0;i<model.primitives.Length;i++)
   {
    var p=model.primitives[i];using(var reader=new BinaryReader(File.OpenRead(path+"/"+p.file)))
    {
     int nv=reader.ReadInt32(),ni=reader.ReadInt32();var v=new Vector3[nv];var normals=new Vector3[nv];var uv=new Vector2[nv];for(int j=0;j<nv;j++){v[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());normals[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());uv[j]=new Vector2(reader.ReadSingle(),reader.ReadSingle());}var indices=new int[ni];for(int j=0;j<ni;j++)indices[j]=reader.ReadInt32();
     var mesh=new Mesh{name=key+"Mesh"+i,indexFormat=IndexFormat.UInt32};mesh.vertices=v;mesh.normals=normals;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path+"/Mesh"+i+".asset");
     var part=new GameObject("Model Part "+i);part.transform.SetParent(root.transform,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=materials[p.material];if(addColliders)part.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
   }return PrefabUtility.SaveAsPrefabAsset(root,path+"/"+key+".prefab");}finally{UnityEngine.Object.DestroyImmediate(root);}
  }
  static Texture2D Tex(string folder,string file,bool normal,bool linear=false){if(string.IsNullOrEmpty(file))return null;string path=folder+"/"+file;AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=AssetImporter.GetAtPath(path) as TextureImporter;imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;imp.sRGBTexture=!normal&&!linear;imp.maxTextureSize=4096;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();Bounds b=r[0].bounds;foreach(var item in r)b.Encapsulate(item.bounds);return b;}
 }
}
#endif


