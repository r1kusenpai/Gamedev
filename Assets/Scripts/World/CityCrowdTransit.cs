using System;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;

[Serializable] public sealed class CityCrowdTransfer
{
 public int Vehicle,FromNode,ToNode,ReturnRoute=-1;public bool FromUpper;public Vector3 Waiting,QueueDirection;public Vector3[] Exit;
}
public sealed class CityCrowdTransit:MonoBehaviour
{
 public CastleElevator Elevator;public CastleFunicular Funicular;public CityCrowdTransfer[] Transfers;
 public int BoardedTotal,CompletedTotal;
 const int QueueCapacity=6;const float BoardingSeconds=2.2f;
 sealed class Rider{public int Person,Transfer,Seat;public Vector3 Start;public float Progress,QueueDistance;}
 sealed class Trip{public readonly List<Rider> Riders=new List<Rider>();public int Transfer,Cart=-1,Stage;public float Time,AvailableAfter;public Transform Platform;public BoxCollider Deck;}
 sealed class ExitWalker{public Rider Rider;public Vector3[] Points;public float Length,Distance,LandingLength;}
 readonly List<Rider> waiting=new List<Rider>();readonly List<ExitWalker> exiting=new List<ExitWalker>();readonly HashSet<int> passengers=new HashSet<int>();
 // Elevator and each of the two carts own independent boarding and riding state.
 readonly Trip[] trips={new Trip(),new Trip{Cart=0},new Trip{Cart=1}};
 Vector3[][] queuePaths;float[] queueLengths,nextAdmission;
 public bool IsPassenger(int person)=>passengers.Contains(person);
 public bool Enqueue(int person,int transfer){if(passengers.Contains(person))return true;if(nextAdmission==null)nextAdmission=new float[Transfers.Length];if(Time.time<nextAdmission[transfer])return false;int count=0;foreach(var r in waiting)if(r.Transfer==transfer)count++;int capacity=queueLengths==null?QueueCapacity:Mathf.Min(QueueCapacity,Mathf.Max(1,Mathf.FloorToInt((queueLengths[transfer]-1.3f)/1.2f)+1));if(count>=capacity)return false;waiting.Add(new Rider{Person=person,Transfer=transfer});passengers.Add(person);nextAdmission[transfer]=Time.time+1.25f;return true;}
 public void Tick(CityCrowdManager crowd,float dt)
 {
  if(queuePaths==null)BuildQueues(crowd);TickExits(crowd,dt);
  for(int i=0;i<waiting.Count;i++){var r=waiting[i];int rank=0;for(int j=0;j<i;j++)if(waiting[j].Transfer==r.Transfer)rank++;var p=crowd.People[r.Person];float desired=Mathf.Min(1.3f+rank*1.2f,queueLengths[r.Transfer]);float previous=r.QueueDistance;r.QueueDistance=Mathf.MoveTowards(r.QueueDistance,desired,p.Speed*dt);var route=new CityCrowdRoute{Corners=queuePaths[r.Transfer],Length=queueLengths[r.Transfer]};CityCrowdManager.Locate(route,r.QueueDistance,0,out p.Position,out p.Yaw);bool walking=Mathf.Abs(r.QueueDistance-previous)>.001f;if(!walking||r.QueueDistance<previous)p.Yaw+=180;p.Wait=walking?0:1;}
  for(int service=0;service<trips.Length;service++)
  {
   var trip=trips[service];bool lift=service==0;
   if(trip.Stage==0)
   {
    if(Time.time<trip.AvailableAfter)continue;Rider first=null;foreach(var candidate in waiting){var link=Transfers[candidate.Transfer];if(link.Vehicle!=(lift?0:1))continue;if(!lift&&(Funicular.NpcCartAt(link.FromUpper)!=trip.Cart||(Funicular.AutoCycle&&Funicular.BoardingTimeRemaining<BoardingSeconds+.25f)))continue;first=candidate;break;}if(first==null)continue;
    var source=Transfers[first.Transfer];if(lift){if(!Elevator.NpcAtStop(source.FromUpper)){Elevator.TryNpcMove(source.FromUpper);continue;}trip.Platform=Elevator.Platform;trip.Deck=Elevator.Deck;}else{trip.Platform=Funicular.Carts[trip.Cart];trip.Deck=Funicular.Decks[trip.Cart];}
    trip.Transfer=first.Transfer;int capacity=lift?4:6;for(int i=0;i<waiting.Count&&trip.Riders.Count<capacity;){var r=waiting[i];if(r.Transfer!=trip.Transfer){i++;continue;}r.Seat=trip.Riders.Count;r.Start=crowd.People[r.Person].Position;r.Progress=0;trip.Riders.Add(r);waiting.RemoveAt(i);BoardedTotal++;}if(trip.Riders.Count==0)continue;trip.Stage=1;trip.Time=0;
   }
   var transfer=Transfers[trip.Transfer];trip.Time+=dt;
   if(trip.Stage==1)
   {
    bool boarded=true;foreach(var r in trip.Riders){r.Progress=Mathf.Min(1,r.Progress+dt/BoardingSeconds);Pose(crowd,r,Vector3.Lerp(r.Start,Seat(trip,r.Seat),r.Progress),r.Progress>=1);if(r.Progress<1)boarded=false;}
    if(boarded){bool departed=lift?Elevator.TryNpcMove(!transfer.FromUpper):Funicular.AutoCycle?Funicular.NpcMoving:Funicular.TryNpcMove(trip.Cart,!transfer.FromUpper);if(departed){trip.Stage=2;trip.Time=0;}}
   }
   else if(trip.Stage==2)
   {
    foreach(var r in trip.Riders)Pose(crowd,r,Seat(trip,r.Seat),true);bool arrived=lift?Elevator.NpcAtStop(!transfer.FromUpper):!Funicular.NpcMoving&&Vector3.Distance(trip.Platform.position,!transfer.FromUpper?Funicular.TopPoints[trip.Cart]:Funicular.BottomPoints[trip.Cart])<.08f;
    if(arrived){foreach(var r in trip.Riders){var points=new List<Vector3>{Seat(trip,r.Seat)};points.AddRange(transfer.Exit);float length=Length(points);exiting.Add(new ExitWalker{Rider=r,Points=points.ToArray(),Length=length,LandingLength=Vector3.Distance(points[0],points[1])});}trip.Riders.Clear();trip.Stage=0;trip.Time=0;trip.AvailableAfter=Time.time+2f;}
   }
  }
 }
 void BuildQueues(CityCrowdManager crowd)
 {
  queuePaths=new Vector3[Transfers.Length][];queueLengths=new float[Transfers.Length];for(int i=0;i<Transfers.Length;i++){var link=Transfers[i];var points=new List<Vector3>();int node=link.FromNode;if(link.ReturnRoute>=0){var route=crowd.Routes[link.ReturnRoute];points.AddRange(route.Corners);node=route.To;}else{points.Add(link.Waiting);points.Add(link.Waiting+link.QueueDirection*7);}
   // Extend short station approaches along actual cached walking paths.
   for(int step=0;step<6&&Length(points)<8.5f;step++){CityCrowdRoute next=null;foreach(var route in crowd.Routes)if(route.From==node&&route.To!=node&&route.Transfer<0&&(next==null||route.CircleNext>=0)){next=route;if(route.CircleNext>=0)break;}if(next==null)break;for(int j=1;j<next.Corners.Length;j++)points.Add(next.Corners[j]);node=next.To;}
   if(points.Count<2)points.Add(points[0]+link.QueueDirection);queuePaths[i]=points.ToArray();queueLengths[i]=Length(points);}
 }
 void TickExits(CityCrowdManager crowd,float dt)
 {
  for(int i=exiting.Count-1;i>=0;i--){var e=exiting[i];var p=crowd.People[e.Rider.Person];float speed=e.Distance<e.LandingLength?Mathf.Max(2.5f,p.Speed):p.Speed;e.Distance+=speed*dt;var route=new CityCrowdRoute{Corners=e.Points,Length=e.Length};CityCrowdManager.Locate(route,e.Distance,0,out var position,out var yaw);Pose(crowd,e.Rider,position,false);p.Yaw=yaw;
   if(e.Distance<e.Length)continue;var transfer=Transfers[e.Rider.Transfer];var choices=new List<int>();for(int j=0;j<crowd.Routes.Length;j++)if(crowd.Routes[j].From==transfer.ToNode&&crowd.Routes[j].Transfer<0&&crowd.Routes[j].From!=crowd.Routes[j].To)choices.Add(j);if(choices.Count==0)continue;p.TripWanted=-1;p.NextTripAt=Time.time+120+e.Rider.Person%180;p.Route=crowd.ChooseNext(e.Rider.Person,transfer.ToNode,choices);p.Distance=0;p.Wait=.5f;passengers.Remove(e.Rider.Person);exiting.RemoveAt(i);CompletedTotal++;}
 }
 static float Length(List<Vector3> points){float length=0;for(int i=1;i<points.Count;i++)length+=Vector3.Distance(points[i-1],points[i]);return length;}
 static Vector3 Seat(Trip trip,int seat){var size=trip.Deck.size;var local=trip.Deck.center+new Vector3(((seat%2)-.5f)*Mathf.Min(.75f,size.x*.32f),size.y*.5f+.02f,((seat/2)-1)*Mathf.Min(.8f,size.z*.25f));return trip.Deck.transform.TransformPoint(local);}
 static void Pose(CityCrowdManager crowd,Rider rider,Vector3 position,bool idle){var p=crowd.People[rider.Person];var direction=position-p.Position;direction.y=0;if(direction.sqrMagnitude>.0001f)p.Yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;p.Position=position;p.Wait=idle?1:0;}
}
