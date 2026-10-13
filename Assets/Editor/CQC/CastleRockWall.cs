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
 [InitializeOnLoad]public static class CastleRockWall
 {
  const string Request="Assets/Editor/CQC/CastleRockWall.request.txt";static double next;
  static CastleRockWall(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleRockWall-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/Forest/Cover Castle Mountain Wall With Scanned Rocks")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Mountain missing.");if(mountain.transform.Find("Overlapping Scanned Rock Wall"))return;
   var slope=mountain.transform.Find("Rocky Mountain Slopes");var collider=slope.GetComponent<MeshCollider>();var renderer=slope.GetComponent<MeshRenderer>();if(!collider||!renderer)throw new InvalidOperationException("Mountain wall mesh missing.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-rock-wall-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   string folder="Assets/CityExpansion/CastleRockWall-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();var prefab=AddMarkedTaverns.Build("CastleRockScan",false);var sourceRenderer=prefab.GetComponentInChildren<MeshRenderer>();if(!sourceRenderer)throw new InvalidOperationException("Imported rock has no renderer.");
   var far=ReadMesh("Assets/Building/ImportedTaverns/CastleRockScan/RockDistant.bytes");AssetDatabase.CreateAsset(far,folder+"/DistantRock.asset");sourceRenderer.sharedMaterial.enableInstancing=true;
   // A matching stone surface beneath the rock overlaps prevents dark wall strips at reserved clearances.
   var backing=new Material(sourceRenderer.sharedMaterial){name="Mountain Rock Backing"};backing.SetTextureScale("_BaseMap",new Vector2(18,6));AssetDatabase.CreateAsset(backing,folder+"/RockBacking.mat");Undo.RecordObject(renderer,"Use rock wall backing");renderer.sharedMaterial=backing;
   var root=new GameObject("Overlapping Scanned Rock Wall");root.transform.SetParent(mountain.transform,false);Undo.RegisterCreatedObjectUndo(root,"Cover mountain with rocks");
   var mills=mountain.transform.Find("Five Slope Windmills");var obstacles=mills?mills.Cast<Transform>().Select(t=>BoundsOf(t.gameObject)).ToArray():Array.Empty<Bounds>();
   var area=collider.bounds;Vector3 center=area.center;float rx=area.extents.x,rz=area.extents.z,circumference=Mathf.PI*(3*(rx+rz)-Mathf.Sqrt((3*rx+rz)*(rx+3*rz)));
   int around=Mathf.CeilToInt(circumference/11),rows=Mathf.CeilToInt(area.size.y/10);float stepY=area.size.y/rows;int count=0,clear=0;var random=new System.Random(114);
   Physics.SyncTransforms();
   for(int row=0;row<rows;row++)for(int i=0;i<around;i++)
   {
    float angle=(i+(row%2)*.5f)*Mathf.PI*2/around;Vector3 outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));Vector3 origin=new Vector3(center.x,area.min.y+(row+.5f)*stepY,center.z)+outward*Mathf.Max(rx,rz)*3;
    if(!collider.Raycast(new Ray(origin,-outward),out RaycastHit hit,Mathf.Max(rx,rz)*6))continue;
    // Preserve the two-track approach and boarding area at the front center.
    if(hit.point.z<center.z&&Mathf.Abs(hit.point.x-center.x)<9){clear++;continue;}
    float width=circumference/around*1.42f,height=stepY*1.4f,depth=3.8f+(float)random.NextDouble()*1.2f;
    var footprint=new Bounds(hit.point,new Vector3(width,height,depth));if(obstacles.Any(b=>FlatNear(footprint,b,2))){clear++;continue;}
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Wall Rock "+(++count).ToString("000");go.transform.SetParent(root.transform,true);go.transform.rotation=Quaternion.LookRotation(outward)*Quaternion.Euler(0,(float)random.NextDouble()*10-5,(float)random.NextDouble()*12-6);
    var source=go.GetComponentInChildren<MeshRenderer>();Vector3 natural=source.GetComponent<MeshFilter>().sharedMesh.bounds.size;go.transform.localScale=new Vector3(width/natural.x,height/natural.y,depth/natural.z);
    var b=BoundsOf(go);Vector3 target=hit.point+outward*.4f;go.transform.position+=target-b.center;b=BoundsOf(go);
    if(b.max.y>area.max.y-.1f)go.transform.position+=Vector3.down*(b.max.y-area.max.y+.1f);
    var distant=new GameObject("Distant Rock");distant.transform.SetParent(source.transform.parent,false);distant.transform.localPosition=source.transform.localPosition;distant.transform.localRotation=source.transform.localRotation;distant.transform.localScale=source.transform.localScale;distant.AddComponent<MeshFilter>().sharedMesh=far;var farRenderer=distant.AddComponent<MeshRenderer>();farRenderer.sharedMaterial=source.sharedMaterial;
    var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.1f,new Renderer[]{source}),new LOD(.002f,new Renderer[]{farRenderer})});lod.RecalculateBounds();PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
   }
   if(count<50)throw new InvalidOperationException("Too few rocks placed to cover the mountain wall.");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CastleRockWall-report.txt","Imported rock_stone_11_.rawscan..glb with original textures. Covered full outer castle-mountain circumference in "+rows+" staggered overlapping rows; "+count+" rocks placed, "+clear+" positions reserved for windmill/rail clearance. Original mountain collider retained; matching rock-texture backing applied. Shared geometry/materials and distant 2643-triangle LOD reduce rendering load. No floors added. Buildings, windmills and funicular retained.\nBackup: "+backup);Debug.Log("Castle mountain wall covered with overlapping scanned rocks. Scene saved.");
  }
  static bool FlatNear(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin&&Mathf.Abs(a.center.y-b.center.y)<a.extents.y+b.extents.y+margin;}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();Bounds b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static Mesh ReadMesh(string path){using(var reader=new BinaryReader(File.OpenRead(path))){int nv=reader.ReadInt32(),ni=reader.ReadInt32();var v=new Vector3[nv];var n=new Vector3[nv];var uv=new Vector2[nv];for(int i=0;i<nv;i++){v[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());n[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());}var idx=new int[ni];for(int i=0;i<ni;i++)idx[i]=reader.ReadInt32();var mesh=new Mesh{name="Scanned Rock Distant",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=idx;mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}}
 }
}
#endif
