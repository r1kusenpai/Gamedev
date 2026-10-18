#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using StarterAssets;
namespace StarterAssets.CQCEditorTools
{
 // Only the replaced outside district gets new cached routes. Castle routes,
 // transport settings, appearance roles and the 500 inside residents are retained.
 public static class RefreshRadialTownNavigation
 {
  public static void Apply(Transform outside,string folder)
  {
   var crowd=UnityEngine.Object.FindAnyObjectByType<CityCrowdManager>();if(!crowd||crowd.People.Length!=750)throw new InvalidOperationException("Expected the existing 750-person population.");
   var registry=crowd.GetComponent<CityPopulationNavigation>();var transit=crowd.Transit;var lift=transit.Funicular;var elevator=transit.Elevator;
   var mountain=GameObject.Find("Castle Mountain").transform;var lower=mountain.Find("Cobblestone Castle Floors/Outer House Floor");var upper=mountain.Find("Inner Castle Mountain/Flat Inner Castle Floor");var bridge=mountain.Find("Castle Scaffold Elevator/Upper Landing Bridge");
   if(!registry||!lower||!upper||!bridge)throw new InvalidOperationException("Existing navigation floors or transport connections missing.");
   var bounds=lower.GetComponent<Collider>().bounds;bounds.Encapsulate(upper.GetComponent<Collider>().bounds);foreach(var r in outside.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);bounds.Expand(new Vector3(20,16,20));
   var markup=new List<NavMeshBuildMarkup>();foreach(var t in new[]{outside,lower,upper,bridge})markup.Add(new NavMeshBuildMarkup{root=t,overrideArea=true,area=0});
   foreach(var t in lift.GetComponentsInChildren<Transform>())if(t.name=="Lower Boarding Platform"||t.name=="Upper Castle Boarding Bridge"||t.name.StartsWith("Forest Access Step "))markup.Add(new NavMeshBuildMarkup{root=t,overrideArea=true,area=0});
   foreach(var t in lift.Carts)markup.Add(new NavMeshBuildMarkup{root=t,ignoreFromBuild=true});markup.Add(new NavMeshBuildMarkup{root=elevator.Platform,ignoreFromBuild=true});
   var sources=new List<NavMeshBuildSource>();NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,1,markup,sources);sources.RemoveAll(s=>s.shape==NavMeshBuildSourceShape.Terrain||(s.component&&s.component.name.StartsWith("ForestGround")));
   var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.3f;settings.agentHeight=1.8f;settings.agentClimb=.3f;settings.overrideVoxelSize=true;settings.voxelSize=.12f;
   var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);if(!data)throw new InvalidOperationException("New town navigation could not be baked.");AssetDatabase.CreateAsset(data,folder+"/CastleAndRadialTownNavigation.asset");
   bool enabled=registry.enabled;Undo.RecordObject(registry,"Replace outside navigation surface");registry.enabled=false;var temporary=NavMesh.AddNavMeshData(data);
   try
   {
    var guides=outside.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Neighborhood Loop")||f.name.StartsWith("Market Street")||f.name=="Funicular Approach Road").ToArray();
    var candidates=new List<Vector3>();foreach(var f in guides){var v=f.sharedMesh.vertices;for(int i=0;i+1<v.Length;i+=12){var p=f.transform.TransformPoint((v[i]+v[i+1])*.5f);if(NavMesh.SamplePosition(p,out var hit,1.0f,1)&&Mathf.Abs(hit.position.y-p.y)<.6f&&candidates.All(q=>Vector3.Distance(q,hit.position)>3))candidates.Add(hit.position);}}
    var seed=candidates.OrderBy(p=>Vector3.Distance(p,guides.First(f=>f.name=="Market Street 1").sharedMesh.bounds.center)).FirstOrDefault();var nodes=candidates.Where(p=>TryPath(seed,p,out _)).ToList();if(nodes.Count<24)throw new InvalidOperationException("New radial streets are not connected.");
    int offset=crowd.Routes.Max(r=>Mathf.Max(r.From,r.To))+1;var old=crowd.Routes;var routes=new List<CityCrowdRoute>();var mapping=new Dictionary<int,int>();
    for(int i=0;i<old.Length;i++)if(old[i].Region!=0){var r=old[i];mapping[i]=routes.Count;routes.Add(new CityCrowdRoute{From=r.From,To=r.To,Region=r.Region,Transfer=r.Transfer,CircleNext=r.CircleNext,Corners=(Vector3[])r.Corners.Clone(),Length=r.Length});}
    foreach(var pair in mapping){var r=routes[pair.Value];r.CircleNext=r.CircleNext>=0&&mapping.TryGetValue(r.CircleNext,out int next)?next:-1;}
    int walkingStart=routes.Count;
    for(int i=0;i<nodes.Count;i++)foreach(int j in Enumerable.Range(0,nodes.Count).Where(j=>j!=i).OrderBy(j=>Vector3.Distance(nodes[i],nodes[j])).Take(5))if(TryPath(nodes[i],nodes[j],out var path))routes.Add(Route(offset+i,offset+j,path));
    // Explicit guide chains prevent nearby-node selection from splitting the
    // two rings into separate logical walking graphs at a junction.
    foreach(var f in guides){var v=f.sharedMesh.vertices;int previous=-1;for(int i=0;i+1<v.Length;i+=12){var p=f.transform.TransformPoint((v[i]+v[i+1])*.5f);int node=Enumerable.Range(0,nodes.Count).OrderBy(j=>Vector3.Distance(nodes[j],p)).First();if(previous>=0&&previous!=node&&TryPath(nodes[previous],nodes[node],out var path)){routes.Add(Route(offset+previous,offset+node,path));routes.Add(Route(offset+node,offset+previous,Enumerable.Reverse(path).ToArray()));}previous=node;}}
    var circles=new List<int>();
    foreach(var f in guides.Where(f=>f.name.StartsWith("Neighborhood Loop")))
    {
     var v=f.sharedMesh.vertices;var points=new List<Vector3>();for(int i=0;i+1<v.Length;i+=2)points.Add(f.transform.TransformPoint((v[i]+v[i+1])*.5f));float total=Length(points.ToArray());var loop=new List<int>();
     for(int i=0;i<24;i++){var p=Along(points,total*i/24);int node=Enumerable.Range(0,nodes.Count).OrderBy(j=>Vector3.Distance(nodes[j],p)).First();if(loop.Count==0||loop.Last()!=node)loop.Add(node);}if(loop.Count>1&&loop.Last()==loop[0])loop.RemoveAt(loop.Count-1);if(loop.Count<12)throw new InvalidOperationException("Octagonal walking loop incomplete.");
     var ids=new List<int>();for(int i=0;i<loop.Count;i++){int a=loop[i],b=loop[(i+1)%loop.Count];if(!TryPath(nodes[a],nodes[b],out var path))throw new InvalidOperationException("Octagonal street disconnected.");ids.Add(routes.Count);routes.Add(Route(offset+a,offset+b,path));}for(int i=0;i<ids.Count;i++)routes[ids[i]].CircleNext=ids[(i+1)%ids.Count];circles.AddRange(ids);
    }
    var transfers=transit.Transfers.Select(t=>new CityCrowdTransfer{Vehicle=t.Vehicle,FromNode=t.FromNode,ToNode=t.ToNode,ReturnRoute=-1,FromUpper=t.FromUpper,Waiting=t.Waiting,QueueDirection=t.QueueDirection,Exit=(Vector3[])t.Exit.Clone()}).ToArray();
    int departing=Array.FindIndex(transfers,t=>t.Vehicle==1&&!t.FromUpper),arriving=Array.FindIndex(transfers,t=>t.Vehicle==1&&t.FromUpper);if(departing<0||arriving<0)throw new InvalidOperationException("Existing funicular transfers missing.");
    Vector3 waiting=transfers[departing].Waiting;int stationNode=-1;Vector3[] approach=null;
    foreach(int i in Enumerable.Range(0,nodes.Count).OrderBy(i=>Vector3.Distance(nodes[i],waiting)))if(TryPath(nodes[i],waiting,out var path)&&Vector3.Distance(path.Last(),waiting)<.8f){stationNode=i;approach=path;break;}
    if(stationNode<0)throw new InvalidOperationException("New town roads do not reach the existing funicular boarding platform.");
    var reachable=new HashSet<int>{offset+stationNode};bool changed=true;while(changed){changed=false;foreach(var r in routes)if(r.Region==0&&r.Transfer<0&&reachable.Contains(r.From)&&reachable.Add(r.To))changed=true;}if(nodes.Any((p)=>!reachable.Contains(offset+nodes.IndexOf(p))))throw new InvalidOperationException("Outside walking graph cannot reach every street from the funicular.");
    if(Vector3.Distance(approach.Last(),waiting)>.03f)approach=approach.Concat(new[]{waiting}).ToArray();if(approach.Length<2)approach=new[]{nodes[stationNode],waiting};
    transfers[departing].FromNode=offset+stationNode;transfers[departing].QueueDirection=Vector3.ProjectOnPlane(approach[approach.Length-2]-waiting,Vector3.up).normalized;transfers[arriving].ToNode=offset+stationNode;transfers[arriving].Exit=Enumerable.Reverse(approach).ToArray();
    foreach(var r in routes)if(r.Transfer==arriving)r.To=offset+stationNode;
    var toLift=Route(offset+stationNode,transfers[departing].ToNode,approach);toLift.Transfer=departing;routes.Add(toLift);
    Undo.RecordObject(crowd,"Use new outside roads while retaining castle residents");Undo.RecordObject(transit,"Reconnect existing lower funicular approach");
    var people=new CityCrowdPerson[crowd.People.Length];var rng=new System.Random(151025);int outsideCount=0;
    for(int i=0;i<people.Length;i++)
    {
     var p=crowd.People[i];var q=new CityCrowdPerson{Route=p.Route,Skin=p.Skin,TripWanted=p.TripWanted,Role=p.Role,NextTripAt=p.NextTripAt,Distance=p.Distance,Speed=p.Speed,Lane=p.Lane,Wait=p.Wait,Position=p.Position,Yaw=p.Yaw};
     if(old[p.Route].Region==0){q.Route=circles[outsideCount%circles.Count];q.Distance=(float)rng.NextDouble()*routes[q.Route].Length;q.TripWanted=-1;q.Wait=0;CityCrowdManager.Locate(routes[q.Route],q.Distance,q.Lane,out q.Position,out q.Yaw);outsideCount++;}else q.Route=mapping[p.Route];people[i]=q;
    }
    if(outsideCount!=250)throw new InvalidOperationException("Outside resident count changed unexpectedly: "+outsideCount);
    crowd.Routes=routes.ToArray();crowd.People=people;transit.Transfers=transfers;registry.Data=data;
    if(crowd.EditorPreview)foreach(Transform t in crowd.EditorPreview.transform){if(!t.name.StartsWith("NPC Preview ")||!int.TryParse(t.name.Substring(12),out int i)||i>=people.Length)continue;Undo.RecordObject(t,"Move NPC preview to new street");t.SetPositionAndRotation(people[i].Position,Quaternion.Euler(0,people[i].Yaw,0));}
    foreach(var obj in new UnityEngine.Object[]{crowd,registry,transit})EditorUtility.SetDirty(obj);
    File.WriteAllText("Logs/RadialTown-navigation.txt","Rebuilt outside street graph: "+nodes.Count+" nodes, "+(routes.Count-walkingStart)+" outside routes, 2 ring circuits, existing lower funicular platform reached. Repositioned 250 outside residents. Preserved 500 inside residents, castle cached paths, role mix, speed/lane settings and all transport behavior.");
   }
   finally{if(temporary.valid)temporary.Remove();registry.enabled=enabled;}
  }
  static CityCrowdRoute Route(int a,int b,Vector3[] path)=>new CityCrowdRoute{From=a,To=b,Region=0,Transfer=-1,CircleNext=-1,Corners=path,Length=Length(path)};
  static bool TryPath(Vector3 a,Vector3 b,out Vector3[] points){var p=new NavMeshPath();if(NavMesh.SamplePosition(a,out var aa,.8f,1)&&NavMesh.SamplePosition(b,out var bb,.8f,1)&&Mathf.Abs(aa.position.y-a.y)<.65f&&Mathf.Abs(bb.position.y-b.y)<.65f&&NavMesh.CalculatePath(aa.position,bb.position,1,p)&&p.status==NavMeshPathStatus.PathComplete){points=p.corners.Length>=2?p.corners:new[]{aa.position,bb.position};return true;}points=null;return false;}
  static float Length(Vector3[] path){float total=0;for(int i=1;i<path.Length;i++)total+=Vector3.Distance(path[i-1],path[i]);return total;}
  static Vector3 Along(List<Vector3> p,float distance){for(int i=1;i<p.Count;i++){float n=Vector3.Distance(p[i-1],p[i]);if(distance<=n)return Vector3.Lerp(p[i-1],p[i],distance/Mathf.Max(.001f,n));distance-=n;}return p.Last();}
 }
}
#endif
