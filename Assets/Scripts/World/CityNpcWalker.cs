using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent))]
public sealed class CityNpcWalker : MonoBehaviour
{
 public Vector3[] Waypoints;
 NavMeshAgent agent;Animator animator;System.Random random;float waitUntil,nextAttempt;bool travelling;int last=-1;
 static readonly int Speed=Animator.StringToHash("Speed");
 void Start(){agent=GetComponent<NavMeshAgent>();animator=GetComponentInChildren<Animator>();random=new System.Random(GetEntityId().GetHashCode());if(!agent.isOnNavMesh&&NavMesh.SamplePosition(transform.position,out NavMeshHit hit,2,NavMesh.AllAreas))agent.Warp(hit.position);waitUntil=Time.time+(float)random.NextDouble()*5;}
 void Update()
 {
  if(!agent||!agent.isOnNavMesh)return;
  if(animator)animator.SetFloat(Speed,agent.velocity.magnitude,.18f,Time.deltaTime);
  if(travelling){if(!agent.pathPending&&(agent.pathStatus!=NavMeshPathStatus.PathComplete||agent.remainingDistance<=agent.stoppingDistance+.12f)){agent.ResetPath();travelling=false;waitUntil=Time.time+2+(float)random.NextDouble()*5;}return;}
  if(Time.time<waitUntil||Time.time<nextAttempt||Waypoints==null||Waypoints.Length<2)return;nextAttempt=Time.time+1;
  for(int attempt=0;attempt<12;attempt++){int index=random.Next(Waypoints.Length);if(index==last||Vector3.Distance(transform.position,Waypoints[index])<5)continue;var path=new NavMeshPath();if(!agent.CalculatePath(Waypoints[index],path)||path.status!=NavMeshPathStatus.PathComplete)continue;agent.SetPath(path);last=index;travelling=true;break;}
 }
}
