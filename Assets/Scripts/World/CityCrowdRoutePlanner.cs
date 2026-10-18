using System;
using System.Collections.Generic;

// Routes remain the prevalidated road polylines. Only graph choices change.
public sealed class CityCrowdRoutePlanner
{
 public struct Edge {public int Route,From,To,Region;public float Length;}
 struct Arc {public int Route,From,To,Group;public float Length;}
 readonly Dictionary<int,int> indices=new Dictionary<int,int>();
 readonly List<Arc>[] outgoing,incoming;
 readonly Dictionary<long,float[]> fields=new Dictionary<long,float[]>();
 readonly Queue<long> fieldOrder=new Queue<long>();
 readonly Dictionary<int,int> groups=new Dictionary<int,int>();
 const int FieldLimit=512;
 public CityCrowdRoutePlanner(IList<Edge> edges)
 {
  foreach(var e in edges){if(!indices.ContainsKey(e.From))indices.Add(e.From,indices.Count);if(!indices.ContainsKey(e.To))indices.Add(e.To,indices.Count);}
  outgoing=new List<Arc>[indices.Count];incoming=new List<Arc>[indices.Count];for(int i=0;i<indices.Count;i++){outgoing[i]=new List<Arc>();incoming[i]=new List<Arc>();}
  var groupIds=new Dictionary<long,int>();
  foreach(var e in edges){if(e.From==e.To||e.Length<=.001f||float.IsNaN(e.Length)||float.IsInfinity(e.Length))continue;long key=((long)Math.Min(e.From,e.To)<<32)|(uint)Math.Max(e.From,e.To);if(!groupIds.TryGetValue(key,out int group)){group=groupIds.Count;groupIds.Add(key,group);}groups[e.Route]=group;var a=new Arc{Route=e.Route,From=indices[e.From],To=indices[e.To],Group=group,Length=e.Length};outgoing[a.From].Add(a);incoming[a.To].Add(a);}
 }
 public int Group(int route)=>groups.TryGetValue(route,out int group)?group:-1;
 public int Next(int start,int goal,int variant,Func<int,int> routeLoad=null,int previous=-1)
 {
  if(start==goal||!indices.TryGetValue(start,out int from)||!indices.TryGetValue(goal,out int to))return -1;variant&=7;var d=Distances(to,variant);if(float.IsInfinity(d[from]))return -1;
  int best=-1;float cost=float.MaxValue;foreach(var e in outgoing[from])
  {
   // Strictly decreasing goal distance prevents wandering and oscillation.
   if(d[e.To]>=d[from]-.0001f)continue;float travel=Cost(e,variant);float load=routeLoad==null?0:Math.Max(0,routeLoad(e.Route));float score=travel+d[e.To]+travel*Math.Min(2,load*.16f);
   if(indices.TryGetValue(previous,out int prior)&&e.To==prior)score+=travel*.3f;
   if(score<cost){cost=score;best=e.Route;}
  }return best;
 }
 float[] Distances(int goal,int variant)
 {
  long key=((long)goal<<3)|(uint)variant;if(fields.TryGetValue(key,out var result))return result;
  var distance=new float[indices.Count];for(int i=0;i<distance.Length;i++)distance[i]=float.PositiveInfinity;distance[goal]=0;var heap=new Heap();heap.Add(goal,0);
  while(heap.Count>0){heap.Pop(out int node,out float cost);if(cost>distance[node]+.0001f)continue;foreach(var e in incoming[node]){float next=cost+Cost(e,variant);if(next<distance[e.From]){distance[e.From]=next;heap.Add(e.From,next);}}}
  if(fields.Count>=FieldLimit)fields.Remove(fieldOrder.Dequeue());fields.Add(key,distance);fieldOrder.Enqueue(key);return distance;
 }
 static float Cost(Arc e,int variant){uint hash=Hash((uint)e.Group,(uint)variant+1);return e.Length*(.7f+(hash&65535)/65535f*1.35f);}
 public static uint Hash(uint a,uint b){unchecked{uint h=a*0x9e3779b9u+b*0x85ebca6bu+0x27d4eb2fu;h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;return h^(h>>16);}}
 sealed class Heap
 {
  readonly List<int> nodes=new List<int>();readonly List<float> costs=new List<float>();public int Count=>nodes.Count;
  public void Add(int node,float cost){int i=nodes.Count;nodes.Add(node);costs.Add(cost);while(i>0){int p=(i-1)/2;if(costs[p]<=cost)break;nodes[i]=nodes[p];costs[i]=costs[p];i=p;}nodes[i]=node;costs[i]=cost;}
  public void Pop(out int node,out float cost){node=nodes[0];cost=costs[0];int end=nodes.Count-1,n=nodes[end];float c=costs[end];nodes.RemoveAt(end);costs.RemoveAt(end);if(end==0)return;int i=0;while(i*2+1<end){int child=i*2+1;if(child+1<end&&costs[child+1]<costs[child])child++;if(costs[child]>=c)break;nodes[i]=nodes[child];costs[i]=costs[child];i=child;}nodes[i]=n;costs[i]=c;}
 }
}
