#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad]public static class SlantedCastleRockWall
 {
  const string Request="Assets/Editor/CQC/SlantedCastleRockWall.request.txt";static double next;
  static SlantedCastleRockWall(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SlantedCastleRockWall-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/Forest/Fit Dense Slanted Rocks To Castle Mountain")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Mountain missing.");var previous=mountain.transform.Find("Overlapping Scanned Rock Wall");if(!previous)throw new InvalidOperationException("Existing rock wall missing.");
   var slope=mountain.transform.Find("Rocky Mountain Slopes");var collider=slope.GetComponent<MeshCollider>();var renderer=slope.GetComponent<MeshRenderer>();if(!collider||!renderer)throw new InvalidOperationException("Mountain wall mesh missing.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-slanted-rock-wall-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   string folder="Assets/CityExpansion/SlantedCastleRockWall-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();var prefab=AddMarkedTaverns.Build("CastleRockScan",false);var sourceRenderer=prefab.GetComponentInChildren<MeshRenderer>();if(!sourceRenderer)throw new InvalidOperationException("Imported rock has no renderer.");
   var far=ReadMesh("Assets/Building/ImportedTaverns/CastleRockScan/RockDistant.bytes");AssetDatabase.CreateAsset(far,folder+"/DistantRock.asset");sourceRenderer.sharedMaterial.enableInstancing=true;
   // A matching stone surface beneath the rock overlaps prevents dark wall strips at reserved clearances.
   var backing=new Material(sourceRenderer.sharedMaterial){name="Mountain Rock Backing"};backing.SetTextureScale("_BaseMap",new Vector2(18,6));AssetDatabase.CreateAsset(backing,folder+"/RockBacking.mat");Undo.RecordObject(renderer,"Use rock wall backing");renderer.sharedMaterial=backing;
   var root=new GameObject("Dense Slanted Rock Wall");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Cover mountain with rocks");
   var mills=mountain.transform.Find("Five Slope Windmills");var obstacles=mills?mills.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponents<MonoBehaviour>().Any(c=>c.GetType().Name=="WindmillRotor")).Select(r=>r.bounds).ToArray():Array.Empty<Bounds>();
   var area=collider.bounds;Vector3 center=area.center;float rx=area.extents.x,rz=area.extents.z,circumference=Mathf.PI*(3*(rx+rz)-Mathf.Sqrt((3*rx+rz)*(rx+3*rz)));
   int around=Mathf.CeilToInt(circumference/6),rows=Mathf.CeilToInt(area.size.y/5);float stepY=area.size.y/rows;int count=0,clear=0;var random=new System.Random(114);
   Physics.SyncTransforms();
   for(int row=0;row<rows;row++)for(int i=0;i<around;i++)
   {
    float angle=(i+(row%2)*.5f)*Mathf.PI*2/around;Vector3 outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));Vector3 origin=new Vector3(center.x,area.min.y+(row+.5f)*stepY,center.z)+outward*Mathf.Max(rx,rz)*3;
    if(!collider.Raycast(new Ray(origin,-outward),out RaycastHit hit,Mathf.Max(rx,rz)*6))continue;
    // Preserve the two-track approach and boarding area at the front center.
    if(hit.point.z<center.z&&Mathf.Abs(hit.point.x-center.x)<9){clear++;continue;}
    Vector3 normal=hit.normal.normalized;if(Vector3.Dot(normal,outward)<0)normal=-normal;
    Vector3 uphill=Vector3.ProjectOnPlane(Vector3.up,normal).normalized;if(uphill.sqrMagnitude<.01f)uphill=Vector3.up;
    float width=circumference/around*(1.45f+(float)random.NextDouble()*.15f),height=stepY*1.5f/Mathf.Max(.3f,uphill.y),depth=1.3f+(float)random.NextDouble()*.5f;
    var footprint=new Bounds(hit.point+normal*.2f,new Vector3(width,stepY*1.5f,depth));if(obstacles.Any(b=>FlatNear(footprint,b,.4f))){clear++;continue;}
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Slope-Fitted Rock "+(++count).ToString("0000");go.transform.SetParent(root.transform,true);
    // Face along the actual surface normal; the rock's long vertical axis follows the slope.
    go.transform.rotation=Quaternion.LookRotation(normal,uphill)*Quaternion.Euler(0,0,(float)random.NextDouble()*36-18);
    var source=go.GetComponentInChildren<MeshRenderer>();Vector3 natural=source.GetComponent<MeshFilter>().sharedMesh.bounds.size;go.transform.localScale=new Vector3(width/natural.x,height/natural.y,depth/natural.z);
    var b=BoundsOf(go);Vector3 target=hit.point-normal*(depth*.28f);go.transform.position+=target-b.center;b=BoundsOf(go);
    if(b.max.y>area.max.y-.1f)go.transform.position+=Vector3.down*(b.max.y-area.max.y+.1f);
    if(Vector3.Dot(go.transform.forward,normal)<.99f)throw new InvalidOperationException("Rock normal alignment failed.");
    var distant=new GameObject("Distant Rock");distant.transform.SetParent(source.transform.parent,false);distant.transform.localPosition=source.transform.localPosition;distant.transform.localRotation=source.transform.localRotation;distant.transform.localScale=source.transform.localScale;distant.AddComponent<MeshFilter>().sharedMesh=far;var farRenderer=distant.AddComponent<MeshRenderer>();farRenderer.sharedMaterial=source.sharedMaterial;
    var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.18f,new Renderer[]{source}),new LOD(.002f,new Renderer[]{farRenderer})});lod.RecalculateBounds();PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   }
   if(count<400)throw new InvalidOperationException("Too few rocks placed to cover the mountain wall.");Undo.DestroyObjectImmediate(previous.gameObject);root.name="Overlapping Scanned Rock Wall";AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SlantedCastleRockWall-report.txt","Replaced previous 269-rock shell with "+count+" smaller densely overlapping rocks in "+rows+" staggered rows around the mountain. Every rock aligned to the sampled wall normal and embedded 28% of its depth behind the surface; roughly 50% overlap with irregular in-plane rotations. Rail corridor and rotating blade clearance retained ("+clear+" skipped positions). Upper edge kept below castle summit. No floors added. Distant LOD retained.\nBackup: "+backup);Debug.Log("Dense slanted rocks fitted tightly to the castle mountain surface. Scene saved.");
  }
  static bool FlatNear(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin&&Mathf.Abs(a.center.y-b.center.y)<a.extents.y+b.extents.y+margin;}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();Bounds b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static Mesh ReadMesh(string path){using(var reader=new BinaryReader(File.OpenRead(path))){int nv=reader.ReadInt32(),ni=reader.ReadInt32();var v=new Vector3[nv];var n=new Vector3[nv];var uv=new Vector2[nv];for(int i=0;i<nv;i++){v[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());n[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());}var idx=new int[ni];for(int i=0;i<ni;i++)idx[i]=reader.ReadInt32();var mesh=new Mesh{name="Scanned Rock Distant",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=idx;mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}}
 }
}
#endif
