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
 [InitializeOnLoad]public static class GothicCastleRemodel
 {
  const string Request="Assets/Editor/CQC/GothicCastleRemodel.request.txt";static double next;
  static GothicCastleRemodel(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GothicCastleRemodel-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  const string Folder="Assets/Building/GothicCastleRemodel";
  [Serializable]class Names{public Entry[] items=Array.Empty<Entry>();}[Serializable]class Entry{public string guid="",name="";}
  [MenuItem("Tools/Buildings/Remodel Central Castle To Chapel Theme And Remove Car")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Castle Mountain missing.");var castle=mountain.transform.Find("Central Upper Floor Building");if(!castle||!castle.gameObject.activeInHierarchy)throw new InvalidOperationException("Restored central castle must be active.");if(castle.Find("Gothic Chapel Style Details"))return;
   var sourceNames=JsonUtility.FromJson<Names>(File.ReadAllText(Folder+"/OriginalMaterialNames.json")).items.ToDictionary(e=>e.guid,e=>e.name);
   var shader=Shader.Find("Castle/Medieval Wall Stone");if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Castle masonry shader unavailable.");var stoneTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Building/ImportedTaverns/UpperCastleChurch/Material2_albedo.png");var stoneNormal=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Building/ImportedTaverns/UpperCastleChurch/Material2_normal.png");if(!stoneTexture||!stoneNormal)throw new InvalidOperationException("Chapel stone textures missing.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-gothic-castle-remodel-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");string output=Folder+"/Remodel-"+stamp;Directory.CreateDirectory(output);AssetDatabase.Refresh();var castleMatrix=castle.localToWorldMatrix;var bounds=BoundsOf(castle.gameObject);
   var carParts=castle.GetComponentsInChildren<Transform>(true).Where(t=>t!=castle&&IsCar(t.name)).ToList();if(carParts.Count==0)throw new InvalidOperationException("Named car parts were not found; no unrelated object removed.");Bounds carBounds=BoundsOf(carParts[0].gameObject);foreach(var t in carParts)carBounds.Encapsulate(BoundsOf(t.gameObject));carBounds.Expand(.25f);
   foreach(var renderer in castle.GetComponentsInChildren<Renderer>()){if(carParts.Any(t=>renderer.transform==t||renderer.transform.IsChildOf(t)))continue;var b=renderer.bounds;if(carBounds.Contains(b.min)&&carBounds.Contains(b.max))carParts.Add(renderer.transform);}
   var removed=new List<string>();foreach(var t in carParts.Distinct()){Undo.RecordObject(t.gameObject,"Remove modern car from castle");t.gameObject.SetActive(false);removed.Add(t.name);}
   Material stone=Stone(shader,"Chapel Dark Masonry",stoneTexture,stoneNormal,Color.white,bounds,output);Material trim=Stone(shader,"Gothic Window Stone Trim",stoneTexture,stoneNormal,new Color(1.45f,1.35f,1.2f),bounds,output);Material slate=Stone(shader,"Chapel Slate Roof",stoneTexture,stoneNormal,new Color(.28f,.30f,.34f),bounds,output);slate.SetFloat("_ForceRoof",1);
   Material oak=Lit("Medieval Dark Oak",new Color(.18f,.075f,.025f),.15f,output);Material glass=Lit("Highlighted Amber Windows",new Color(.17f,.115f,.055f),.65f,output);glass.SetColor("_EmissionColor",new Color(.62f,.27f,.07f));glass.EnableKeyword("_EMISSION");glass.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
   Material metal=Lit("Antique Window Mullions",new Color(.25f,.18f,.075f),.35f,output);metal.SetFloat("_Metallic",.45f);
   var detail=new GameObject("Gothic Chapel Style Details");detail.transform.SetParent(castle,false);Undo.RegisterCreatedObjectUndo(detail,"Add Gothic window surrounds");var frames=new Geometry(detail.transform);var mullions=new Geometry(detail.transform);int slots=0,windows=0;
   foreach(var renderer in castle.GetComponentsInChildren<MeshRenderer>())
   {
    var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;var original=PrefabUtility.GetCorrespondingObjectFromSource(renderer);var source=original?original.sharedMaterials:renderer.sharedMaterials;var materials=renderer.sharedMaterials;Undo.RecordObject(renderer,"Restyle castle to chapel theme");
    for(int i=0;i<materials.Length;i++)
    {
     var mat=i<source.Length?source[i]:materials[i];string semantic=mat?mat.name:"stone";if(mat){string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mat));if(sourceNames.TryGetValue(guid,out string named))semantic=named;}semantic=semantic.ToLowerInvariant();bool isGlass=semantic.Contains("glass")&&!renderer.name.ToLowerInvariant().Contains("mirror");
     materials[i]=isGlass?glass:semantic.Contains("roof")||semantic.Contains("slate")?slate:semantic.Contains("wood")||renderer.name.StartsWith("door",StringComparison.OrdinalIgnoreCase)||renderer.name.StartsWith("front door",StringComparison.OrdinalIgnoreCase)?oak:semantic.Contains("metal")||semantic.Contains("silver")||semantic.Contains("aluminum")?trim:stone;
     if(isGlass&&i<filter.sharedMesh.subMeshCount)windows+=WindowFrames(filter,i,bounds,frames,mullions);slots++;
    }
    renderer.sharedMaterials=materials;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
   }
   if(windows==0)throw new InvalidOperationException("No window surfaces identified; changes rolled back rather than highlighting wrong surfaces.");frames.Save("Pointed Stone Window Surrounds",trim,output+"/PointedWindowFrames.asset");mullions.Save("Window Mullions",metal,output+"/WindowMullions.asset");
   if(castle.localToWorldMatrix!=castleMatrix)throw new InvalidOperationException("Castle position changed; remodel rolled back.");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/GothicCastleRemodel-report.txt","Restyled the restored central castle to match chapel: original chapel dark masonry albedo/normal texture, slate roofing, oak doors, pointed stone window surrounds, antique mullions and subtle amber window emission. Highlighted "+windows+" connected window panes; restyled "+slots+" material slots. Removed "+removed.Count+" car-related objects and enclosed attached pieces: "+string.Join(", ",removed)+". Car colliders inactive. Castle transform and existing architecture retained; chapel, walls, towers, taverns, transport and terrain unaffected. Original model asset retained; scene backup available.\nBackup: "+backup);Debug.Log("Castle restyled to Gothic chapel theme; car removed and windows highlighted. Scene saved.");
  }
  static bool IsCar(string n){n=n.ToLowerInvariant();return n.StartsWith("car wheel")||n.StartsWith("car_half")||n.StartsWith("side mirror")||n.Contains("automobile");}
  static Material Stone(Shader shader,string name,Texture2D tex,Texture2D normal,Color color,Bounds bounds,string folder){var m=new Material(shader){name=name};m.SetTexture("_StoneTex",tex);m.SetTexture("_StoneNormal",normal);m.SetVector("_StoneScale",new Vector4(3.5f,3.5f,0,0));m.SetColor("_BaseColor",color);m.SetColor("_RoofColor",new Color(.12f,.14f,.18f));m.SetColor("_MortarColor",new Color(.07f,.065f,.055f));m.SetFloat("_AutoRoof",0);m.SetFloat("_ForceRoof",0);m.SetFloat("_RoofStartY",bounds.max.y);m.SetFloat("_BlockWidth",.55f);m.SetFloat("_BlockHeight",.28f);m.SetFloat("_Joint",.014f);m.doubleSidedGI=true;AssetDatabase.CreateAsset(m,folder+"/"+name.Replace(" ","")+".mat");return m;}
  static Material Lit(string name,Color color,float smooth,string folder){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Cull",0);m.doubleSidedGI=true;AssetDatabase.CreateAsset(m,folder+"/"+name.Replace(" ","")+".mat");return m;}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>(true);if(r.Length==0)throw new InvalidOperationException("Model has no geometry: "+go.name);var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static int WindowFrames(MeshFilter f,int submesh,Bounds castle,Geometry frames,Geometry mullions)
  {
   var mesh=f.sharedMesh;var vertices=mesh.vertices;var indices=mesh.GetTriangles(submesh);var parent=new Dictionary<int,int>();Func<int,int> root=null;root=i=>{if(!parent.ContainsKey(i))parent[i]=i;if(parent[i]!=i)parent[i]=root(parent[i]);return parent[i];};var shared=new Dictionary<Vector3Int,int>();
   foreach(int index in indices){int r=root(index);var v=vertices[index];var key=new Vector3Int(Mathf.RoundToInt(v.x*10000),Mathf.RoundToInt(v.y*10000),Mathf.RoundToInt(v.z*10000));if(shared.TryGetValue(key,out int previous))parent[root(index)]=root(previous);else shared[key]=index;}
   for(int i=0;i<indices.Length;i+=3){int r=root(indices[i]);parent[root(indices[i+1])]=r;parent[root(indices[i+2])]=r;}
   var groups=indices.Distinct().GroupBy(i=>root(i)).Select(g=>g.ToArray()).ToArray();int count=0;
   foreach(var group in groups)
   {
    if(group.Length<3)continue;var points=group.Select(i=>f.transform.TransformPoint(vertices[i])).ToArray();var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);if(b.size.y<.45f||b.size.y>6||b.center.y<castle.min.y+.5f)continue;
    Vector3 normal=Vector3.zero;int component=root(group[0]);for(int i=0;i<indices.Length;i+=3)if(root(indices[i])==component){var a=f.transform.TransformPoint(vertices[indices[i]]);var c=f.transform.TransformPoint(vertices[indices[i+1]]);var d=f.transform.TransformPoint(vertices[indices[i+2]]);normal=Vector3.Cross(c-a,d-a).normalized;if(normal.sqrMagnitude>.5f)break;}if(Mathf.Abs(normal.y)>.35f)continue;normal=Vector3.ProjectOnPlane(normal,Vector3.up).normalized;if(Vector3.Dot(normal,b.center-castle.center)<0)normal=-normal;var right=Vector3.Cross(Vector3.up,normal).normalized;float low=float.PositiveInfinity,high=float.NegativeInfinity;foreach(var p in points){float x=Vector3.Dot(p-b.center,right);low=Mathf.Min(low,x);high=Mathf.Max(high,x);}float width=high-low;if(width<.22f||width>4||points.Max(p=>Mathf.Abs(Vector3.Dot(p-b.center,normal)))>.18f)continue;
    Vector3 center=b.center+normal*.055f+right*(low+high)*.5f;float h=b.size.y;float thick=Mathf.Clamp(width*.075f,.055f,.16f);Vector3 bl=center-right*(width*.5f+thick*.5f)-Vector3.up*h*.5f,br=center+right*(width*.5f+thick*.5f)-Vector3.up*h*.5f;Vector3 tl=bl+Vector3.up*h,tr=br+Vector3.up*h;Vector3 tip=center+Vector3.up*(h*.5f+Mathf.Min(width*.5f,.7f));frames.Beam(bl,tl,normal,thick);frames.Beam(br,tr,normal,thick);frames.Beam(bl,br,normal,thick);frames.Beam(tl,tip,normal,thick);frames.Beam(tip,tr,normal,thick);mullions.Beam(center-Vector3.up*h*.48f,center+Vector3.up*h*.48f,normal,.035f);if(width>.65f)mullions.Beam(center-right*width*.46f,center+right*width*.46f,normal,.03f);count++;
   }return count;
  }
  class Geometry
  {
   Transform parent;List<Vector3> v=new List<Vector3>();List<int> triangles=new List<int>();public Geometry(Transform p){parent=p;}
   public void Beam(Vector3 a,Vector3 b,Vector3 normal,float width){var direction=(b-a).normalized;var side=Vector3.Cross(direction,normal).normalized*width*.5f;var depth=normal*width*.4f;int start=v.Count;foreach(var endpoint in new[]{a,b}){v.Add(parent.InverseTransformPoint(endpoint-side-depth));v.Add(parent.InverseTransformPoint(endpoint+side-depth));v.Add(parent.InverseTransformPoint(endpoint+side+depth));v.Add(parent.InverseTransformPoint(endpoint-side+depth));}int[] faces={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};foreach(int i in faces)triangles.Add(start+i);}
   public void Save(string name,Material material,string path){if(v.Count==0)return;var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;}
  }
 }
}
#endif
