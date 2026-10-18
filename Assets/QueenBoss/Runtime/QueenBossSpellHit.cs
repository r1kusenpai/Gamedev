using UnityEngine;
namespace QueenBattle
{
    [DefaultExecutionOrder(100)]
    public sealed class QueenBossSpellHit : MonoBehaviour
    {
        public QueenBossBattle Battle;
        public int Kind;
        public Vector3 Aim;
        public float Damage;
        Vector3 previous;
        bool fired;
        float age;
        void Start(){previous=transform.position;}
        void LateUpdate()
        {
            if(!Battle||!Battle.Fighting){Destroy(gameObject);return;}
            age+=Time.deltaTime;
            if(fired||!Battle.Player)return;
            var chest=Battle.Player.position+Vector3.up*.9f;
            if(Kind==0)
            {
                var delta=transform.position-previous;
                var hits=Physics.SphereCastAll(previous,.18f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                foreach(var hit in hits)
                {
                    if(hit.transform.IsChildOf(Battle.Actor)||hit.transform.IsChildOf(transform))continue;
                    if(hit.transform.IsChildOf(Battle.Player)){fired=true;Battle.ReceivePlayerDamage(Damage);Destroy(gameObject);return;}
                    // Fireballs stop on scenery rather than travelling through pillars.
                    fired=true;Destroy(gameObject);return;
                }
                if(Vector3.Distance(transform.position,chest)<.55f){fired=true;Battle.ReceivePlayerDamage(Damage);Destroy(gameObject);}
                previous=transform.position;
            }
            else if(age>.08f)
            {
                fired=true;
                if(Kind==1)
                {
                    // A short vertical strike at the marked point, never a queen-to-player beam.
                    Vector3 a=Aim+Vector3.up*2.4f,b=Aim+Vector3.up*.03f,ab=b-a;
                    bool blocked=false;
                    foreach(var h in Physics.RaycastAll(a,ab.normalized,ab.magnitude,~0,QueryTriggerInteraction.Ignore))
                        if(!h.transform.IsChildOf(Battle.Actor)&&!h.transform.IsChildOf(Battle.Player)&&!h.transform.IsChildOf(transform)){blocked=true;break;}
                    float distance=Vector3.ProjectOnPlane(chest-Aim,Vector3.up).magnitude;
                    if(!blocked&&distance<.9f&&chest.y>Aim.y-.2f&&chest.y<Aim.y+2.8f)Battle.ReceivePlayerDamage(Damage);
                }
                else if(Vector3.ProjectOnPlane(Battle.Player.position-Aim,Vector3.up).magnitude<2.8f)Battle.ReceivePlayerDamage(Damage);
            }
        }
    }
}
