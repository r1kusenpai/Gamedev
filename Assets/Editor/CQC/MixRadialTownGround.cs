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
 [InitializeOnLoad] public static class MixRadialTownGround
 {
  const string Request="Assets/Editor/CQC/MixRadialTownGround-persist-v4.request.txt",Marker="Verified Mixed Green And Brown Town Ground";
  static double next;
  [Serializable] sealed class Layout {public Vector2 center;public float outerX,outerZ;}
  sealed class Road {public Vector2 A,B;public float Half;}
  static MixRadialTownGround(){EditorApplication.update+=Poll;}
  static void Poll(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Mix grass and brown dirt in outside town");try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/MixRadialTownGround-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}finally{EditorUtility.ClearProgressBar();}}
  [MenuItem("Tools/City/Mix Green Grass And Brown Dirt In New Outside Town")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode before painting the ground.");
   var scene=SceneManager.GetActiveScene();var city=GameObject.Find("Lower Castle City");if(!city)throw new InvalidOperationException("Outside town missing.");if(city.transform.Find(Marker))return;
   var roads=city.transform.Find("Winding Roads and Market Square");var surface=roads?roads.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f=>f.name=="Joined Radial Cobblestone Surface"):null;if(!surface)throw new InvalidOperationException("Apply the new radial town before mixing its ground.");
   string layoutPath=Path.GetDirectoryName(AssetDatabase.GetAssetPath(surface.sharedMesh)).Replace('\\','/')+"/Layout.json";
   var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(layoutPath));if(layout.outerX<50||layout.outerZ<50)throw new InvalidOperationException("Outside town footprint missing.");
   var layersFound=AssetDatabase.FindAssets("t:TerrainLayer").Select(g=>AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(g))).ToArray();var grass=layersFound.FirstOrDefault(l=>l.name=="UNS_Grass");var dirt=layersFound.FirstOrDefault(l=>l.name=="UNS_Dirt");if(!grass||!dirt||!grass.diffuseTexture||!dirt.diffuseTexture)throw new InvalidOperationException("Existing grass and brown dirt textures are required.");
   var segments=RoadSegments(roads);
   var polygon=Enumerable.Range(0,8).Select(i=>layout.center+new Vector2(Mathf.Cos(i*Mathf.PI/4)*layout.outerX,Mathf.Sin(i*Mathf.PI/4)*layout.outerZ)).ToArray();
   var terrains=Terrain.activeTerrains.Where(t=>t.gameObject.scene==scene).Where(t=>{var p=t.transform.position;var s=t.terrainData.size;return p.x<layout.center.x+layout.outerX+34&&p.x+s.x>layout.center.x-layout.outerX-34&&p.z<layout.center.y+layout.outerZ+34&&p.z+s.z>layout.center.y-layout.outerZ-34;}).ToArray();if(terrains.Length==0)throw new InvalidOperationException("No ground tiles under the new outside town.");
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),folder="Assets/CityExpansion/MixedOutsideGround-"+stamp,backup="Assets/ForestExpansion/Backups/MyForest-before-mixed-outside-ground-"+stamp+".unity";if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Ground backup failed.");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");if(!shader)throw new InvalidOperationException("Terrain surface shader missing.");var material=new Material(shader){name="Verified Natural Grass And Brown Dirt"};material.SetFloat("_EnableHeightBlend",0);AssetDatabase.CreateAsset(material,folder+"/NaturalTerrain.mat");
   int count=0,texels=0;double greenTotal=0,brownTotal=0;float maximumError=0;var diagnostics=new System.Text.StringBuilder();
   foreach(var terrain in terrains)
   {
    EditorUtility.DisplayProgressBar("Outside town ground","Blending grass and dirt in natural patches",(float)count/terrains.Length);
    var source=terrain.terrainData;var layers=source.terrainLayers.ToList();if(!layers.Contains(grass))layers.Add(grass);if(!layers.Contains(dirt))layers.Add(dirt);int gi=layers.IndexOf(grass),di=layers.IndexOf(dirt),width=source.alphamapWidth,height=source.alphamapHeight;var old=source.GetAlphamaps(0,0,width,height);var alpha=new float[height,width,layers.Count];var origin=terrain.transform.position;var size=source.size;int changed=0;
    double previousBrown=0,previousGreen=0;int paintedSamples=0;
    for(int z=0;z<height;z++)for(int x=0;x<width;x++)
    {
     var point=new Vector2(origin.x+x*size.x/Mathf.Max(1,width-1),origin.z+z*size.z/Mathf.Max(1,height-1));float signed=PolygonDistance(point,polygon);float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((32-signed)/10));
     float brown=0;if(blend>0){brown=BrownWeight(point,segments);changed++;if(blend>.99f){previousGreen+=gi<old.GetLength(2)?old[z,x,gi]:0;previousBrown+=di<old.GetLength(2)?old[z,x,di]:0;paintedSamples++;}}
     TownGroundWeights.Blend(old,alpha,z,x,gi,di,brown,blend);
    }
    if(changed==0)continue;
    // Allocate independent native control textures. Persist the asset BEFORE
    // painting because CreateAsset can recreate/reset its internal splatmaps.
    var copy=IndependentTerrain(source,terrain.name+" Verified Mixed Grass And Brown Dirt");copy.terrainLayers=layers.ToArray();string dataPath=folder+"/Terrain-"+terrain.GetEntityId()+".asset";AssetDatabase.CreateAsset(copy,dataPath);
    copy.SetAlphamaps(0,0,alpha);copy.SetBaseMapDirty();EditorUtility.SetDirty(copy);foreach(var control in copy.alphamapTextures)EditorUtility.SetDirty(control);AssetDatabase.SaveAssets();
    AssetDatabase.ImportAsset(dataPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);copy=AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);var verified=copy.GetAlphamaps(0,0,width,height);
    for(int z=0;z<height;z++)for(int x=0;x<width;x++)
    {
     for(int l=0;l<layers.Count;l++){float error=Mathf.Abs(verified[z,x,l]-alpha[z,x,l]);maximumError=Mathf.Max(maximumError,error);if(error>.02f)throw new InvalidOperationException("Saved ground paint did not persist on "+terrain.name+" at "+x+","+z+", layer "+l+": expected "+alpha[z,x,l]+", saved "+verified[z,x,l]);}
     var point=new Vector2(origin.x+x*size.x/Mathf.Max(1,width-1),origin.z+z*size.z/Mathf.Max(1,height-1));if(PolygonDistance(point,polygon)<22){greenTotal+=verified[z,x,gi];brownTotal+=verified[z,x,di];texels++;}
    }
    if(copy.heightmapResolution!=source.heightmapResolution||copy.size!=source.size||copy.treeInstanceCount!=source.treeInstanceCount)throw new InvalidOperationException("Ground geometry or vegetation changed unexpectedly.");
    diagnostics.AppendLine(terrain.name+": previous green/brown "+(previousGreen/Math.Max(1,paintedSamples)).ToString("F3")+" / "+(previousBrown/Math.Max(1,paintedSamples)).ToString("F3")+"; saved control maps verified after import. Previous material: "+(terrain.materialTemplate?AssetDatabase.GetAssetPath(terrain.materialTemplate):"default"));
    Undo.RecordObject(terrain,"Paint new outside town grass and dirt mix");terrain.terrainData=copy;terrain.materialTemplate=material;var collider=terrain.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Preserve copied terrain collision heights");collider.terrainData=copy;}terrain.Flush();EditorUtility.SetDirty(terrain);count++;
   }
   if(count==0||texels==0)throw new InvalidOperationException("No outside town ground was painted.");if(brownTotal/texels<.15||greenTotal/texels<.25)throw new InvalidOperationException("Saved ground is missing the expected green/brown contrast.");var marker=new GameObject(Marker);Undo.RegisterCreatedObjectUndo(marker,"Remember verified mixed outside ground");marker.transform.SetParent(city.transform,false);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save the mixed town ground.");
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/MixRadialTownGround-report.txt","Verified saved green grass and brown dirt around the redesigned outside town. "+count+" independent terrain tiles; original UNS_Grass and UNS_Dirt textures, irregular broad patches and finer mottling, extra worn soil beside roads, soft transition into surrounding forest. ACTUAL SAVED MAP averages inside painted area: green "+(100*greenTotal/texels).ToString("F1")+"%, brown "+(100*brownTotal/texels).ToString("F1")+"%. Maximum saved-vs-requested layer error "+maximumError.ToString("F5")+" after saving and forced reimport. Asset created before painting; independent native control maps and neutral URP terrain material. Ground heights, terrain trees/details, buildings, roads, castle and NPC data retained.\nBackup: "+backup+"\nAssets: "+folder+"\nBefore/after diagnostics:\n"+diagnostics+"\nScene saved.");Debug.Log("Outside town green/brown ground saved and verified from persisted control maps.");SceneView.RepaintAll();
  }
  static TerrainData IndependentTerrain(TerrainData current,string name)
  {
   var data=new TerrainData{name=name,heightmapResolution=current.heightmapResolution,alphamapResolution=current.alphamapResolution,baseMapResolution=current.baseMapResolution,size=current.size};
   data.SetHeights(0,0,current.GetHeights(0,0,current.heightmapResolution,current.heightmapResolution));data.treePrototypes=current.treePrototypes;data.treeInstances=current.treeInstances;data.detailPrototypes=current.detailPrototypes;
   if(current.detailResolution>0){data.SetDetailResolution(current.detailResolution,current.detailResolutionPerPatch);for(int l=0;l<current.detailPrototypes.Length;l++)data.SetDetailLayer(0,0,l,current.GetDetailLayer(0,0,current.detailWidth,current.detailHeight,l));}
   data.wavingGrassAmount=current.wavingGrassAmount;data.wavingGrassSpeed=current.wavingGrassSpeed;data.wavingGrassStrength=current.wavingGrassStrength;data.wavingGrassTint=current.wavingGrassTint;data.enableHolesTextureCompression=current.enableHolesTextureCompression;data.SetHoles(0,0,current.GetHoles(0,0,current.holesResolution,current.holesResolution));return data;
  }
  static List<Road> RoadSegments(Transform roads)
  {
   var result=new List<Road>();foreach(var f in roads.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Neighborhood Loop")||f.name.StartsWith("Market Street")||f.name=="Funicular Approach Road"))
   {
    var v=f.sharedMesh.vertices;var points=new List<Vector2>();for(int i=0;i+1<v.Length;i+=2){var p=f.transform.TransformPoint((v[i]+v[i+1])*.5f);points.Add(new Vector2(p.x,p.z));}if(points.Count<2)continue;float half=Vector3.Distance(f.transform.TransformPoint(v[0]),f.transform.TransformPoint(v[1]))*.5f;var anchor=points[0];
    for(int i=1;i<points.Count;i++)if(i==points.Count-1||Vector2.Dot((points[i]-points[i-1]).normalized,(points[i+1]-points[i]).normalized)<.9999f){result.Add(new Road{A=anchor,B=points[i],Half=half});anchor=points[i];}
   }return result;
  }
  static float BrownWeight(Vector2 p,List<Road> roads){float broad=Mathf.PerlinNoise((p.x+853)*.036f,(p.y+317)*.036f),fine=Mathf.PerlinNoise((p.x+127)*.12f,(p.y+911)*.12f);float noise=broad*.78f+fine*.22f;float patches=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.37f,.69f,noise));float brown=Mathf.Lerp(.08f,.83f,patches);float distance=10000;foreach(var r in roads)distance=Mathf.Min(distance,SegmentDistance(p,r.A,r.B)-r.Half);float wear=Mathf.Clamp01((2.8f-distance)/3.2f);return Mathf.Clamp01(Mathf.Lerp(brown,Mathf.Max(brown,.5f+fine*.18f),wear*.7f));}
  static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){var v=b-a;return Vector2.Distance(p,a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/Mathf.Max(.0001f,v.sqrMagnitude)));}
  static float PolygonDistance(Vector2 p,Vector2[] polygon){bool inside=false;float distance=float.MaxValue;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++){var a=polygon[i];var b=polygon[j];distance=Mathf.Min(distance,SegmentDistance(p,a,b));if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;}return inside?-distance:distance;}
 }
}
#endif
