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
 [InitializeOnLoad]public static class BrickTavernFoundations
 {
  const string Request="Assets/Editor/CQC/BrickTavernFoundations.request.txt";static double next;
  static BrickTavernFoundations(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BrickTavernFoundations-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/City/Replace Plain House Bases With Realistic Brick Paving")]
  public static void Apply()
  {
   var city=GameObject.Find("Lower Castle City");if(!city)throw new InvalidOperationException("Lower city missing.");if(city.transform.Find("House Bases Brick Paving Applied"))return;
   var foundations=city.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Stone House Foundation",StringComparison.Ordinal)&&f.GetComponent<Renderer>()).ToArray();if(foundations.Length==0)throw new InvalidOperationException("House foundation slabs missing.");
   const string textures="Assets/YughuesFreePavementsMaterials/Textures/";var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(textures+"T_YFPM_Pavement01_d.tga");var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(textures+"T_YFPM_Pavement01_n.tga");if(!albedo||!normal)throw new InvalidOperationException("Brick paving textures missing.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-brick-house-bases-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   string folder="Assets/CityExpansion/BrickHouseBases-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(albedo)) as TextureImporter;if(ai&&ai.wrapMode!=TextureWrapMode.Repeat){ai.wrapMode=TextureWrapMode.Repeat;ai.SaveAndReimport();}var ni=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;if(ni&&(ni.textureType!=TextureImporterType.NormalMap||ni.wrapMode!=TextureWrapMode.Repeat)){ni.textureType=TextureImporterType.NormalMap;ni.wrapMode=TextureWrapMode.Repeat;ni.SaveAndReimport();}
   var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("URP Lit missing.");var material=new Material(shader){name="Realistic Brick House Paving"};material.SetTexture("_BaseMap",albedo);material.SetColor("_BaseColor",Color.white);material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.85f);material.EnableKeyword("_NORMALMAP");material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.16f);material.enableInstancing=true;AssetDatabase.CreateAsset(material,folder+"/RealisticBrickPaving.mat");
   int count=0;foreach(var filter in foundations)
   {
    var original=filter.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);mesh.name=filter.name+" Brick UV";var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
    // Use meters to keep the bricks a consistent size on every slab and side face.
    for(int i=0;i<vertices.Length;i++){var world=filter.transform.TransformPoint(vertices[i]);var normalWorld=filter.transform.TransformDirection(normals[i]);if(Mathf.Abs(normalWorld.y)>.5f)uv[i]=new Vector2(world.x,world.z)/2f;else if(Mathf.Abs(normalWorld.x)>.5f)uv[i]=new Vector2(world.z,world.y)/2f;else uv[i]=new Vector2(world.x,world.y)/2f;}
    mesh.uv=uv;mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,folder+"/Foundation-"+(++count).ToString("000")+".asset");Undo.RecordObject(filter,"Tile realistic bricks on foundation");filter.sharedMesh=mesh;var renderer=filter.GetComponent<Renderer>();Undo.RecordObject(renderer,"Replace plain base material");renderer.sharedMaterial=material;
   }
   var marker=new GameObject("House Bases Brick Paving Applied");marker.transform.SetParent(city.transform,false);Undo.RegisterCreatedObjectUndo(marker,"Remember brick paving change");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/BrickTavernFoundations-report.txt","Replaced "+count+" plain house foundation materials with existing Yughues brick pavement albedo and normal map in URP Lit. Rebuilt UVs at two meters per texture repeat for consistent brick scale on tops and sides. Foundation vertices, transforms, box colliders, houses, fences, trees, roads and terrain unchanged.\nBackup: "+backup);Debug.Log("House bases now use textured brick paving. Scene saved.");
  }
 }
}
#endif
