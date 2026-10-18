using UnityEngine;
namespace QueenBattle
{
    [DefaultExecutionOrder(110)]
    public sealed class QueenRoyalProjectile:MonoBehaviour
    {
        public QueenBossBattle Battle;
        public Vector3 Aim;
        public float Damage,Speed,Radius,Delay;
        public int OrbitIndex=-1;
        public bool Heavy;
        public float Curve,CurvePhase;
        public bool Tendril;
        float age,flight;
        Vector3 velocity;
        Vector3 previousCurve;
        TrailRenderer trail;
        public void ResetProjectile(QueenBossBattle owner,Vector3 point,Vector3 target,float damage,float speed,float radius,float delay,int orbit,bool heavy,Material material)
        {
            Battle=owner;Aim=target;Damage=damage;Speed=speed;Radius=radius;Delay=delay;OrbitIndex=orbit;Heavy=heavy;
            age=flight=Curve=CurvePhase=0;Tendril=false;velocity=previousCurve=Vector3.zero;
            transform.position=point;transform.rotation=Quaternion.LookRotation((target-point).sqrMagnitude>.001f?target-point:Vector3.forward);
            transform.localScale=Vector3.one*(heavy?.95f:radius*2);
            GetComponent<Renderer>().sharedMaterial=material;trail=GetComponent<TrailRenderer>();trail.sharedMaterial=material;trail.Clear();trail.time=.18f;trail.startWidth=heavy?.4f:.12f;
            enabled=true;gameObject.SetActive(true);
        }
        void Retire(){if(Battle)Battle.RecycleRoyalProjectile(this);else Destroy(gameObject);}
        void Update()
        {
            if(!Battle||!Battle.Fighting){Retire();return;}
            age+=Time.deltaTime;
            if(age<Delay)
            {
                if(OrbitIndex>=0)
                {float a=OrbitIndex*2.399f+age;float ring=1.1f+(OrbitIndex/12)*.16f;
                    transform.position=Battle.Actor.position+Vector3.up*1.6f+new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.65f,Mathf.Sin(OrbitIndex)*.35f)*ring;}
                return;
            }
            flight+=Time.deltaTime;
            if(velocity.sqrMagnitude<.01f)velocity=(Aim-transform.position).normalized;
            // Mild early steering ends well before contact, so rolls can evade the volley.
            if(OrbitIndex>=0&&flight<.3f&&Vector3.Distance(transform.position,Battle.Player.position)>3)
                velocity=Vector3.RotateTowards(velocity,(Battle.Player.position+Vector3.up*.9f-transform.position).normalized,Time.deltaTime*1.5f,0);
            Vector3 start=transform.position,delta=velocity*Speed*Time.deltaTime;
            if(Curve!=0)
            {
                Vector3 side=Vector3.Cross(Vector3.up,velocity).normalized;
                Vector3 offset=side*Mathf.Sin(flight*5+CurvePhase)*Curve;
                delta+=offset-previousCurve;previousCurve=offset;
            }
            if(Tendril&&trail)trail.time=.65f;
            var hits=Physics.SphereCastAll(start,Radius,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var h in hits)
            {
                if(h.transform.IsChildOf(Battle.transform))continue;
                transform.position=start+delta.normalized*h.distance;
                if(Heavy)Battle.RoyalExplosion(transform.position,Damage);
                else if(h.transform.IsChildOf(Battle.Player))Battle.ReceivePlayerDamage(Damage);
                Retire();return;
            }
            transform.position+=delta;
            if(Vector3.Distance(transform.position,Aim)<Mathf.Max(.3f,delta.magnitude))
            {
                if(Heavy)Battle.RoyalExplosion(Aim,Damage);
                else if(Vector3.Distance(transform.position,Battle.Player.position+Vector3.up*.9f)<.65f)Battle.ReceivePlayerDamage(Damage);
                Retire();
            }
            else if(flight>5)Retire();
        }
    }
}
