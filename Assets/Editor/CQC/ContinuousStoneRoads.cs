#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class ContinuousStoneRoads
 {
  const string Request="Assets/Editor/CQC/ContinuousStoneRoads.request.txt";static double next;
  static ContinuousStoneRoads(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ContinuousStoneRoads-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/City/Use Continuous Hand Painted Stone Roads")]
  public static void Apply()
  {
   var city=GameObject.Find("Lower Castle City");if(!city)throw new InvalidOperationException("Town missing.");var roads=city.transform.Find("Winding Roads and Market Square");if(!roads)throw new InvalidOperationException("Town roads missing.");
   const string texturePath="Assets/Whaleride/Hand Painted Rocks Road (Blocky)/road_rocks_blocky.png";
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);var normal=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Whaleride/Hand Painted Rocks Road (Blocky)/road_rocks_blocky_NRM.png");if(!texture)throw new InvalidOperationException("Imported road texture missing.");
   var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new InvalidOperationException("URP shader missing.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-continuous-roads-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   string folder="Assets/CityExpansion/ContinuousRoads-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;if(importer&&importer.wrapMode!=TextureWrapMode.Repeat){importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();}
   var mat=new Material(shader){name="Hand Painted Stone Town Roads"};mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.08f);mat.enableInstancing=true;
   if(normal){var ni=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;if(ni&&ni.textureType!=TextureImporterType.NormalMap){ni.textureType=TextureImporterType.NormalMap;ni.SaveAndReimport();}mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");}
   AssetDatabase.CreateAsset(mat,folder+"/RoadStone.mat");int count=0,joins=0;
   foreach(var filter in roads.GetComponentsInChildren<MeshFilter>())
   {
    var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!filter.sharedMesh)continue;
    Undo.RecordObject(renderer,"Change road stone texture");renderer.sharedMaterial=mat;
    if(filter.name=="Central Market Square")continue;
    var mesh=BuildContinuous(filter.sharedMesh,filter.transform,out int stitched);AssetDatabase.CreateAsset(mesh,folder+"/Road-"+(count++)+".asset");
    Undo.RecordObject(filter,"Connect curved road edges");filter.sharedMesh=mesh;var col=filter.GetComponent<MeshCollider>();if(col){Undo.RecordObject(col,"Continuous road collision");col.sharedMesh=mesh;}
    joins+=stitched;
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ContinuousStoneRoads-report.txt","Replaced town roads and market square surface with imported hand painted rock-road albedo and normal textures. Rebuilt "+count+" curved roads as continuous strips; "+joins+" joins now share edge positions and vertices. World-space UVs maintain matching stone scale. Terrain-conforming colliders updated; houses, castle and ground blend retained.\nBackup: "+backup);Debug.Log("Hand painted town roads ready; curved joins connected. Scene saved.");
  }
  public static Mesh BuildContinuous(Mesh source,Transform transform,out int joins)
  {
   var original=source.vertices;if(original.Length%4!=0)throw new InvalidOperationException("Expected segmented road geometry: "+source.name);
   var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();joins=0;
   int segmentCount=original.Length/4,start=0;
   while(start<segmentCount)
   {
    int end=start;while(end+1<segmentCount&&Vector3.Distance((original[end*4+2]+original[end*4+3])*.5f,(original[(end+1)*4]+original[(end+1)*4+1])*.5f)<.1f)end++;
    int segments=end-start+1;var centers=new Vector3[segments+1];var half=new float[segments+1];
    for(int i=0;i<segments;i++){int k=(start+i)*4;centers[i]=(original[k]+original[k+1])*.5f;half[i]=Vector3.Distance(original[k],original[k+1])*.5f;}
    int last=end*4;centers[segments]=(original[last+2]+original[last+3])*.5f;half[segments]=Vector3.Distance(original[last+2],original[last+3])*.5f;
    bool closed=Vector3.Distance(centers[0],centers[segments])<.1f;
    if(closed){centers[segments]=centers[0];half[segments]=half[0];}
    int offset=vertices.Count;
    for(int i=0;i<=segments;i++)
    {
     Vector3 prev=centers[Mathf.Max(0,i-1)],next=centers[Mathf.Min(segments,i+1)];if(closed&&(i==0||i==segments)){prev=centers[segments-1];next=centers[1];}
     Vector3 before=Vector3.ProjectOnPlane(centers[i]-prev,Vector3.up).normalized,after=Vector3.ProjectOnPlane(next-centers[i],Vector3.up).normalized;if(before.sqrMagnitude<.1f)before=after;if(after.sqrMagnitude<.1f)after=before;
     Vector3 n0=Vector3.Cross(Vector3.up,before),n1=Vector3.Cross(Vector3.up,after),side=(n0+n1).normalized;if(side.sqrMagnitude<.1f)side=n1;
     float length=half[i]/Mathf.Max(.5f,Vector3.Dot(side,n1));
     for(int j=0;j<2;j++){Vector3 local=centers[i]+side*length*(j==0?-1:1);Vector3 world=transform.TransformPoint(local);float ground=Ground(world);if(float.IsNaN(ground))throw new InvalidOperationException("Road edge outside terrain.");world.y=ground+.10f;vertices.Add(transform.InverseTransformPoint(world));uv.Add(new Vector2(world.x/3,world.z/3));}
    }
    for(int i=0;i<segments;i++){int a=offset+i*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}joins+=Mathf.Max(0,segments-1)+(closed?1:0);start=end+1;
   }
   var mesh=new Mesh{name=source.name+" Continuous",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
  }
  static float Ground(Vector3 p){foreach(var t in Terrain.activeTerrains){var pos=t.transform.position;var size=t.terrainData.size;if(p.x>=pos.x&&p.x<=pos.x+size.x&&p.z>=pos.z&&p.z<=pos.z+size.z)return t.SampleHeight(p)+pos.y;}return float.NaN;}
 }
}
#endif
