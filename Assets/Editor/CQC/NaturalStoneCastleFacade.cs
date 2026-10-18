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
 [InitializeOnLoad]public static class NaturalStoneCastleFacade
 {
  const string Request="Assets/Editor/CQC/NaturalStoneCastleFacade.request.txt";static double next;
  static NaturalStoneCastleFacade(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/NaturalStoneCastleFacade-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/Buildings/Use Supplied Stone Facade On Central Castle")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");var castle=mountain.transform.Find("Central Upper Floor Building");if(!castle||!castle.gameObject.activeInHierarchy)throw new InvalidOperationException("Restored castle missing.");if(castle.Find("Natural Stone Facade And Window Sills"))return;
   const string source="Assets/Building/ImportedTaverns/WarmStoneFacade/";var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(source+"Material0_albedo.png");var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(source+"Material0_normal.png");if(!albedo||!normal)throw new InvalidOperationException("Supplied stone facade textures missing.");
   var shader=Shader.Find("Castle/Medieval Wall Stone");if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("World-mapped castle masonry shader unavailable.");var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-natural-stone-facade-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");string folder="Assets/Building/GothicCastleRemodel/NaturalStone-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   foreach(var pair in new[]{Tuple.Create(AssetDatabase.GetAssetPath(albedo),false),Tuple.Create(AssetDatabase.GetAssetPath(normal),true)}){var importer=AssetImporter.GetAtPath(pair.Item1) as TextureImporter;if(!importer)throw new InvalidOperationException("Stone texture importer missing.");importer.textureType=pair.Item2?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!pair.Item2;importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=4096;importer.SaveAndReimport();}
   var stone=new Material(shader){name="Supplied Natural Rubble Stone"};stone.SetTexture("_StoneTex",albedo);stone.SetTexture("_StoneNormal",normal);stone.SetColor("_BaseColor",Color.white);stone.SetVector("_StoneScale",new Vector4(8,8,0,0));stone.SetFloat("_AutoRoof",0);stone.SetFloat("_ForceRoof",0);stone.doubleSidedGI=true;AssetDatabase.CreateAsset(stone,folder+"/NaturalRubbleStone.mat");var trim=new Material(stone){name="Lighter Rubble Stone Window Trim"};trim.SetColor("_BaseColor",new Color(1.12f,1.09f,1.02f));AssetDatabase.CreateAsset(trim,folder+"/StoneWindowTrim.mat");
   int surfaces=0;foreach(var renderer in castle.GetComponentsInChildren<MeshRenderer>())
   {
    var materials=renderer.sharedMaterials;bool changed=false;for(int i=0;i<materials.Length;i++){var m=materials[i];if(!m)continue;string name=m.name.ToLowerInvariant();if(name.Contains("slate")||name.Contains("roof")||name.Contains("oak")||name.Contains("amber")||name.Contains("mullion")||name.Contains("window")&&!name.Contains("stone"))continue;if(m.shader==shader){materials[i]=name.Contains("trim")||name.Contains("surround")?trim:stone;changed=true;surfaces++;}}
    if(changed){Undo.RecordObject(renderer,"Apply supplied natural stone facade");renderer.sharedMaterials=materials;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
   }
   if(surfaces==0)throw new InvalidOperationException("Castle masonry materials not identified; no unrelated materials replaced.");var root=new GameObject("Natural Stone Facade And Window Sills");root.transform.SetParent(castle,false);Undo.RegisterCreatedObjectUndo(root,"Add natural stone window sills");var points=new List<Vector3>();var indices=new List<int>();int sills=0;
   var frames=castle.Find("Gothic Chapel Style Details/Pointed Stone Window Surrounds");if(frames){var filter=frames.GetComponent<MeshFilter>();var v=filter.sharedMesh.vertices;
    // Each existing pointed window surround consists of five eight-vertex beams.
    for(int i=0;i+39<v.Length;i+=40){Vector3 left=Vector3.zero,right=Vector3.zero;for(int k=0;k<4;k++){left+=filter.transform.TransformPoint(v[i+16+k])*.25f;right+=filter.transform.TransformPoint(v[i+20+k])*.25f;}var paneNormal=(filter.transform.TransformPoint(v[i+18])-filter.transform.TransformPoint(v[i+17])).normalized;var axis=(right-left).normalized;float width=Vector3.Distance(left,right);if(width<.2f||width>4.5f)continue;var center=(left+right)*.5f-Vector3.up*.07f+paneNormal*.06f;AddBox(root.transform,center,axis,paneNormal,width+.22f,.16f,.32f,points,indices);sills++;}}
   if(points.Count>0){var mesh=new Mesh{name="Projecting Natural Stone Window Sills",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(points);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/StoneWindowSills.asset");var sillsObject=new GameObject("Projecting Stone Window Sills");sillsObject.transform.SetParent(root.transform,false);sillsObject.AddComponent<MeshFilter>().sharedMesh=mesh;sillsObject.AddComponent<MeshRenderer>().sharedMaterial=trim;}
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/NaturalStoneCastleFacade-report.txt","Applied original 4096px gray/brown rubble-stone albedo and normal texture from stone_facade_substance_alchemist_material.glb to "+surfaces+" castle masonry material slots, including window surrounds. Used consistent world-space scale and untinted original color. Added "+sills+" projecting stone window sills with lighter matching trim. Slate roofing, oak doors, highlighted amber glass and mullions retained. Car remains removed. Castle geometry/placement, chapel, walls, houses, fences and terrain unchanged. Imported material-preview sphere not added to scene.\nBackup: "+backup);Debug.Log("Supplied natural stone castle facade and matching window sills applied. Scene saved.");
  }
  static void AddBox(Transform parent,Vector3 center,Vector3 right,Vector3 normal,float width,float height,float depth,List<Vector3> vertices,List<int> triangles){int start=vertices.Count;for(int z=0;z<2;z++)for(int y=0;y<2;y++)for(int x=0;x<2;x++)vertices.Add(parent.InverseTransformPoint(center+right*(x==0?-width*.5f:width*.5f)+Vector3.up*(y==0?-height*.5f:height*.5f)+normal*(z==0?-depth*.5f:depth*.5f)));int[] faces={0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5};foreach(int i in faces)triangles.Add(start+i);}
 }
}
#endif
