using UnityEngine;
namespace WildernessCombat {
// Measure imported mesh geometry after parenting, including the hand's inherited scale.
public static class CyclopsSizing {
 public static Bounds GeometryBounds(GameObject root){bool found=false;Bounds bounds=default;foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)){Mesh mesh=null;if(renderer is SkinnedMeshRenderer skin)mesh=skin.sharedMesh;else{var filter=renderer.GetComponent<MeshFilter>();if(filter)mesh=filter.sharedMesh;}if(!mesh)continue;var b=mesh.bounds;for(int i=0;i<8;i++){var p=renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);}}return bounds;}
 public static void FitHeight(GameObject root,float height){var b=GeometryBounds(root);if(b.size.y>.001f)root.transform.localScale*=Mathf.Clamp(height,1f,3f)/b.size.y;}
 public static float LongestAxisLength(GameObject root){bool found=false;Bounds bounds=default;foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;for(int i=0;i<8;i++){var p=root.transform.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);}}return Mathf.Max(root.transform.TransformVector(Vector3.right*bounds.size.x).magnitude,root.transform.TransformVector(Vector3.up*bounds.size.y).magnitude,root.transform.TransformVector(Vector3.forward*bounds.size.z).magnitude);}
 public static void FitLength(GameObject root,float length){float span=LongestAxisLength(root);if(span>.001f)root.transform.localScale*=Mathf.Clamp(length,.2f,2.2f)/span;}
 public static GameObject AttachClub(WildernessEnemySettings settings,Transform visual,Transform hand){var animator=visual.GetComponentInChildren<Animator>();var forearm=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);Vector3 fingers=(hand.position-forearm.position).normalized;
 var socket=new GameObject("Cyclops club palm grip").transform;socket.SetParent(hand,false);
 // Hand bone starts at the wrist. The grip belongs in the palm, along the fingers.
 socket.position=hand.position+fingers*(settings.CyclopsHeight*.04f)+visual.forward*(settings.CyclopsHeight*.005f);
 Vector3 shaft=Vector3.Cross(visual.forward,fingers).normalized;if(Vector3.Dot(shaft,visual.up)<0)shaft=-shaft;
 socket.rotation=Quaternion.LookRotation(visual.forward,shaft);
 var club=Object.Instantiate(settings.CyclopsClub,socket);club.name="Spiked club";club.transform.localPosition=Vector3.zero;club.transform.localRotation=Quaternion.Euler(90f,0f,0f);club.transform.localScale=Vector3.one;FitLength(club,settings.CyclopsClubLength);return club;}
}
}


