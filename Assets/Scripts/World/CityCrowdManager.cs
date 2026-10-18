using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
[Serializable]public sealed class CityCrowdRoute{public int From,To,Region,Transfer=-1,CircleNext=-1;public Vector3[] Corners;public float Length;}
[Serializable]public sealed class CityCrowdAppearance{public GameObject Skin;public string Role;public Mesh[] PoseA,PoseB;public Material[] Materials;}
[Serializable]public sealed class CityCrowdPerson{public int Route,Skin,TripWanted=-1;public string Role;public float NextTripAt;public float Distance,Speed,Lane,Wait;public Vector3 Position;public float Yaw;}
public sealed class CityCrowdManager:MonoBehaviour
{
 public CityCrowdRoute[] Routes;public CityCrowdAppearance[] Appearances;public CityCrowdPerson[] People;public GameObject EditorPreview;public CityCrowdTransit Transit;
 public int PopulationLimit=750;public int DetailedPerAppearance=12;public float DetailDistance=55,DrawDistance=550;
 public bool SpreadAcrossRoadsOnStart=true;
 public int PopulationCount=>People==null?0:People.Length;
 sealed class Actor{public GameObject Root;public Animator Animator;public int Person=-1,Skin;}
 readonly List<Actor> actors=new List<Actor>();Dictionary<int,List<int>> outgoing;System.Random random;Camera cameraView;float simulationTime,nextSelection;bool started;static readonly int SpeedParameter=Animator.StringToHash("Speed");readonly Matrix4x4[] matrices=new Matrix4x4[1023];readonly HashSet<int> detailed=new HashSet<int>();
 CityCrowdRoutePlanner planner;int[] walkingGoals,journeys,variants,lastNodes;
 readonly Dictionary<int,List<int>> regionNodes=new Dictionary<int,List<int>>();readonly Dictionary<int,Vector3> nodePositions=new Dictionary<int,Vector3>();readonly Dictionary<int,int> destinationUse=new Dictionary<int,int>(),streetLoads=new Dictionary<int,int>();
 void Start()
 {
  if(Routes==null||Routes.Length==0||Appearances==null||People==null){enabled=false;return;}if(People.Length>PopulationLimit)Array.Resize(ref People,PopulationLimit);started=true;if(EditorPreview)EditorPreview.SetActive(false);random=new System.Random(17421);outgoing=new Dictionary<int,List<int>>();for(int i=0;i<Routes.Length;i++){if(!outgoing.TryGetValue(Routes[i].From,out var list)){list=new List<int>();outgoing.Add(Routes[i].From,list);}list.Add(i);}BuildReturnRoutes();if(SpreadAcrossRoadsOnStart)SpreadResidentsAcrossRoads();InitializeRouting();cameraView=Camera.main;simulationTime=Time.time;
  for(int skin=0;skin<Appearances.Length;skin++)for(int i=0;i<DetailedPerAppearance;i++){var root=new GameObject("Nearby Synty NPC");root.transform.SetParent(transform,false);var visual=Instantiate(Appearances[skin].Skin,root.transform);var animator=visual.GetComponentInChildren<Animator>();animator.fireEvents=false;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullCompletely;var capsule=root.AddComponent<CapsuleCollider>();capsule.center=new Vector3(0,.875f,0);capsule.radius=.28f;capsule.height=1.75f;root.SetActive(false);actors.Add(new Actor{Root=root,Animator=animator,Skin=skin});}
 }
 void Update()
 {
  if(!started)return;if(Transit)Transit.Tick(this,Time.deltaTime);if(!cameraView)cameraView=Camera.main;if(Time.time-simulationTime>=.15f){float dt=Time.time-simulationTime;simulationTime=Time.time;UpdateStreetLoads();for(int personIndex=0;personIndex<People.Length;personIndex++){var person=People[personIndex];if(Transit&&Transit.IsPassenger(personIndex))continue;if(person.Wait>0){person.Wait=Mathf.Max(0,person.Wait-dt);}else{person.Distance+=person.Speed*dt;var route=Routes[person.Route];if(person.Distance>=route.Length){if(route.Transfer>=0&&Transit){if(!Transit.Enqueue(personIndex,route.Transfer)){CancelTravel(personIndex,route.Transfer);}continue;}person.Distance=0;person.Wait=.05f+(float)random.NextDouble()*.12f;if(outgoing.TryGetValue(route.To,out var options))person.Route=ChooseNext(personIndex,route.To,options);}}Locate(Routes[person.Route],person.Distance,person.Lane,out person.Position,out person.Yaw);}}
  if(cameraView&&Time.time>=nextSelection){nextSelection=Time.time+.5f;SelectDetailed();}
  foreach(var actor in actors){if(actor.Person<0)continue;var p=People[actor.Person];actor.Root.transform.position=Vector3.Lerp(actor.Root.transform.position,p.Position,1-Mathf.Exp(-12*Time.deltaTime));actor.Root.transform.rotation=Quaternion.Slerp(actor.Root.transform.rotation,Quaternion.Euler(0,p.Yaw,0),1-Mathf.Exp(-8*Time.deltaTime));actor.Animator.SetFloat(SpeedParameter,p.Wait>0?0:p.Speed,.18f,Time.deltaTime);}
 }
 void SelectDetailed()
 {
  var position=cameraView.transform.position;float limit=DetailDistance*DetailDistance;var planes=GeometryUtility.CalculateFrustumPlanes(cameraView);detailed.Clear();
  for(int skin=0;skin<Appearances.Length;skin++){var candidates=new List<int>();for(int i=0;i<People.Length;i++)if(People[i].Skin==skin&&(People[i].Position-position).sqrMagnitude<limit&&GeometryUtility.TestPlanesAABB(planes,new Bounds(People[i].Position+Vector3.up*.9f,new Vector3(2,2,2))))candidates.Add(i);candidates.Sort((a,b)=>(People[a].Position-position).sqrMagnitude.CompareTo((People[b].Position-position).sqrMagnitude));if(candidates.Count>DetailedPerAppearance)candidates.RemoveRange(DetailedPerAppearance,candidates.Count-DetailedPerAppearance);foreach(var i in candidates)detailed.Add(i);var free=new List<Actor>();foreach(var actor in actors){if(actor.Skin!=skin)continue;if(actor.Person>=0&&detailed.Contains(actor.Person)){candidates.Remove(actor.Person);continue;}actor.Person=-1;actor.Root.SetActive(false);free.Add(actor);}for(int i=0;i<candidates.Count&&i<free.Count;i++){var actor=free[i];actor.Person=candidates[i];actor.Root.transform.SetPositionAndRotation(People[actor.Person].Position,Quaternion.Euler(0,People[actor.Person].Yaw,0));actor.Root.SetActive(true);}}
 }
 void LateUpdate()
 {
  if(!started||!cameraView||!SystemInfo.supportsInstancing)return;var planes=GeometryUtility.CalculateFrustumPlanes(cameraView);var origin=cameraView.transform.position;float limit=DrawDistance*DrawDistance;
  for(int skin=0;skin<Appearances.Length;skin++)for(int phase=0;phase<2;phase++){var appearance=Appearances[skin];var meshes=phase==0?appearance.PoseA:appearance.PoseB;for(int part=0;part<meshes.Length;part++){int count=0;for(int i=0;i<People.Length;i++){var p=People[i];if(p.Skin!=skin||detailed.Contains(i)||(p.Position-origin).sqrMagnitude>limit||(p.Wait>0?0:(((int)(Time.time*2.5f+i*.37f))&1))!=phase||!GeometryUtility.TestPlanesAABB(planes,new Bounds(p.Position+Vector3.up*.9f,new Vector3(2,2,2))))continue;matrices[count++]=Matrix4x4.TRS(p.Position,Quaternion.Euler(0,p.Yaw,0),Vector3.one);if(count==1023){Draw(meshes[part],appearance.Materials[part],count);count=0;}}if(count>0)Draw(meshes[part],appearance.Materials[part],count);}}
 }
 void Draw(Mesh mesh,Material material,int count){Graphics.DrawMeshInstanced(mesh,0,material,matrices,count,null,ShadowCastingMode.Off,true,gameObject.layer,cameraView,LightProbeUsage.Off);}
 void BuildReturnRoutes()
 {
  if(!Transit||Transit.Transfers==null)return;var routes=new List<CityCrowdRoute>(Routes);for(int i=0;i<Transit.Transfers.Length;i++){var link=Transit.Transfers[i];int approach=routes.FindIndex(r=>r.Transfer==i);if(approach<0)continue;var source=routes[approach];var corners=(Vector3[])source.Corners.Clone();Array.Reverse(corners);link.ReturnRoute=routes.Count;routes.Add(new CityCrowdRoute{From=source.From,To=source.From,Region=source.Region,Transfer=-1,CircleNext=-1,Corners=corners,Length=source.Length});}Routes=routes.ToArray();
 }
 void CancelTravel(int personIndex,int transfer)
 {
  var person=People[personIndex];var link=Transit.Transfers[transfer];person.TripWanted=-1;person.NextTripAt=Time.time+120+personIndex%120;person.Wait=.3f;if(link.ReturnRoute>=0){person.Route=link.ReturnRoute;person.Distance=0;}else{person.Distance=Routes[person.Route].Length;person.Wait=5;}
 }
 public int ChooseNext(int personIndex,int node,List<int> available=null)
 {
  var p=People[personIndex];if(available==null&&!outgoing.TryGetValue(node,out available))return p.Route;int region=Routes[available[0]].Region;
  if(planner==null)InitializeRouting();
  if(Transit&&p.TripWanted<0&&Time.time>=p.NextTripAt){var eligible=new List<int>();for(int i=0;i<Transit.Transfers.Length;i++){var t=Transit.Transfers[i];foreach(var r in Routes)if(r.Transfer==i&&r.Region==region){eligible.Add(i);break;}}if(eligible.Count>0)p.TripWanted=eligible[random.Next(eligible.Count)];}
  if(Transit&&p.TripWanted>=0){var target=Transit.Transfers[p.TripWanted];if(node==target.FromNode){foreach(int route in available)if(Routes[route].Transfer==p.TripWanted)return route;}int next=planner.Next(node,target.FromNode,variants[personIndex],StreetLoad,lastNodes[personIndex]);if(next>=0){lastNodes[personIndex]=node;return next;}p.TripWanted=-1;p.NextTripAt=Time.time+20;}
  int goal=walkingGoals[personIndex];if(goal==node||!regionNodes.TryGetValue(region,out var goals)||!goals.Contains(goal)){if(goal==node)p.Wait=1+(float)random.NextDouble()*3;ReleaseGoal(personIndex);goal=PickGoal(personIndex,node,region);}
  int choice=planner.Next(node,goal,variants[personIndex],StreetLoad,lastNodes[personIndex]);if(choice>=0){lastNodes[personIndex]=node;return choice;}ReleaseGoal(personIndex);
  var walking=available.FindAll(i=>Routes[i].Transfer<0);return walking.Count>0?walking[random.Next(walking.Count)]:available[0];
 }
 void InitializeRouting()
 {
  var edges=new List<CityCrowdRoutePlanner.Edge>();regionNodes.Clear();nodePositions.Clear();destinationUse.Clear();streetLoads.Clear();
  for(int i=0;i<Routes.Length;i++){var r=Routes[i];if(r.Transfer>=0||r.From==r.To||r.Corners==null||r.Corners.Length<2)continue;edges.Add(new CityCrowdRoutePlanner.Edge{Route=i,From=r.From,To=r.To,Region=r.Region,Length=r.Length});if(!regionNodes.TryGetValue(r.Region,out var nodes)){nodes=new List<int>();regionNodes.Add(r.Region,nodes);}if(!nodes.Contains(r.From))nodes.Add(r.From);if(!nodes.Contains(r.To))nodes.Add(r.To);nodePositions[r.From]=r.Corners[0];nodePositions[r.To]=r.Corners[r.Corners.Length-1];}
  planner=new CityCrowdRoutePlanner(edges);walkingGoals=new int[People.Length];journeys=new int[People.Length];variants=new int[People.Length];lastNodes=new int[People.Length];for(int i=0;i<People.Length;i++){walkingGoals[i]=-1;lastNodes[i]=-1;variants[i]=(int)(CityCrowdRoutePlanner.Hash((uint)i,31)&7);}
 }
 void UpdateStreetLoads(){streetLoads.Clear();for(int i=0;i<People.Length;i++){if(Transit&&Transit.IsPassenger(i))continue;int group=planner.Group(People[i].Route);if(group<0)continue;streetLoads.TryGetValue(group,out int count);streetLoads[group]=count+1;}}
 int StreetLoad(int route){int group=planner.Group(route);return streetLoads.TryGetValue(group,out int count)?count:0;}
 void ReleaseGoal(int person){int goal=walkingGoals[person];if(goal>=0&&destinationUse.TryGetValue(goal,out int count))destinationUse[goal]=Mathf.Max(0,count-1);walkingGoals[person]=-1;}
 int PickGoal(int person,int node,int region)
 {
  if(!regionNodes.TryGetValue(region,out var nodes))return -1;journeys[person]++;variants[person]=(int)(CityCrowdRoutePlanner.Hash((uint)person,(uint)journeys[person]+71)&7);float best=float.MaxValue;int result=-1;nodePositions.TryGetValue(node,out var start);
  foreach(int candidate in nodes){if(candidate==node)continue;destinationUse.TryGetValue(candidate,out int use);float distance=Vector3.Distance(start,nodePositions[candidate]);float score=use*10-distance*.02f+(CityCrowdRoutePlanner.Hash((uint)candidate,(uint)(person+journeys[person]*113))&1023)/512f;if(distance<12)score+=20;if(score<best){best=score;result=candidate;}}
  if(result>=0){walkingGoals[person]=result;destinationUse.TryGetValue(result,out int use);destinationUse[result]=use+1;}return result;
 }
 struct RoadSample{public int Route;public float Distance;}
 public int SpreadResidentsAcrossRoads()
 {
  var cells=new Dictionary<int,Dictionary<long,RoadSample>>();
  for(int route=0;route<Routes.Length;route++){var r=Routes[route];if(r.Transfer>=0||r.From==r.To||r.Length<.5f||r.Corners==null||r.Corners.Length<2)continue;if(!cells.TryGetValue(r.Region,out var region)){region=new Dictionary<long,RoadSample>();cells.Add(r.Region,region);}int count=Mathf.Max(1,Mathf.CeilToInt(r.Length/4.5f));for(int j=0;j<count;j++){float distance=(j+.5f)*r.Length/count;Locate(r,distance,0,out var p,out _);long key=((long)Mathf.FloorToInt(p.x/5)<<32)|(uint)Mathf.FloorToInt(p.z/5);if(!region.TryGetValue(key,out var existing)||r.Length<Routes[existing.Route].Length)region[key]=new RoadSample{Route=route,Distance=distance};}}
  int placed=0;foreach(var pair in cells){var people=new List<int>();for(int i=0;i<People.Length;i++)if(Routes[People[i].Route].Region==pair.Key)people.Add(i);var slots=new List<RoadSample>(pair.Value.Values);var rng=new System.Random(151025+pair.Key);for(int i=slots.Count-1;i>0;i--){int j=rng.Next(i+1);var swap=slots[i];slots[i]=slots[j];slots[j]=swap;}if(slots.Count==0)continue;for(int i=0;i<people.Count;i++){int person=people[i];var p=People[person];var slot=slots[Mathf.Min(slots.Count-1,(int)((i+.5f)*slots.Count/people.Count))];p.Route=slot.Route;p.Distance=Mathf.Clamp(slot.Distance+((CityCrowdRoutePlanner.Hash((uint)person,51)&65535)/65535f-.5f)*Mathf.Min(3,Routes[p.Route].Length*.6f),.02f,Mathf.Max(.02f,Routes[p.Route].Length-.02f));p.TripWanted=-1;p.Wait=0;Locate(Routes[p.Route],p.Distance,p.Lane,out p.Position,out p.Yaw);placed++;}}
  return placed;
 }
 public static void Locate(CityCrowdRoute route,float distance,float lane,out Vector3 position,out float yaw){float remaining=Mathf.Clamp(distance,0,route.Length);for(int i=1;i<route.Corners.Length;i++){var a=route.Corners[i-1];var b=route.Corners[i];float length=Vector3.Distance(a,b);if(remaining<=length||i==route.Corners.Length-1){var direction=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized;position=Vector3.Lerp(a,b,length>.001f?remaining/length:0)+Vector3.Cross(Vector3.up,direction)*lane;yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;return;}remaining-=length;}position=route.Corners[0];yaw=0;}
}
