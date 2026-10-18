#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools {
 [InitializeOnLoad] public static class SurveyRadialTown {
  [Serializable] public class Item { public string name,path; public Vector3 position,scale; public Quaternion rotation; public Bounds world,local; }
  [Serializable] public class Survey { public List<Item> houses=new List<Item>(),obstacles=new List<Item>(); public List<Vector3> ground=new List<Vector3>(); }
  static double next;
  static SurveyRadialTown(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+3;if(File.Exists("Logs/RadialTown-survey.json")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   try{var city=GameObject.Find("Lower Castle City");if(!city)return;var s=new Survey();var homes=city.transform.Find("House Neighborhoods");foreach(Transform t in homes)if(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))s.houses.Add(Read(t));
   foreach(var t in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>())){if(t.IsChildOf(homes)||t.IsChildOf(city.transform.Find("Winding Roads and Market Square")))continue;if(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&(t.name.ToLowerInvariant().Contains("mountain")||t.name.ToLowerInvariant().Contains("cliff")))s.obstacles.Add(Read(t));}
   foreach(string n in new[]{"Gothic Town Center Tower","Three Outside Castle Blacksmiths"}){var t=city.transform.Find(n);if(t)s.obstacles.Add(Read(t));}
   for(int x=-340;x<=50;x+=5)for(int z=0;z<=250;z+=5)foreach(var t in Terrain.activeTerrains){var o=t.transform.position;var size=t.terrainData.size;if(x>=o.x&&x<=o.x+size.x&&z>=o.z&&z<=o.z+size.z){s.ground.Add(new Vector3(x,t.SampleHeight(new Vector3(x,0,z))+o.y,z));break;}}
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RadialTown-survey.json",JsonUtility.ToJson(s,true));EditorApplication.update-=Poll;
   }catch(Exception e){File.WriteAllText("Logs/RadialTown-survey-error.txt",e.ToString());EditorApplication.update-=Poll;}}
  static Item Read(Transform t){var i=new Item{name=t.name,path=AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)),position=t.position,scale=t.lossyScale,rotation=t.rotation};bool first=true;foreach(var r in t.GetComponentsInChildren<Renderer>(true)){if(first)i.world=r.bounds;else i.world.Encapsulate(r.bounds);var b=r.localBounds;for(int k=0;k<8;k++){var p=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));p=t.InverseTransformPoint(r.transform.TransformPoint(p));if(first&&k==0)i.local=new Bounds(p,Vector3.zero);else i.local.Encapsulate(p);}first=false;}return i;}
 }
}
#endif
