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
 [InitializeOnLoad]public static class FitMountainToWall
 {
  const string Request="Assets/Editor/CQC/FitMountainToWall.request.txt";static double next;
  static FitMountainToWall(){EditorApplication.update+=Pending;}
  static void Pending(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;int group=Undo.GetCurrentGroup();try{Apply();AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FitMountainToWall-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+".failed");}}
  [MenuItem("Tools/Forest/Fit Mountain Rocks And Windmills To Fixed Wall")]
  public static void Apply()
  {
   var mountain=GameObject.Find("Castle Mountain");if(!mountain)throw new InvalidOperationException("Mountain missing.");var perimeter=mountain.transform.Find("Outer Castle Walls And Towers");var sections=perimeter?perimeter.Find("Perimeter Wall Sections"):null;if(!sections)throw new InvalidOperationException("Current walls missing.");
   var fixedTransforms=perimeter.GetComponentsInChildren<Transform>(true);var fixedMatrices=fixedTransforms.Select(t=>t.localToWorldMatrix).ToArray();
   var slope=mountain.transform.Find("Rocky Mountain Slopes");var filter=slope.GetComponent<MeshFilter>();var collider=slope.GetComponent<MeshCollider>();var floor=mountain.transform.Find("Cobblestone Castle Floors/Outer House Floor");if(!filter||!collider||!floor)throw new InvalidOperationException("Mountain surfaces missing.");
   Bounds oldFloor=BoundsOf(floor.gameObject);var oldWorld=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();float oldTop=oldWorld.Max(v=>v.y),bottom=oldWorld.Min(v=>v.y);var rim=oldWorld.Where(v=>v.y>oldTop-.1f).ToArray();if(rim.Length<12)throw new InvalidOperationException("Mountain upper rim not found.");
   Vector3 originalCenter=new Vector3((rim.Min(v=>v.x)+rim.Max(v=>v.x))*.5f,oldTop,(rim.Min(v=>v.z)+rim.Max(v=>v.z))*.5f);float oldRx=(rim.Max(v=>v.x)-rim.Min(v=>v.x))*.5f,oldRz=(rim.Max(v=>v.z)-rim.Min(v=>v.z))*.5f;
   var wallPoints=sections.Cast<Transform>().Select(t=>BoundsOf(t.gameObject).center).ToArray();if(wallPoints.Length<16)throw new InvalidOperationException("Not enough wall sections to fit mountain.");
   // Fit an axis-aligned ellipse to the CURRENT wall positions without moving a wall object.
   Vector3 seed=oldFloor.center;double[,] matrix=new double[4,5];foreach(var point in wallPoints){double x=(point.x-seed.x)/oldRx,z=(point.z-seed.z)/oldRz;double[] a={x*x,z*z,x,z};for(int i=0;i<4;i++){for(int j=0;j<4;j++)matrix[i,j]+=a[i]*a[j];matrix[i,4]+=a[i];}}
   var coefficients=Solve(matrix);double A=coefficients[0],B=coefficients[1],cx=-coefficients[2]/(2*A),cz=-coefficients[3]/(2*B),k=1+A*cx*cx+B*cz*cz;if(A<=0||B<=0)throw new InvalidOperationException("Current wall curve cannot be fitted.");
   Vector3 targetCenter=new Vector3(seed.x+(float)cx*oldRx,oldFloor.max.y-.02f,seed.z+(float)cz*oldRz);float rx=oldRx*(float)Math.Sqrt(k/A)+.7f,rz=oldRz*(float)Math.Sqrt(k/B)+.7f;
   if(rx<oldRx*.7f||rx>oldRx*1.3f||rz<oldRz*.7f||rz>oldRz*1.3f)throw new InvalidOperationException("Mountain fit exceeds expected rim adjustment.");
   var scene=SceneManager.GetActiveScene();string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),backup="Assets/ForestExpansion/Backups/MyForest-before-mountain-wall-fit-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Backup failed.");string folder="Assets/ForestExpansion/MountainWallFit-"+stamp;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Mountain Slopes Fitted To Current Wall";var vertices=mesh.vertices;
   for(int i=0;i<vertices.Length;i++){Vector3 point=oldWorld[i];float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(bottom,oldTop,point.y));float angle=Mathf.Atan2((point.z-originalCenter.z)/oldRz,(point.x-originalCenter.x)/oldRx);Vector3 previous=originalCenter+new Vector3(Mathf.Cos(angle)*oldRx,0,Mathf.Sin(angle)*oldRz),desired=targetCenter+new Vector3(Mathf.Cos(angle)*rx,0,Mathf.Sin(angle)*rz);point+=(desired-previous)*blend;vertices[i]=filter.transform.InverseTransformPoint(point);}
   mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Slope.asset");Undo.RecordObject(filter,"Fit mountain to fixed walls");Undo.RecordObject(collider,"Update fitted slope collision");filter.sharedMesh=mesh;collider.sharedMesh=mesh;
   // Trim only the outer edge of the mountain's caps; retain the interior vertices under buildings.
   int caps=0;foreach(var cap in new[]{mountain.transform.Find("Flat House Building Area"),floor})if(cap&&cap.GetComponent<MeshFilter>()){var f=cap.GetComponent<MeshFilter>();var copy=UnityEngine.Object.Instantiate(f.sharedMesh);var vv=copy.vertices;for(int i=0;i<vv.Length;i++){Vector3 q=f.transform.TransformPoint(vv[i]);float norm=Mathf.Sqrt(Mathf.Pow((q.x-originalCenter.x)/oldRx,2)+Mathf.Pow((q.z-originalCenter.z)/oldRz,2));float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1f,norm));float angle=Mathf.Atan2((q.z-originalCenter.z)/oldRz,(q.x-originalCenter.x)/oldRx);Vector3 from=originalCenter+new Vector3(oldRx*Mathf.Cos(angle),0,oldRz*Mathf.Sin(angle)),to=targetCenter+new Vector3(rx*Mathf.Cos(angle),0,rz*Mathf.Sin(angle));Vector3 change=to-from;change.y=0;q+=change*blend;vv[i]=f.transform.InverseTransformPoint(q);}copy.vertices=vv;copy.RecalculateNormals();copy.RecalculateTangents();copy.RecalculateBounds();AssetDatabase.CreateAsset(copy,folder+"/Rim-"+(caps++)+".asset");Undo.RecordObject(f,"Trim mountain outer rim");f.sharedMesh=copy;var c=cap.GetComponent<MeshCollider>();if(c){Undo.RecordObject(c,"Update rim collision");c.sharedMesh=copy;}}
   Physics.SyncTransforms();int rocks=0,mills=0;var rockGroup=mountain.transform.Find("Overlapping Scanned Rock Wall");if(rockGroup)foreach(Transform rock in rockGroup)
   {
    Bounds b=BoundsOf(rock.gameObject);Vector3 outward=Vector3.ProjectOnPlane(b.center-originalCenter,Vector3.up).normalized;Vector3 origin=new Vector3(targetCenter.x,b.center.y,targetCenter.z)+outward*Mathf.Max(rx,rz)*3;if(!collider.Raycast(new Ray(origin,-outward),out RaycastHit hit,Mathf.Max(rx,rz)*6))throw new InvalidOperationException("Cannot refit wall rock "+rock.name);
    Vector3 normal=hit.normal.normalized;if(Vector3.Dot(normal,outward)<0)normal=-normal;Vector3 uphill=Vector3.ProjectOnPlane(Vector3.up,normal).normalized;float roll=Vector3.SignedAngle(Vector3.ProjectOnPlane(Vector3.up,rock.forward),rock.up,rock.forward);Undo.RecordObject(rock,"Refit rock on adjusted slope");rock.rotation=Quaternion.LookRotation(normal,uphill)*Quaternion.Euler(0,0,roll);var part=rock.GetComponentInChildren<MeshFilter>();float depth=part.sharedMesh.bounds.size.z*rock.lossyScale.z;b=BoundsOf(rock.gameObject);rock.position+=hit.point-normal*depth*.28f-b.center;b=BoundsOf(rock.gameObject);if(b.max.y>targetCenter.y-.08f)rock.position-=Vector3.up*(b.max.y-targetCenter.y+.08f);rocks++;
   }
   var millGroup=mountain.transform.Find("Five Slope Windmills");if(millGroup)foreach(Transform mill in millGroup)
   {
    var body=mill.Find("Model Part 2");if(!body||!body.GetComponent<Renderer>())throw new InvalidOperationException("Windmill body missing.");Bounds b=body.GetComponent<Renderer>().bounds;Vector3 outward=Vector3.ProjectOnPlane(b.center-originalCenter,Vector3.up).normalized;Vector3 origin=new Vector3(targetCenter.x,b.min.y,targetCenter.z)+outward*Mathf.Max(rx,rz)*3;if(!collider.Raycast(new Ray(origin,-outward),out RaycastHit hit,Mathf.Max(rx,rz)*6))throw new InvalidOperationException("Cannot refit windmill "+mill.name);float half=Mathf.Abs(outward.x)*b.extents.x+Mathf.Abs(outward.z)*b.extents.z;Vector3 target=hit.point+outward*half*.25f;Undo.RecordObject(mill,"Refit windmill to mountain wall");mill.position+=new Vector3(target.x-b.center.x,hit.point.y-b.min.y,target.z-b.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(mill);mills++;
   }
   for(int i=0;i<fixedTransforms.Length;i++)if(fixedTransforms[i].localToWorldMatrix!=fixedMatrices[i])throw new InvalidOperationException("Wall or tower moved; changes rolled back.");
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save failed.");SceneView.RepaintAll();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FitMountainToWall-report.txt","Mountain upper rim fitted beneath the current wall ellipse. Old rim radii "+oldRx.ToString("F2")+", "+oldRz.ToString("F2")+"m; new "+rx.ToString("F2")+", "+rz.ToString("F2")+"m. Lower mountain base retained, "+caps+" outer cap rims trimmed without moving interior buildings; "+rocks+" rocks refitted and "+mills+" windmills repositioned against surface. All "+fixedTransforms.Length+" wall/tower transforms verified unchanged. Transportation routes retained.\nBackup: "+backup);Debug.Log("Mountain, rocks and windmills fitted to existing wall; wall and towers unchanged. Scene saved.");
  }
  static Bounds BoundsOf(GameObject go){var r=go.GetComponentsInChildren<Renderer>();if(r.Length==0)throw new InvalidOperationException("Mesh renderer missing: "+go.name);var b=r[0].bounds;foreach(var part in r)b.Encapsulate(part.bounds);return b;}
  static double[] Solve(double[,] m){for(int i=0;i<4;i++){int best=i;for(int j=i+1;j<4;j++)if(Math.Abs(m[j,i])>Math.Abs(m[best,i]))best=j;for(int k=i;k<5;k++){double tmp=m[i,k];m[i,k]=m[best,k];m[best,k]=tmp;}double divisor=m[i,i];if(Math.Abs(divisor)<1e-12)throw new InvalidOperationException("Wall fit singular.");for(int k=i;k<5;k++)m[i,k]/=divisor;for(int j=0;j<4;j++)if(j!=i){double f=m[j,i];for(int k=i;k<5;k++)m[j,k]-=f*m[i,k];}}return new[]{m[0,4],m[1,4],m[2,4],m[3,4]};}
 }
}
#endif
