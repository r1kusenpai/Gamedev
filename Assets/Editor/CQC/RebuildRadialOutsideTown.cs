#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace StarterAssets.CQCEditorTools
{
 [InitializeOnLoad] public static class RebuildRadialOutsideTown
 {
  const string Request="Assets/Editor/CQC/RebuildRadialOutsideTown-v3.request.txt";
  const string Marker="Radial Star And Octagonal Blocks";
  static double next;
  static RebuildRadialOutsideTown(){EditorApplication.update+=Poll;}
  static void Poll()
  {
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
   if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="MyForest")return;
   Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Replace outside town with radial star blocks");
   try{Apply();Undo.CollapseUndoOperations(group);AssetDatabase.DeleteAsset(Request);}
   catch(Exception e){Undo.RevertAllDownToGroup(group);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RebuildRadialOutsideTown-error.txt",e.ToString());Debug.LogException(e);AssetDatabase.MoveAsset(Request,Request+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".failed");}
   finally{EditorUtility.ClearProgressBar();}
  }
  sealed class Road {public string Name;public float Width;public List<Vector2> Points=new List<Vector2>();}
  sealed class House {public Transform Source;public Bounds Local;public Vector3 Scale;public float Width,Depth,Height;public bool Smith;}
  sealed class Lot {public House House;public Vector2 Center,Right,Front,Door,RoadPoint;public float Y;public Vector2[] Corners;public string Street;public int Edge,Side;}
  sealed class Plan {public Vector2 Center;public float X,Z;public List<Road> Roads=new List<Road>();public List<Lot> Lots=new List<Lot>();public float Cost;}
  sealed class Obstacle {public Bounds Bounds;public string Name;public bool HousesOnly;public Collider Collider;}
  struct Segment {public Vector2 A,B;public float Half;}
  sealed class Field
  {
   public Vector2 Center;public float Plaza=15;public List<Segment> Segments=new List<Segment>();
   public void Add(Road road){for(int i=1;i<road.Points.Count;i++)Segments.Add(new Segment{A=road.Points[i-1],B=road.Points[i],Half=road.Width*.5f});}
   public float Value(Vector2 p){float d=(p-Center).magnitude-Plaza;foreach(var s in Segments){float q=Distance(p,s.A,s.B)-s.Half;float h=Mathf.Max(0,.7f-Mathf.Abs(d-q))/.7f;d=Mathf.Min(d,q)-h*h*.175f;}return d;}
  }
  sealed class Geometry
  {
   public List<Vector3> V=new List<Vector3>();public List<Vector2> UV=new List<Vector2>();public List<int> T=new List<int>();
   public void Tri(Vector3 a,Vector3 b,Vector3 c){var normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<1e-12f)return;int n=V.Count;V.AddRange(new[]{a,b,c});Func<Vector3,Vector2> uv=p=>Mathf.Abs(normal.normalized.y)>.5f?new Vector2(p.x/3,p.z/3):Mathf.Abs(normal.x)>Mathf.Abs(normal.z)?new Vector2(p.z/3,p.y/3):new Vector2(p.x/3,p.y/3);UV.AddRange(new[]{uv(a),uv(b),uv(c)});T.AddRange(new[]{n,n+1,n+2});}
   public Mesh Make(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(V);m.SetUVs(0,UV);m.SetTriangles(T,0);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;}
  }
  static List<Obstacle> obstacles;static Terrain[] terrains;
  [MenuItem("Tools/City/Replace Outside Town With Radial Star And Octagonal Blocks")]
  public static void Apply()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode before rebuilding the town.");
   var scene=SceneManager.GetActiveScene();if(scene.name!="MyForest")throw new InvalidOperationException("MyForest must be open.");
   var city=GameObject.Find("Lower Castle City");if(!city)throw new InvalidOperationException("Outside town missing.");
   var oldRoads=city.transform.Find("Winding Roads and Market Square");if(oldRoads&&oldRoads.Find(Marker))return;
   var oldHomes=city.transform.Find("House Neighborhoods");if(!oldRoads||!oldHomes)throw new InvalidOperationException("Outside town roads or taverns missing.");
   Physics.SyncTransforms();terrains=Terrain.activeTerrains;obstacles=CollectObstacles(city.transform);
   var houses=new List<House>();foreach(Transform t in oldHomes)if(t.gameObject.activeInHierarchy&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))houses.Add(ReadHouse(t,false));
   var oldSmiths=city.transform.Find("Three Outside Castle Blacksmiths");if(oldSmiths)foreach(Transform t in oldSmiths)if(t.gameObject.activeInHierarchy&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))houses.Add(ReadHouse(t,true));
   if(houses.Count(h=>!h.Smith)!=50)throw new InvalidOperationException("Expected the 50 existing outside taverns; found "+houses.Count(h=>!h.Smith)+". Nothing changed.");
   houses=houses.OrderByDescending(h=>h.Width*h.Depth).ToList();
   var lift=UnityEngine.Object.FindAnyObjectByType<StarterAssets.CastleFunicular>();if(!lift)throw new InvalidOperationException("Funicular missing.");
   var steps=lift.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Forest Access Step ")).OrderBy(c=>c.bounds.max.y).ToArray();if(steps.Length==0)throw new InvalidOperationException("Funicular ground stairs missing.");
   var station=Flat(steps[0].bounds.center);
   Plan best=null;var notes=new List<string>();
   // Search intact geometric layouts, never cut arbitrary gaps through a ring.
   // The live ground survey fits all 53 existing buildings on this compact
   // footprint and clears the western mountains and the southern cliff props.
   var configs=new[]{new Vector4(-110,100,145,88),new Vector4(-100,100,150,92),new Vector4(-95,105,150,90),new Vector4(-100,105,145,95),new Vector4(-105,115,145,90),new Vector4(-90,110,150,100)};
   for(int k=0;k<configs.Length;k++)
   {
    EditorUtility.DisplayProgressBar("Outside town","Fitting radial blocks and existing taverns",(float)k/(configs.Length+2));
    var c=configs[k];var plan=CreatePlan(new Vector2(c.x,c.y),c.z,c.w,station);
    string why;if(!RoadsClear(plan,out why)){notes.Add(k+": "+why);continue;}
    for(int pass=0;pass<3;pass++){plan.Lots.Clear();Pack(plan,houses,pass);if(plan.Lots.Count==houses.Count)break;}
    notes.Add(k+": fitted "+plan.Lots.Count+" / "+houses.Count);
    if(plan.Lots.Count!=houses.Count)continue;plan.Cost=plan.Lots.Sum(l=>Mathf.Abs(l.Y-Ground(l.Center)))+Vector2.Distance(plan.Center,new Vector2(-145,108))*.05f;
    if(best==null||plan.Cost<best.Cost)best=plan;if(k>=2&&best!=null)break;
   }
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RadialTown-fit.txt",string.Join("\n",notes));
   if(best==null)throw new InvalidOperationException("No complete clear radial layout fits every full-size tavern. Existing town retained. See RadialTown-fit.txt.");
   var tower=city.transform.Find("Gothic Town Center Tower");
   var protectedModels=GameObject.Find("Castle Mountain").GetComponentsInChildren<Transform>(true);var protectedMatrices=protectedModels.Select(t=>t.localToWorldMatrix).ToArray();
   string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss"),folder="Assets/CityExpansion/RadialOutsideTown-"+stamp;
   string backup="Assets/ForestExpansion/Backups/MyForest-before-radial-outside-town-"+stamp+".unity";
   if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up the existing town.");
   Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var pavingSource=oldRoads.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.Contains("Joined")||r.name=="Central Market Square");
   var paving=new Material(pavingSource?pavingSource.sharedMaterial:AssetDatabase.LoadAssetAtPath<Material>("Assets/Building/Cobblestone/CastlePavingStone01.mat")){name="Radial Town Cobblestone"};paving.mainTextureScale=Vector2.one;paving.SetFloat("_Parallax",0);paving.DisableKeyword("_PARALLAXMAP");paving.enableInstancing=true;AssetDatabase.CreateAsset(paving,folder+"/Cobblestone.mat");
   var dirtSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/CityExpansion/CastleFlowerBeds-20261014-115651/OriginalBrownDirtBeds.mat");var dirt=new Material(dirtSource?dirtSource:paving){name="Radial Town Soft Dirt Edges"};dirt.SetColor("_BaseColor",new Color(.47f,.39f,.28f));dirt.mainTextureScale=Vector2.one;AssetDatabase.CreateAsset(dirt,folder+"/DirtEdges.mat");
   var oldFooting=oldHomes.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name.StartsWith("Stone House Foundation"));var footingMaterial=oldFooting?oldFooting.sharedMaterial:paving;
   var archive=Group(city.transform,"Previous Outside Town - Inactive "+stamp);var oldChildren=city.transform.Cast<Transform>().Where(t=>t!=archive&&t!=tower).ToArray();
   foreach(var child in oldChildren){Undo.SetTransformParent(child,archive,"Archive former outside layout");}Undo.RecordObject(archive.gameObject,"Retire former outside roads and lots");archive.gameObject.SetActive(false);
   var roads=Group(city.transform,"Winding Roads and Market Square");Group(roads,Marker);var homes=Group(city.transform,"House Neighborhoods");var smiths=Group(city.transform,"Three Outside Castle Blacksmiths");
   var field=new Field{Center=best.Center};foreach(var road in best.Roads)field.Add(road);
   int number=0;foreach(var lot in best.Lots)
   {
    var house=lot.House;Undo.SetTransformParent(house.Source,house.Smith?smiths:homes,"Place existing building in new radial block");Undo.RecordObject(house.Source,"Face tavern toward its new street");
    float yaw=Mathf.Atan2(lot.Front.x,lot.Front.y)*Mathf.Rad2Deg;house.Source.rotation=Quaternion.Euler(0,yaw,0);house.Source.localScale=house.Scale;
    var center=house.Source.rotation*Vector3.Scale(house.Local.center,house.Scale);house.Source.position=new Vector3(lot.Center.x-center.x,lot.Y-house.Local.min.y*house.Scale.y,lot.Center.y-center.z);
    PrefabUtility.RecordPrefabInstancePropertyModifications(house.Source);
    var doorRoad=new Road{Name="Door Access "+(++number).ToString("000"),Width=2.2f,Points=new List<Vector2>{lot.RoadPoint,lot.Door+lot.Front*.1f}};field.Add(doorRoad);
    var support=new Geometry();var corners=lot.Corners;float top=lot.Y-.015f;
    var a=new Vector3(corners[0].x,top,corners[0].y);var b=new Vector3(corners[1].x,top,corners[1].y);var c=new Vector3(corners[2].x,top,corners[2].y);var d=new Vector3(corners[3].x,top,corners[3].y);support.Tri(a,b,c);support.Tri(a,c,d);
    for(int i=0;i<4;i++){var p=corners[i];var q=corners[(i+1)%4];var u=new Vector3(p.x,top,p.y);var v=new Vector3(q.x,top,q.y);var w=new Vector3(p.x,Ground(p)-.03f,p.y);var z=new Vector3(q.x,Ground(q)-.03f,q.y);support.Tri(u,w,v);support.Tri(v,w,z);}
    Save(support,"Low Stone Footing "+number,footingMaterial,house.Smith?smiths:homes,folder,true);
   }
   if(tower){var bounds=BoundsOf(tower.gameObject);Undo.RecordObject(tower,"Center existing market monument on new focal plaza");tower.position+=new Vector3(best.Center.x-bounds.center.x,Ground(best.Center)+.055f-bounds.min.y,best.Center.y-bounds.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(tower);}
   foreach(var road in best.Roads)CreateGuide(road,roads,paving,folder);
   EditorUtility.DisplayProgressBar("Outside town","Joining road intersections and door paths",.8f);
   var surface=BuildSurface(field,best,.045f,0);var shoulder=BuildSurface(field,best,.012f,.45f);Save(shoulder,"Flush Dirt Road Margins",dirt,roads,folder,false);Save(surface,"Joined Radial Cobblestone Surface",paving,roads,folder,true);
   var square=Group(roads,"Central Market Square");square.position=new Vector3(best.Center.x,Ground(best.Center)+.045f,best.Center.y);
   Physics.SyncTransforms();int cleared=ClearVegetation(scene,city.transform,field,best,folder);
   for(int i=0;i<protectedModels.Length;i++)if(protectedModels[i].localToWorldMatrix!=protectedMatrices[i])throw new InvalidOperationException("Castle or transport transform changed unexpectedly.");
   WritePlan(best,folder,backup,cleared);
   AssetDatabase.SaveAssets();Physics.SyncTransforms();EditorUtility.DisplayProgressBar("Outside town","Updating walking routes to the new streets",.94f);
   RefreshRadialTownNavigation.Apply(roads,folder);
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save the new town.");
   File.WriteAllText("Logs/RebuildRadialOutsideTown-report.txt","Replaced the outside town with 8 radial spokes, 2 complete octagonal ring roads and a 15m focal plaza. Reused all "+best.Lots.Count(l=>!l.House.Smith)+" existing taverns at their previous sizes, with "+best.Lots.Count(l=>l.House.Smith)+" blacksmiths. Every building faces a street and has a short entrance path. Single joined terrain-following cobblestone surface, continuous world-space UVs, low dirt margins. Old outside roads, foundations and fence yards archived inactive; castle and transport transforms unchanged. Existing 750 NPC population and role mix retained; paths rebaked for new streets. Ground plants cleared only under new roads and building footprints: "+cleared+".\nBackup: "+backup+"\nAssets: "+folder+"\nLayout audit: "+folder+"/Layout.json\nScene saved. Play-mode observation still needed.");
   Selection.activeGameObject=roads.gameObject;SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(best.Center.x,Ground(best.Center)+10,best.Center.y),new Vector3(best.X*2+65,60,best.Z*2+65)),false);SceneView.RepaintAll();Debug.Log("Radial star outside town saved: 50 taverns arranged on 8 spokes and 2 octagonal rings; NPC street routes updated.");
  }
  static House ReadHouse(Transform t,bool smith){var b=LocalBounds(t);var s=t.lossyScale;return new House{Source=t,Local=b,Scale=s,Width=b.size.x*s.x,Depth=b.size.z*s.z,Height=b.size.y*s.y,Smith=smith};}
  static Bounds LocalBounds(Transform t){bool first=true;var result=new Bounds();foreach(var r in t.GetComponentsInChildren<Renderer>(true)){var b=r.localBounds;for(int k=0;k<8;k++){var p=t.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}}if(first)throw new InvalidOperationException("No visible house geometry on "+t.name);return result;}
  static Bounds BoundsOf(GameObject g){var r=g.GetComponentsInChildren<Renderer>(true);var b=r[0].bounds;foreach(var x in r)b.Encapsulate(x.bounds);return b;}
  static Plan CreatePlan(Vector2 center,float x,float z,Vector2 station)
  {
   var p=new Plan{Center=center,X=x,Z=z};
   for(int ring=0;ring<2;ring++){float scale=ring==0?.52f:1;var r=new Road{Name="Neighborhood Loop "+(ring+1),Width=ring==0?4.8f:5.2f};for(int i=0;i<=8;i++){float a=i*Mathf.PI/4;r.Points.Add(center+new Vector2(Mathf.Cos(a)*x*scale,Mathf.Sin(a)*z*scale));}p.Roads.Add(r);}
   for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var r=new Road{Name="Market Street "+(i+1),Width=i%2==0?6.5f:4.5f};r.Points.Add(center);r.Points.Add(center+new Vector2(Mathf.Cos(a)*x,Mathf.Sin(a)*z));p.Roads.Add(r);}
   // Reach the existing stair foot, without moving either station or the rail line.
   Vector2 nearest=Vector2.zero;float distance=float.MaxValue;foreach(var r in p.Roads)for(int j=1;j<r.Points.Count;j++){var q=Nearest(station,r.Points[j-1],r.Points[j]);float d=(q-station).sqrMagnitude;if(d<distance){distance=d;nearest=q;}}
   p.Roads.Add(new Road{Name="Funicular Approach Road",Width=4,Points=new List<Vector2>{nearest,station}});return p;
  }
  static bool RoadsClear(Plan p,out string why)
  {
   foreach(var r in p.Roads)for(int i=1;i<r.Points.Count;i++){var a=r.Points[i-1];var b=r.Points[i];var normal=new Vector2(-(b-a).y,(b-a).x).normalized;int n=Mathf.CeilToInt(Vector2.Distance(a,b)/2);float previous=float.NaN;
    for(int j=0;j<=n;j++){var q=Vector2.Lerp(a,b,(float)j/n);float h=Ground(q);if(float.IsNaN(h)){why="Road outside terrain";return false;}if(!float.IsNaN(previous)&&Mathf.Abs(h-previous)>1.0f){why="Steep ground on "+r.Name;return false;}previous=h;
     var possible=obstacles.Where(o=>!o.HousesOnly&&h+2>o.Bounds.min.y&&h+.15f<o.Bounds.max.y&&FlatDistance(q,o.Bounds)<r.Width*.5f+1.5f).ToArray();
     if(possible.Length>0){var forward=new Vector3((b-a).x,0,(b-a).y).normalized;var hits=Physics.OverlapBox(new Vector3(q.x,h+1,q.y),new Vector3(r.Width*.5f+.25f,.85f,1.15f),Quaternion.LookRotation(forward),~0,QueryTriggerInteraction.Ignore);var actual=possible.FirstOrDefault(o=>hits.Contains(o.Collider));if(actual!=null){why=r.Name+" blocked by actual "+actual.Name+" near "+q;return false;}}
     if(Mathf.Abs(Ground(q+normal*r.Width*.5f)-Ground(q-normal*r.Width*.5f))>.8f){why="Excessive road cross slope";return false;}
    }}why=null;return true;
  }
  static void Pack(Plan p,List<House> houses,int pass)
  {
   var field=new Field{Center=p.Center};foreach(var r in p.Roads)field.Add(r);
   foreach(var house in houses)
   {
    Lot best=null;float score=float.MaxValue;
    for(int ring=0;ring<2;ring++)for(int edge=0;edge<8;edge++)
    {
     var road=p.Roads[ring];var a=road.Points[edge];var b=road.Points[edge+1];float length=Vector2.Distance(a,b);var tangent=(b-a)/length;
     for(int sign=-1;sign<=1;sign+=2)for(float run=house.Width*.5f+7+pass*.65f;run<length-house.Width*.5f-7;run+=2.0f)
     {
      var roadPoint=a+tangent*run;var normal=new Vector2(-tangent.y,tangent.x)*sign;var front=-normal;var right=new Vector2(front.y,-front.x);var center=roadPoint+normal*(road.Width*.5f+2.5f+house.Depth*.5f);
      var lot=new Lot{House=house,Center=center,Front=front,Right=right,RoadPoint=roadPoint,Door=center+front*house.Depth*.5f,Street=road.Name,Edge=edge,Side=sign};lot.Corners=Corners(lot,.18f);
      if(p.Lots.Any(other=>Intersects(lot,other,2.1f)))continue;
      if(!FootprintClear(lot,field))continue;
      bool clear=true;float min=10000,max=-10000;
      for(int u=-2;u<=2&&clear;u++)for(int v=-2;v<=2;v++){var q=center+right*(house.Width*.5f*u/2)+front*(house.Depth*.5f*v/2);if(field.Value(q)<1.05f){clear=false;break;}float h=Ground(q);if(float.IsNaN(h)){clear=false;break;}min=Mathf.Min(min,h);max=Mathf.Max(max,h);}
      if(!clear||max-min>.55f)continue;lot.Y=max+.035f;
      var possible=obstacles.Where(o=>o.Bounds.max.y>min+.15f&&o.Bounds.min.y<max+house.Height&&IntersectsBounds(lot,o.Bounds,.65f)).ToArray();
      if(possible.Length>0){var pose=Quaternion.LookRotation(new Vector3(front.x,0,front.y));var hits=Physics.OverlapBox(new Vector3(center.x,(min+max+house.Height)*.5f,center.y),new Vector3(house.Width*.5f+.3f,(max-min+house.Height)*.5f,house.Depth*.5f+.3f),pose,~0,QueryTriggerInteraction.Ignore);if(possible.Any(o=>hits.Contains(o.Collider)))clear=false;}
      if(!clear)continue;
      // Balance fronts across sectors; favour the smallest level footprint.
      int sector=p.Lots.Count(l=>l.Street==road.Name&&l.Edge==edge),sideCount=p.Lots.Count(l=>l.Street==road.Name&&l.Edge==edge&&l.Side==sign);
      float cost=(max-min)*20+sector*8+sideCount*2+((edge*5+ring*3+pass*7)%8)*.1f+(run-house.Width*.5f-7)*.015f+(ring==0?1:0);
      if(cost<score){score=cost;best=lot;}
     }
    }
    if(best==null)return;p.Lots.Add(best);
   }
  }
  static Vector2[] Corners(Lot l,float pad){float x=l.House.Width*.5f+pad,z=l.House.Depth*.5f+pad;return new[]{l.Center-l.Right*x-l.Front*z,l.Center-l.Right*x+l.Front*z,l.Center+l.Right*x+l.Front*z,l.Center+l.Right*x-l.Front*z};}
  static bool Intersects(Lot a,Lot b,float gap){foreach(var axis in new[]{a.Right,a.Front,b.Right,b.Front}){float ra=Mathf.Abs(Vector2.Dot(a.Right,axis))*(a.House.Width*.5f+gap*.5f)+Mathf.Abs(Vector2.Dot(a.Front,axis))*(a.House.Depth*.5f+gap*.5f),rb=Mathf.Abs(Vector2.Dot(b.Right,axis))*(b.House.Width*.5f+gap*.5f)+Mathf.Abs(Vector2.Dot(b.Front,axis))*(b.House.Depth*.5f+gap*.5f);if(Mathf.Abs(Vector2.Dot(b.Center-a.Center,axis))>=ra+rb)return false;}return true;}
  static bool IntersectsBounds(Lot a,Bounds b,float gap){var h=new House{Width=b.size.x,Depth=b.size.z};return Intersects(a,new Lot{House=h,Center=Flat(b.center),Right=Vector2.right,Front=Vector2.up},gap);}
  static bool FootprintClear(Lot lot,Field field)
  {
   if(PointRectDistance(field.Center,lot)<field.Plaza+1.25f)return false;
   foreach(var s in field.Segments){float distance=Mathf.Min(PointRectDistance(s.A,lot),PointRectDistance(s.B,lot));for(int i=0;i<4;i++)distance=Mathf.Min(distance,SegmentDistance(s.A,s.B,lot.Corners[i],lot.Corners[(i+1)%4]));if(distance<s.Half+1.25f)return false;}return true;
  }
  static float PointRectDistance(Vector2 p,Lot lot){var v=p-lot.Center;float x=Mathf.Max(0,Mathf.Abs(Vector2.Dot(v,lot.Right))-lot.House.Width*.5f-.18f),z=Mathf.Max(0,Mathf.Abs(Vector2.Dot(v,lot.Front))-lot.House.Depth*.5f-.18f);return Mathf.Sqrt(x*x+z*z);}
  static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static float SegmentDistance(Vector2 a,Vector2 b,Vector2 c,Vector2 d){var v=b-a;var w=d-c;float denominator=Cross(v,w);if(Mathf.Abs(denominator)>1e-6f){float t=Cross(c-a,w)/denominator,u=Cross(c-a,v)/denominator;if(t>=0&&t<=1&&u>=0&&u<=1)return 0;}return Mathf.Min(Mathf.Min(Distance(a,c,d),Distance(b,c,d)),Mathf.Min(Distance(c,a,b),Distance(d,a,b)));}
  static List<Obstacle> CollectObstacles(Transform city)
  {
   var result=new List<Obstacle>();foreach(var c in UnityEngine.Object.FindObjectsByType<Collider>()){
    if(!c.enabled||c.isTrigger||c is TerrainCollider||c.transform.IsChildOf(city)||c.name.StartsWith("ForestGround"))continue;
    if(c.GetComponentInParent<CityCrowdManager>()||IsSmallSiteRock(c.transform))continue;
    if(c.transform.GetComponentsInParent<Transform>().Any(t=>t.name=="PlayerArmature"||t.name.Contains("Practice Bot")||IsPlant(t.name)))continue;
    var b=c.bounds;if(b.size.y<.15f||b.max.x< -340||b.min.x>100||b.max.z< -40||b.min.z>260)continue;
    result.Add(new Obstacle{Bounds=b,Name=c.name,HousesOnly=c.GetComponentInParent<StarterAssets.CastleFunicular>(),Collider=c});
   }return result;
  }
  static bool IsPlant(string n){n=n.ToLowerInvariant();return n.Contains("grass")||n.Contains("bush")||n.Contains("spruce")||n.Contains("tree")||n.Contains("flower")||n.Contains("mushroom")||n.Contains("fir_");}
  static bool IsSmallSiteRock(Transform t){var root=t.GetComponentsInParent<Transform>().FirstOrDefault(p=>p.name.StartsWith("UNS_Standard_Rock",StringComparison.Ordinal)||p.name.StartsWith("UNS_Small_Rock",StringComparison.Ordinal));if(!root||root.GetComponentsInChildren<Renderer>().Length==0)return false;var b=BoundsOf(root.gameObject);return b.size.x<=12&&b.size.y<=12&&b.size.z<=12;}
  static Geometry BuildSurface(Field field,Plan p,float offset,float expand)
  {
   var g=new Geometry();const float cell=.8f;float x0=p.Center.x-p.X-50,z0=p.Center.y-p.Z-50;int nx=Mathf.CeilToInt((p.X*2+100)/cell),nz=Mathf.CeilToInt((p.Z*2+100)/cell);var values=new float[nx+1,nz+1];
   for(int x=0;x<=nx;x++)for(int z=0;z<=nz;z++)values[x,z]=field.Value(new Vector2(x0+x*cell,z0+z*cell))-expand;
   for(int x=0;x<nx;x++)for(int z=0;z<nz;z++){var a=new Vector2(x0+x*cell,z0+z*cell);var b=a+Vector2.up*cell;var c=a+Vector2.right*cell;var d=a+Vector2.one*cell;Clip(new[]{a,b,c},new[]{values[x,z],values[x,z+1],values[x+1,z]},g,offset);Clip(new[]{c,b,d},new[]{values[x+1,z],values[x,z+1],values[x+1,z+1]},g,offset);}
   if(g.V.Count<3000)throw new InvalidOperationException("Incomplete road surface.");return g;
  }
  static void Clip(Vector2[] corners,float[] values,Geometry g,float height){var polygon=new List<Vector2>();for(int i=0;i<3;i++){int j=(i+1)%3;if(values[i]<=0)polygon.Add(corners[i]);if((values[i]<0)!=(values[j]<0))polygon.Add(Vector2.Lerp(corners[i],corners[j],values[i]/(values[i]-values[j])));}for(int i=1;i+1<polygon.Count;i++)g.Tri(Surface(polygon[0],height),Surface(polygon[i],height),Surface(polygon[i+1],height));}
  static Vector3 Surface(Vector2 p,float offset)=>new Vector3(p.x,Ground(p)+offset,p.y);
  static void Save(Geometry geometry,string name,Material material,Transform parent,string folder,bool collider){var mesh=geometry.Make(name);AssetDatabase.CreateAsset(mesh,folder+"/"+name.Replace(" ","_")+".asset");var t=Group(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;if(collider)t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;}
  static void CreateGuide(Road road,Transform parent,Material material,string folder)
  {
   var vertices=new List<Vector3>();var indices=new List<int>();var points=new List<Vector2>();for(int j=1;j<road.Points.Count;j++){var a=road.Points[j-1];var b=road.Points[j];int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));for(int i=0;i<n;i++)points.Add(Vector2.Lerp(a,b,(float)i/n));}points.Add(road.Points.Last());
   for(int i=0;i<points.Count;i++){var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)]).normalized;var side=new Vector2(-tangent.y,tangent.x)*road.Width*.5f;vertices.Add(Surface(points[i]-side,.045f));vertices.Add(Surface(points[i]+side,.045f));if(i+1<points.Count){int n=i*2;indices.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});}}
   var mesh=new Mesh{name=road.Name+" Navigation Guide",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Guide-"+road.Name.Replace(" ","_")+".asset");var t=Group(parent,road.Name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.enabled=false;
  }
  static bool Occupied(Vector2 q,Field field,Plan plan)=>field.Value(q)<.8f||plan.Lots.Any(l=>Mathf.Abs(Vector2.Dot(q-l.Center,l.Right))<l.House.Width*.5f+1&&Mathf.Abs(Vector2.Dot(q-l.Center,l.Front))<l.House.Depth*.5f+1);
  static int ClearVegetation(Scene scene,Transform city,Field field,Plan plan,string folder)
  {
   var cells=new HashSet<Vector2Int>();for(int x=Mathf.FloorToInt(plan.Center.x-plan.X-45);x<=Mathf.CeilToInt(plan.Center.x+plan.X+45);x++)for(int z=Mathf.FloorToInt(plan.Center.y-plan.Z-45);z<=Mathf.CeilToInt(plan.Center.y+plan.Z+45);z++)if(Occupied(new Vector2(x+.5f,z+.5f),field,plan))cells.Add(new Vector2Int(x,z));
   Func<Vector3,bool> cleared=p=>cells.Contains(new Vector2Int(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.z)));
   int count=0;foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray()){
    if(!t||t.IsChildOf(city)||!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)||(!IsPlant(t.name)&&!IsSmallSiteRock(t))||t.GetComponentsInChildren<Renderer>().Length==0)continue;var b=BoundsOf(t.gameObject);if(b.size.x>30||b.size.z>30)continue;
    bool hit=Occupied(Flat(b.center),field,plan);for(int k=0;k<8&&!hit;k++){float a=k*Mathf.PI/4;hit=Occupied(Flat(b.center)+new Vector2(Mathf.Cos(a)*b.extents.x,Mathf.Sin(a)*b.extents.z),field,plan);}if(!hit)continue;
    Undo.RecordObject(t.gameObject,"Clear vegetation from new street and tavern plot");t.gameObject.SetActive(false);count++;
   }
   // Instance-based ground cover is copied only if it actually intersects a new plot.
   foreach(var cover in UnityEngine.Object.FindObjectsByType<StarterAssets.ForestGroundCover>()){
    if(!cover.Data)continue;int removed=0;var batches=new List<StarterAssets.ForestGroundCoverData.Batch>();foreach(var b in cover.Data.Batches){var matrices=b.Matrices.Where(m=>!cleared(m.GetColumn(3))).ToArray();removed+=b.Matrices.Length-matrices.Length;batches.Add(new StarterAssets.ForestGroundCoverData.Batch{Mesh=b.Mesh,DistantMesh=b.DistantMesh,Material=b.Material,Submesh=b.Submesh,Bounds=b.Bounds,Matrices=matrices});}
    if(removed==0)continue;var copy=UnityEngine.Object.Instantiate(cover.Data);copy.name="Radial Town Ground Cover";copy.Batches=batches.ToArray();copy.TotalPlants=batches.Sum(b=>b.Matrices.Length);AssetDatabase.CreateAsset(copy,folder+"/GroundCover-"+cover.GetEntityId()+".asset");Undo.RecordObject(cover,"Keep grass off new roads and lots");cover.Data=copy;count+=removed;
   }
   int meshId=0;
   foreach(var f in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).ToArray())
   {
    if(!f||f.transform.IsChildOf(city)||!f.sharedMesh||!f.transform.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("Original Dense Grass")||t.name.Contains("Visible Original Grass")))continue;
    var renderer=f.GetComponent<Renderer>();if(!renderer||renderer.bounds.max.x<plan.Center.x-plan.X-45||renderer.bounds.min.x>plan.Center.x+plan.X+45||renderer.bounds.max.z<plan.Center.y-plan.Z-45||renderer.bounds.min.z>plan.Center.y+plan.Z+45)continue;
    var source=f.sharedMesh;
    using(var input=MeshUtility.AcquireReadOnlyMeshData(source))
    using(var positions=new NativeArray<Vector3>(input[0].vertexCount,Allocator.Temp))
    {
     var data=input[0];data.GetVertices(positions);var kept=new List<int>();var descriptors=new List<SubMeshDescriptor>();int removed=0;
     for(int sub=0;sub<source.subMeshCount;sub++){int start=kept.Count;var desc=data.GetSubMesh(sub);using(var indices=new NativeArray<int>(desc.indexCount,Allocator.Temp)){data.GetIndices(indices,sub,true);for(int j=0;j<indices.Length;j+=3){var p=f.transform.TransformPoint((positions[indices[j]]+positions[indices[j+1]]+positions[indices[j+2]])/3);if(cleared(p)){removed++;continue;}kept.Add(indices[j]);kept.Add(indices[j+1]);kept.Add(indices[j+2]);}}descriptors.Add(new SubMeshDescriptor(start,kept.Count-start,MeshTopology.Triangles){bounds=source.bounds,vertexCount=data.vertexCount});}
     if(removed==0)continue;
     var writable=Mesh.AllocateWritableMeshData(1);var target=writable[0];target.SetVertexBufferParams(data.vertexCount,source.GetVertexAttributes());for(int stream=0;stream<source.vertexBufferCount;stream++)data.GetVertexData<byte>(stream).CopyTo(target.GetVertexData<byte>(stream));target.SetIndexBufferParams(kept.Count,IndexFormat.UInt32);var buffer=target.GetIndexData<int>();for(int j=0;j<kept.Count;j++)buffer[j]=kept[j];target.subMeshCount=descriptors.Count;for(int j=0;j<descriptors.Count;j++)target.SetSubMesh(j,descriptors[j],MeshUpdateFlags.DontRecalculateBounds);
     var mesh=new Mesh{name=source.name+" Radial Town Clearance"};Mesh.ApplyAndDisposeWritableMeshData(writable,mesh,MeshUpdateFlags.DontRecalculateBounds);mesh.bounds=source.bounds;AssetDatabase.CreateAsset(mesh,folder+"/GrassClearance-"+(meshId++)+".asset");Undo.RecordObject(f,"Clear combined grass only on new streets and lots");f.sharedMesh=mesh;count+=removed;
    }
   }
   foreach(var terrain in terrains)
   {
    var original=terrain.terrainData;if(original.treeInstanceCount==0&&original.detailPrototypes.Length==0)continue;var origin=terrain.transform.position;var size=original.size;
    if(origin.x>plan.Center.x+plan.X+45||origin.x+size.x<plan.Center.x-plan.X-45||origin.z>plan.Center.y+plan.Z+45||origin.z+size.z<plan.Center.y-plan.Z-45)continue;
    var copy=UnityEngine.Object.Instantiate(original);int removed=0;var trees=copy.treeInstances.Where(t=>!cleared(origin+Vector3.Scale(t.position,size))).ToArray();removed+=copy.treeInstanceCount-trees.Length;copy.treeInstances=trees;
    for(int layer=0;layer<copy.detailPrototypes.Length;layer++){var detail=copy.GetDetailLayer(0,0,copy.detailWidth,copy.detailHeight,layer);for(int z=0;z<copy.detailHeight;z++)for(int x=0;x<copy.detailWidth;x++)if(detail[z,x]>0&&cleared(origin+new Vector3((x+.5f)*size.x/copy.detailWidth,0,(z+.5f)*size.z/copy.detailHeight))){removed+=detail[z,x];detail[z,x]=0;}copy.SetDetailLayer(0,0,layer,detail);}
    if(removed==0){UnityEngine.Object.DestroyImmediate(copy);continue;}AssetDatabase.CreateAsset(copy,folder+"/TerrainClearance-"+terrain.GetEntityId()+".asset");Undo.RecordObject(terrain,"Clear native plants from new radial streets");terrain.terrainData=copy;var collider=terrain.GetComponent<TerrainCollider>();if(collider){Undo.RecordObject(collider,"Use copied ground data");collider.terrainData=copy;}count+=removed;
   }
   return count;
  }
  [Serializable] sealed class Audit {public string backup;public Vector2 center;public float outerX,outerZ;public int taverns,blacksmiths,spokes,rings,cleared;public List<AuditLot> lots=new List<AuditLot>();public List<AuditRoad> roads=new List<AuditRoad>();}
  [Serializable] sealed class AuditLot {public string name,street;public Vector2 center,front;public Vector2[] corners;public float ground,width,depth,height;}
  [Serializable] sealed class AuditRoad {public string name;public float width;public Vector2[] points;}
  static void WritePlan(Plan p,string folder,string backup,int cleared){var a=new Audit{backup=backup,center=p.Center,outerX=p.X,outerZ=p.Z,taverns=p.Lots.Count(l=>!l.House.Smith),blacksmiths=p.Lots.Count(l=>l.House.Smith),spokes=8,rings=2,cleared=cleared};foreach(var l in p.Lots)a.lots.Add(new AuditLot{name=l.House.Source.name,street=l.Street,center=l.Center,front=l.Front,corners=l.Corners,ground=l.Y,width=l.House.Width,depth=l.House.Depth,height=l.House.Height});foreach(var r in p.Roads)a.roads.Add(new AuditRoad{name=r.Name,width=r.Width,points=r.Points.ToArray()});File.WriteAllText(folder+"/Layout.json",JsonUtility.ToJson(a,true));File.WriteAllText("Logs/RadialTown-layout.json",JsonUtility.ToJson(a,true));}
  static Transform Group(Transform parent,string name){var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Build radial outside town");g.transform.SetParent(parent,false);return g.transform;}
  static Vector2 Flat(Vector3 p)=>new Vector2(p.x,p.z);
  static float Distance(Vector2 p,Vector2 a,Vector2 b)=>Vector2.Distance(p,Nearest(p,a,b));
  static Vector2 Nearest(Vector2 p,Vector2 a,Vector2 b){var v=b-a;return a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/Mathf.Max(v.sqrMagnitude,.0001f));}
  static float FlatDistance(Vector2 p,Bounds b){float x=Mathf.Max(0,Mathf.Abs(p.x-b.center.x)-b.extents.x),z=Mathf.Max(0,Mathf.Abs(p.y-b.center.z)-b.extents.z);return Mathf.Sqrt(x*x+z*z);}
  static float Ground(Vector2 p){foreach(var terrain in terrains){var o=terrain.transform.position;var s=terrain.terrainData.size;if(p.x>=o.x&&p.x<=o.x+s.x&&p.y>=o.z&&p.y<=o.z+s.z)return terrain.SampleHeight(new Vector3(p.x,0,p.y))+o.y;}return float.NaN;}
 }
}
#endif
