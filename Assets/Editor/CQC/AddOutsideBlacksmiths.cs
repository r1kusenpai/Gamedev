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
 [InitializeOnLoad]public static class AddOutsideBlacksmiths
 {
  const string Request="Assets/Editor/CQC/AddOutsideBlacksmiths.request.txt";static double next;
  static AddOutsideBlacksmiths(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AddOutsideBlacksmiths-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}}
  [MenuItem("Tools/City/Add Three Blacksmiths Outside Castle")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");var city=GameObject.Find("Lower Castle City");if(!mountain||!city)throw new InvalidOperationException("Castle and lower city required.");if(city.transform.Find("Three Outside Castle Blacksmiths"))return;
   var roads=city.transform.Find("Winding Roads and Market Square");var houses=city.transform.Find("House Neighborhoods");if(!roads||!houses)throw new InvalidOperationException("City roads or houses missing.");Physics.SyncTransforms();
   var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor").GetComponent<Renderer>().bounds;
   var obstacles=new List<Bounds>();foreach(Transform t in houses)if(t.gameObject.activeInHierarchy&&t.GetComponentsInChildren<Renderer>().Length>0)obstacles.Add(BoundsOf(t.gameObject));var details=city.transform.Find("Market and Street Details");if(details)foreach(Transform t in details)if(t.GetComponentsInChildren<Renderer>().Length>0)obstacles.Add(BoundsOf(t.gameObject));
   var segments=new List<Vector3[]>();var candidates=new List<Vector3[]>();
   foreach(var filter in roads.GetComponentsInChildren<MeshFilter>())
   {
    if(filter.name=="Central Market Square")continue;var vertices=filter.sharedMesh.vertices;
    for(int i=0;i+3<vertices.Length;i+=2)
    {
     var a=filter.transform.TransformPoint(vertices[i]);var b=filter.transform.TransformPoint(vertices[i+1]);var c=filter.transform.TransformPoint(vertices[i+2]);var d=filter.transform.TransformPoint(vertices[i+3]);var p=(a+b)*.5f;var q=(c+d)*.5f;if(Vector3.Distance(p,q)>12)continue;float half=Vector3.Distance(a,b)*.5f;segments.Add(new[]{p,q,new Vector3(half,0,0)});
     if(i%4!=0)continue;var tangent=Vector3.ProjectOnPlane(q-p,Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,tangent);foreach(int sign in new[]{-1,1})foreach(float setback in new[]{8f,11f,14f,18f,23f,29f,35f})candidates.Add(new[]{p+side*sign*(half+setback),-side*sign});
    }
   }
   if(candidates.Count==0)throw new InvalidOperationException("No city street lots found.");
   var scene=SceneManager.GetActiveScene();string backup="Assets/ForestExpansion/Backups/MyForest-before-outside-blacksmiths-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");
   var original=mountain.GetComponentsInChildren<Transform>(true).Concat(city.GetComponentsInChildren<Transform>(true)).ToArray();var matrices=original.Select(t=>t.localToWorldMatrix).ToArray();
   var prefab=AddMarkedTaverns.Build("UpperBlacksmithHouse");var root=new GameObject("Three Outside Castle Blacksmiths");root.transform.SetParent(city.transform,false);Undo.RegisterCreatedObjectUndo(root,"Add three outside blacksmiths");var placed=new List<Bounds>();var roadPolygons=new List<Vector2[]>();foreach(var filter in roads.GetComponentsInChildren<MeshFilter>()){var v=filter.sharedMesh.vertices;var ids=filter.sharedMesh.triangles;for(int i=0;i<ids.Length;i+=3)roadPolygons.Add(new[]{Flat(filter.transform.TransformPoint(v[ids[i]])),Flat(filter.transform.TransformPoint(v[ids[i+1]])),Flat(filter.transform.TransformPoint(v[ids[i+2]]))});}var diagnostics=new int[6];var positions=new List<string>();var market=roads.Find("Central Market Square");var townCenter=market?BoundsOf(market.gameObject).center:BoundsOf(city).center;
   for(int index=0;index<3;index++)
   {
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Outside Castle Blacksmith "+(index+1).ToString("00");go.transform.SetParent(root.transform,true);Vector3 nativeScale=go.transform.localScale;float nativeHeight=BoundsOf(go).size.y;
    Vector3 desired=townCenter+new Vector3((index-1)*70,0,25);bool found=false;Vector3 chosen=Vector3.zero;Quaternion rotation=Quaternion.identity;float score=float.PositiveInfinity;
    foreach(float height in new[]{8f,6.5f,5.5f})
    {go.transform.localScale=nativeScale*(height/nativeHeight);
    foreach(var lot in candidates)
    {
     Vector3 target=lot[0];if(Mathf.Pow((target.x-floor.center.x)/(floor.extents.x+15),2)+Mathf.Pow((target.z-floor.center.z)/(floor.extents.z+15),2)<1){diagnostics[0]++;continue;}
     go.transform.rotation=Quaternion.LookRotation(lot[1]);float ground=Ground(target.x,target.z);if(float.IsNaN(ground)){diagnostics[1]++;continue;}Place(go,target,ground);var b=BoundsOf(go);
     if(obstacles.Any(o=>Overlap(b,o,1.5f))||placed.Any(o=>Overlap(b,o,10))){diagnostics[2]++;continue;}var footprint=Footprint(go);if(roadPolygons.Any(poly=>Intersects(footprint,poly,1.2f))){diagnostics[3]++;continue;}
     float low=ground,high=ground;bool supported=true;for(int k=0;k<4;k++){float h=Ground(k%2==0?b.min.x:b.max.x,k<2?b.min.z:b.max.z);if(float.IsNaN(h)){supported=false;break;}low=Mathf.Min(low,h);high=Mathf.Max(high,h);}if(!supported||high-low>.6f){diagnostics[4]++;continue;}
     Place(go,target,high);b=BoundsOf(go);Physics.SyncTransforms();bool blocked=Physics.OverlapBox(b.center+Vector3.up*.1f,new Vector3(b.extents.x+.5f,Mathf.Max(.5f,b.extents.y-.4f),b.extents.z+.5f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>!c.transform.IsChildOf(root.transform)&&!(c is TerrainCollider)&&!c.transform.IsChildOf(roads)&&c.bounds.max.y>high+1f);if(blocked){diagnostics[5]++;continue;}
     float cost=(new Vector2(target.x-desired.x,target.z-desired.z)).sqrMagnitude;if(cost<score){score=cost;chosen=new Vector3(target.x,high,target.z);rotation=go.transform.rotation;found=true;}
    }
    if(found)break;
    }
    if(!found)throw new InvalidOperationException("No free blacksmith lot "+(index+1)+". Rejected candidates [castle, terrain, buildings, road, slope, colliders]: "+string.Join(",",diagnostics)+". Existing scene retained.");go.transform.rotation=rotation;Place(go,chosen,chosen.y);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);placed.Add(BoundsOf(go));positions.Add(go.name+": "+BoundsOf(go).center+", height "+BoundsOf(go).size.y.ToString("F2")+"m");
   }
   for(int i=0;i<original.Length;i++)if(original[i].localToWorldMatrix!=matrices[i])throw new InvalidOperationException("Existing structure moved; changes rolled back.");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");if(File.Exists("Logs/AddOutsideBlacksmiths-error.txt"))File.Delete("Logs/AddOutsideBlacksmiths-error.txt");File.WriteAllText("Logs/AddOutsideBlacksmiths-report.txt","Added exactly three blacksmith buildings outside castle in lower city, 8m tall where space permits, with 6.5m/5.5m fallback, using supplied blacksmith model and textures. Street-facing lots, road separation, building separation, terrain support and collider clearance checked. Existing castle, city houses, roads and terrain preserved; no added ground platforms.\n"+string.Join("\n",positions)+"\nBackup: "+backup);Debug.Log("Three blacksmiths added outside castle. Scene saved.");
  }
  static Vector2 Flat(Vector3 v){return new Vector2(v.x,v.z);}
  static Vector2[] Footprint(GameObject go){Bounds local=new Bounds();bool first=true;foreach(var f in go.GetComponentsInChildren<MeshFilter>()){var b=f.sharedMesh.bounds;for(int k=0;k<8;k++){var point=go.transform.InverseTransformPoint(f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));if(first){local=new Bounds(point,Vector3.zero);first=false;}else local.Encapsulate(point);}}return new[]{Flat(go.transform.TransformPoint(new Vector3(local.min.x,0,local.min.z))),Flat(go.transform.TransformPoint(new Vector3(local.max.x,0,local.min.z))),Flat(go.transform.TransformPoint(new Vector3(local.max.x,0,local.max.z))),Flat(go.transform.TransformPoint(new Vector3(local.min.x,0,local.max.z)))};}
  static bool Intersects(Vector2[] a,Vector2[] b,float margin){foreach(var polygon in new[]{a,b})for(int i=0;i<polygon.Length;i++){var edge=polygon[(i+1)%polygon.Length]-polygon[i];if(edge.sqrMagnitude<.00001f)continue;var axis=new Vector2(-edge.y,edge.x).normalized;float amin=float.PositiveInfinity,amax=float.NegativeInfinity,bmin=amin,bmax=amax;foreach(var p in a){float v=Vector2.Dot(axis,p);amin=Mathf.Min(amin,v);amax=Mathf.Max(amax,v);}foreach(var p in b){float v=Vector2.Dot(axis,p);bmin=Mathf.Min(bmin,v);bmax=Mathf.Max(bmax,v);}if(amax+margin<bmin||bmax+margin<amin)return false;}return true;}
  static float Ground(float x,float z){foreach(var t in Terrain.activeTerrains){var p=t.transform.position;var size=t.terrainData.size;if(x>=p.x&&x<=p.x+size.x&&z>=p.z&&z<=p.z+size.z)return p.y+t.terrainData.GetInterpolatedHeight((x-p.x)/size.x,(z-p.z)/size.z);}return float.NaN;}
  static float Distance(Vector3 p,Vector3 a,Vector3 b){var d=new Vector2(b.x-a.x,b.z-a.z);var q=new Vector2(p.x-a.x,p.z-a.z);return(q-d*Mathf.Clamp01(Vector2.Dot(q,d)/Mathf.Max(.001f,d.sqrMagnitude))).magnitude;}
  static bool Overlap(Bounds a,Bounds b,float margin){return Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+margin&&Mathf.Abs(a.center.z-b.center.z)<a.extents.z+b.extents.z+margin;}
  static void Place(GameObject go,Vector3 target,float y){var b=BoundsOf(go);go.transform.position+=new Vector3(target.x-b.center.x,y+.02f-b.min.y,target.z-b.center.z);}
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Visible model missing.");var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
 }
}
#endif
