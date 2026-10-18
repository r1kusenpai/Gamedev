using UnityEngine;
namespace QueenBattle
{
    public sealed partial class QueenBossBattle
    {
        Vector3 ArenaForward
        {
            get
            {
                Vector3 direction=Vector3.ProjectOnPlane((Arrival?Arrival.position:arena.center)-ThroneSeat.position,Vector3.up);
                return direction.sqrMagnitude>.01f?direction.normalized:RoomRoot.forward;
            }
        }
        Vector3 ArenaRight=>Vector3.Cross(Vector3.up,ArenaForward);
        Vector3 ClampCombatPoint(Vector3 point,float height)
        {
            Vector3 forward=ArenaForward,side=ArenaRight,offset=point-ThroneSeat.position;
            float length=Vector3.ProjectOnPlane((Arrival?Arrival.position:arena.center+forward*10)-ThroneSeat.position,Vector3.up).magnitude;
            float along=Mathf.Clamp(Vector3.Dot(offset,forward),7,Mathf.Max(9,length-4));
            float lateral=Mathf.Clamp(Vector3.Dot(offset,side),-Mathf.Min(8,arena.extents.x-2),Mathf.Min(8,arena.extents.x-2));
            point=ThroneSeat.position+forward*along+side*lateral;
            point.x=Mathf.Clamp(point.x,arena.min.x+2,arena.max.x-2);point.z=Mathf.Clamp(point.z,arena.min.z+2,arena.max.z-2);
            point.y=SurfaceHeightAt(point)+height;return point;
        }
        bool BlocksFlight(Collider collider)
        {
            return collider&&collider!=FloorSupport&&!collider.transform.IsChildOf(transform)
                &&(!Player||!collider.transform.IsChildOf(Player))&&!IsWalkableSurface(collider);
        }
        bool BodyPathClear(Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;
            if(delta.sqrMagnitude>.000001f)
                foreach(var hit in Physics.CapsuleCastAll(from+Vector3.up*.55f,from+Vector3.up*2.2f,.4f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                    if(BlocksFlight(hit.collider))return false;
            foreach(var c in Physics.OverlapCapsule(to+Vector3.up*.55f,to+Vector3.up*2.2f,.4f,~0,QueryTriggerInteraction.Ignore))
                if(BlocksFlight(c))return false;
            return true;
        }
        Vector3 FindOpenArenaPoint(Vector3 desired,float height)
        {
            var point=ClampCombatPoint(desired,height);
            if(BodyPathClear(point,point))return point;
            for(int step=1;step<=5;step++)
                for(int side=-1;side<=1;side++)
                {
                    var option=ClampCombatPoint(desired+ArenaForward*(step*2)+ArenaRight*(side*2),height);
                    if(BodyPathClear(option,option))return option;
                }
            Debug.LogWarning("Queen arena has no clear starting point near the aisle.",this);return point;
        }
        Vector3 FindTravelPoint(Vector3 desired)
        {
            var point=ClampCombatPoint(desired,1.1f);
            if(BodyPathClear(Actor.position,point))return point;
            // Choose a reachable detour instead of repeatedly pushing into a pillar.
            Vector3 direction=Vector3.ProjectOnPlane(point-Actor.position,Vector3.up).normalized;
            if(direction.sqrMagnitude<.01f)direction=ArenaForward;
            foreach(float angle in new[]{45f,-45f,90f,-90f,135f,-135f,180f})
            {
                var option=ClampCombatPoint(Actor.position+Quaternion.AngleAxis(angle,Vector3.up)*direction*3,1.1f);
                if(Vector3.ProjectOnPlane(option-Actor.position,Vector3.up).magnitude>.6f&&BodyPathClear(Actor.position,option))return option;
            }
            return new Vector3(Actor.position.x,GroundHeight+1.1f,Actor.position.z);
        }
        void MovePlayerToBattleStart()
        {
            Vector3 point=FindOpenArenaPoint(ThroneSeat.position+ArenaForward*19,.04f);
            Vector3 facing=Vector3.ProjectOnPlane(Actor.position-point,Vector3.up);
            Quaternion rotation=facing.sqrMagnitude>.01f?Quaternion.LookRotation(facing):Quaternion.LookRotation(-ArenaForward);
            var cc=Player.GetComponent<CharacterController>();bool on=cc&&cc.enabled;if(cc)cc.enabled=false;
            Vector3 delta=point-Player.position;Player.SetPositionAndRotation(point,rotation);if(cc)cc.enabled=on;
            Player.SendMessage("ResetAfterDomainTravel",rotation.eulerAngles.y,SendMessageOptions.DontRequireReceiver);
            if(Camera.main)Camera.main.transform.position+=delta;
            foreach(var b in FindObjectsByType<MonoBehaviour>())
                if(b.GetType().Name=="CinemachineCamera")
                {
                    var method=b.GetType().GetMethod("OnTargetObjectWarped",new[]{typeof(Transform),typeof(Vector3)});
                    method?.Invoke(b,new object[]{Player,delta});
                }
            Physics.SyncTransforms();
        }
    }
}
