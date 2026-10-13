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
 [InitializeOnLoad] public static class CastlePerimeterWalls
 {
  const string Request="Assets/Editor/CQC/CastlePerimeterWalls.request.txt";
  static CastlePerimeterWalls(){EditorApplication.delayCall+=Pending;}
  static void Pending(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Pending;return;}if(SceneManager.GetActiveScene().name!="MyForest")return;try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastlePerimeterWalls-error.txt",e.ToString());Debug.LogException(e);File.Move(Request,Request+".failed");}}
  [MenuItem("Tools/Buildings/Add Edge Walls And Towers")]public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle missing.");if(mountain.transform.Find("Outer Castle Walls And Towers"))return;
   var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<MeshCollider>();Bounds area=floor.bounds;Vector3 c=area.center;c.y=area.max.y;
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-perimeter-walls-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var towerPrefab=AddMarkedTaverns.Build("KickelhahnTower");var wallPrefab=AddMarkedTaverns.Build("MedievalWall");
   var root=new GameObject("Outer Castle Walls And Towers");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add outer castle walls");
   var walls=new GameObject("Perimeter Wall Sections");walls.transform.SetParent(root.transform,false);var towers=new GameObject("Eight Edge Towers");towers.transform.SetParent(root.transform,false);
   float rx=area.extents.x-3.2f,rz=area.extents.z-3.2f;const int n=96;int placed=0,checks=0;
   for(int i=0;i<n;i++)
   {
    float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n,mid=(a+b)*.5f;
    // Preserve the original south approach rather than walling the player out.
    if(Mathf.Abs(Mathf.DeltaAngle(mid*Mathf.Rad2Deg,270))<9f)continue;
    Vector3 p=Edge(c,rx,rz,a),q=Edge(c,rx,rz,b),target=(p+q)*.5f,direction=(q-p).normalized;
    var go=(GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);go.transform.SetParent(walls.transform,false);go.name="Wall Section "+(++placed).ToString("00");
    go.transform.rotation=Quaternion.LookRotation(Vector3.Cross(direction,Vector3.up));Bounds bounds=BoundsOf(go);go.transform.localScale*=7.5f/bounds.size.y;
    // Source module length follows local X; match each chord while retaining thickness and height.
    float length=Span(Corners(go.transform),direction);Vector3 scale=go.transform.localScale;scale.x*=((q-p).magnitude+.12f)/length;go.transform.localScale=scale;
    GroundCenter(go,target,c.y);FitSupport(go,floor,c);checks+=Corners(go.transform).Count;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   }
   for(int i=0;i<8;i++)
   {
    float a=i*Mathf.PI/4;if(i==6)a+=.24f;Vector3 target=Edge(c,rx,rz,a);
    var go=(GameObject)PrefabUtility.InstantiatePrefab(towerPrefab);go.transform.SetParent(towers.transform,false);go.name="Edge Tower "+(i+1).ToString("00");go.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(target-c,Vector3.up));
    Bounds bounds=BoundsOf(go);go.transform.localScale*=18f/bounds.size.y;bounds=BoundsOf(go);
    float footprint=Mathf.Max(bounds.size.x,bounds.size.z);if(footprint>7f)go.transform.localScale*=7f/footprint;
    GroundCenter(go,target,c.y);FitSupport(go,floor,c);checks+=Corners(go.transform).Count;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   }
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");Selection.activeGameObject=root;SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastlePerimeterWalls-report.txt","Original supplied textured GLBs imported as reusable URP prefabs with mesh colliders.\nPerimeter wall sections: "+placed+", height 7.5m. Towers: 8, height up to 18m.\nAll mesh bounding corners supported by outer floor: "+checks+". South entrance ramp opening preserved. Existing houses and elevator unchanged.\nBackup: "+backup);Debug.Log("Outer castle walls and eight towers added along the edge.");
  }
  static void GroundCenter(GameObject go,Vector3 target,float y){Bounds b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.01f-b.min.y,target.z-b.center.z);}
  static void FitSupport(GameObject go,MeshCollider floor,Vector3 center)
  {
   Vector3 inward=Vector3.ProjectOnPlane(center-BoundsOf(go).center,Vector3.up).normalized;
   for(int attempt=0;attempt<16;attempt++)
   {
    bool supported=true;foreach(var p in Corners(go.transform))if(!floor.Raycast(new Ray(new Vector3(p.x,center.y+100,p.z),Vector3.down),out RaycastHit hit,150)){supported=false;break;}
    if(supported)return;go.transform.position+=inward*.35f;
   }
   throw new InvalidOperationException("Perimeter model overhangs floor: "+go.name);
  }
  static Vector3 Edge(Vector3 c,float rx,float rz,float a){return c+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);}
  static float Span(List<Vector3> points,Vector3 axis){float min=float.PositiveInfinity,max=float.NegativeInfinity;foreach(var p in points){float d=Vector3.Dot(p,axis);min=Mathf.Min(min,d);max=Mathf.Max(max,d);}return max-min;}
  static List<Vector3> Corners(Transform h){var points=new List<Vector3>();foreach(var f in h.GetComponentsInChildren<MeshFilter>()){if(!f.sharedMesh)continue;Bounds b=f.sharedMesh.bounds;for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)points.Add(f.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));}return points;}
  static Bounds BoundsOf(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
 }
}
#endif
