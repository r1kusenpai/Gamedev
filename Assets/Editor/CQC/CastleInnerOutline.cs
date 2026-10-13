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
 [InitializeOnLoad] public static class CastleInnerOutline
 {
  const string Request="Assets/Editor/CQC/CastleInnerOutline.request.txt";
  static CastleInnerOutline(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleInnerOutline-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Enlarge Inner Floor With Vertical Walls")] public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");
   var inner=mountain.transform.Find("Inner Castle Mountain");if(!inner)throw new InvalidOperationException("Inner mountain missing.");
   var outer=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshRenderer>().bounds;
   var top=inner.Find("Flat Inner Castle Floor");var wall=inner.Find("Inner Rocky Mountain Sides");var ramp=inner.Find("Inner Mountain Access Ramp");
   float y=top.GetComponent<MeshRenderer>().bounds.max.y,ground=outer.max.y;
   Vector3 c=outer.center;c.y=ground;float rx=outer.extents.x*.68f,rz=outer.extents.z*.68f;
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-vertical-inner-walls-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   if(!ramp){var entrance=new GameObject("Inner Mountain Access Ramp");entrance.transform.SetParent(inner,false);entrance.AddComponent<MeshFilter>();entrance.AddComponent<MeshRenderer>().sharedMaterial=top.GetComponent<MeshRenderer>().sharedMaterial;entrance.AddComponent<MeshCollider>();Undo.RegisterCreatedObjectUndo(entrance,"Restore inner entrance");ramp=entrance.transform;}
   const int n=128;var v=new List<Vector3>{new Vector3(c.x,y,c.z)};var t=new List<int>();
   for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;v.Add(new Vector3(c.x+Mathf.Cos(a)*rx,y,c.z+Mathf.Sin(a)*rz));}
   for(int i=0;i<n;i++)t.AddRange(new[]{0,(i+1)%n+1,i+1});Replace(top,v,t,"ExpandedInnerFloor",false);
   v=new List<Vector3>();t=new List<int>();
   for(int row=0;row<2;row++)for(int i=0;i<=n;i++){float a=i*Mathf.PI*2/n;v.Add(new Vector3(c.x+Mathf.Cos(a)*rx,row==0?y:ground,c.z+Mathf.Sin(a)*rz));}
   for(int i=0;i<n;i++)t.AddRange(new[]{i,i+1,n+1+i,i+1,n+2+i,n+1+i});
   for(int i=0;i<=n;i++){if((v[i]-v[i+n+1]).x!=0||(v[i]-v[i+n+1]).z!=0)throw new InvalidOperationException("Wall is not vertical.");}
   Replace(wall,v,t,"VerticalInnerWalls",true);
   // The long axis leaves room for the entrance between two houses.
   float start=c.x-rx-24,end=c.x-rx+1;
   v=new List<Vector3>{new Vector3(start,ground,c.z-3.5f),new Vector3(start,ground,c.z+3.5f),new Vector3(end,y+.04f,c.z-3.5f),new Vector3(end,y+.04f,c.z+3.5f),new Vector3(end,ground,c.z-3.5f),new Vector3(end,ground,c.z+3.5f)};
   Replace(ramp,v,new List<int>{0,1,2,1,3,2,0,2,4,1,5,3,2,3,4,3,5,4},"ExpandedInnerEntranceRamp",false);
   Physics.SyncTransforms();var collider=top.GetComponent<MeshCollider>();int checks=0;
   foreach(Vector3 offset in new[]{Vector3.zero,Vector3.right*rx*.9f,Vector3.left*rx*.9f,Vector3.forward*rz*.9f,Vector3.back*rz*.9f})
    {if(!collider.Raycast(new Ray(new Vector3(c.x,y+20,c.z)+offset,Vector3.down),out RaycastHit hit,40)||Mathf.Abs(hit.point.y-y)>.02f)throw new InvalidOperationException("Enlarged floor collision check failed.");checks++;}
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleInnerOutline-report.txt","Inner floor expanded to "+(rx*2)+" x "+(rz*2)+" metres to match marked inner outline.\nAll 129 wall pairs verified vertical at 90 degrees.\nLevel summit collision checks: "+checks+". Entrance ramp repositioned between houses.\nPaving, house positions, and outer mountain preserved.\nBackup: "+backup);Selection.activeGameObject=inner.gameObject;SceneView.RepaintAll();Debug.Log("Inner floor enlarged. Walls are now vertical at 90 degrees.");
  }
  static void Replace(Transform obj,List<Vector3> world,List<int> triangles,string name,bool wall)
  {
   var mesh=new Mesh{name=name};var uv=new List<Vector2>();var local=new List<Vector3>();
   for(int i=0;i<world.Count;i++){local.Add(obj.InverseTransformPoint(world[i]));uv.Add(wall?new Vector2((i%129)/128f*60f,world[i].y/4f):new Vector2(world[i].x/4f,world[i].z/4f));}
   mesh.SetVertices(local);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
   AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath("Assets/Building/Cobblestone/"+name+".asset"));
   var filter=obj.GetComponent<MeshFilter>();var collider=obj.GetComponent<MeshCollider>();Undo.RecordObject(filter,"Resize inner castle mountain");Undo.RecordObject(collider,"Resize inner castle collision");filter.sharedMesh=mesh;collider.sharedMesh=null;collider.sharedMesh=mesh;EditorUtility.SetDirty(filter);EditorUtility.SetDirty(collider);
  }
 }
}
#endif
