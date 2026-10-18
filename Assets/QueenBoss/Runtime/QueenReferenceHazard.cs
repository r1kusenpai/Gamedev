using UnityEngine;
namespace QueenBattle
{
    public sealed class QueenReferenceHazard:MonoBehaviour
    {
        public enum Form { Spike,Panel,Slash,Electric,DarkZone,Dome }
        public Form Shape {get;private set;}
        QueenBossBattle battle;
        Vector3 from,to;
        float radius,damage,delay,duration,age;
        bool hit;
        Transform visual;
        Collider panel;
        LineRenderer electric;
        public void Initialize(QueenBossBattle owner,Form form,Vector3 a,Vector3 b,float size,float power,float wait,float life,Material material)
        {
            battle=owner;Shape=form;from=a;to=b;radius=size;damage=power;delay=wait;duration=life;
            if(form==Form.Electric||form==Form.Slash)
            {
                var go=new GameObject(form+" arc");go.transform.SetParent(transform,false);electric=go.AddComponent<LineRenderer>();electric.sharedMaterial=material;
                electric.startWidth=electric.endWidth=form==Form.Slash?.14f:.07f;electric.positionCount=16;electric.useWorldSpace=true;
            }
            else if(form==Form.Dome)
            {
                for(int arc=0;arc<12;arc++)
                {
                    var go=new GameObject("Barrage boundary");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.startWidth=line.endWidth=.025f;line.positionCount=33;
                    float direction=arc*Mathf.PI/12;
                    for(int i=0;i<33;i++){float angle=i*Mathf.PI/32;line.SetPosition(i,a+new Vector3(Mathf.Cos(direction)*Mathf.Cos(angle),Mathf.Sin(angle),Mathf.Sin(direction)*Mathf.Cos(angle))*size);}
                }
            }
            else
            {
                var go=GameObject.CreatePrimitive(form==Form.Panel?PrimitiveType.Cube:form==Form.Spike?PrimitiveType.Capsule:PrimitiveType.Cylinder);
                go.name=form+" magic";go.transform.SetParent(transform,false);visual=go.transform;
                go.GetComponent<Renderer>().sharedMaterial=material;visual.position=(a+b)*.5f;
                if(form==Form.Panel)
                {
                    visual.position+=Vector3.up*1.4f;visual.rotation=Quaternion.LookRotation((b-a).normalized);
                    visual.localScale=new Vector3(.22f,2.8f,Mathf.Max(1,Vector3.Distance(a,b)));panel=go.GetComponent<Collider>();panel.enabled=false;
                }
                else
                {
                    Destroy(go.GetComponent<Collider>());
                    visual.localScale=form==Form.Spike?new Vector3(size*.6f,1,size*.6f):new Vector3(size*2,.015f,size*2);
                    if(form==Form.Spike)visual.position+=Vector3.up;
                }
            }
        }
        void Update()
        {
            if(!battle||!battle.Fighting){Destroy(gameObject);return;}
            age+=Time.deltaTime;if(age>delay+duration){Destroy(gameObject);return;}
            bool active=age>=delay;if(panel)panel.enabled=active;
            if(visual&&Shape==Form.Spike)visual.localScale=new Vector3(radius*.6f,active?1:.04f,radius*.6f);
            if(electric)
            {
                float height=Shape==Form.Slash?1.1f:.15f;
                for(int i=0;i<16;i++)
                {
                    Vector3 p=Vector3.Lerp(from,to,i/15f)+Vector3.up*height;
                    if(active&&Shape==Form.Electric)p+=Vector3.up*(Mathf.PerlinNoise(i*7,Time.time*35)*.3f);
                    electric.SetPosition(i,p);
                }
                electric.widthMultiplier=active?1:.18f;
            }
            if(!active||hit||damage<=0||!battle.Player)return;
            Vector3 player=battle.Player.position;Vector3 ab=Vector3.ProjectOnPlane(to-from,Vector3.up);
            float t=ab.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(player-from,Vector3.up),ab)/ab.sqrMagnitude):0;
            Vector3 closest=from+ab*t;
            if(Vector3.ProjectOnPlane(player-closest,Vector3.up).magnitude>radius+.3f||Mathf.Abs(player.y-closest.y)>2.5f)return;
            Vector3 start=closest+Vector3.up,delta=player+Vector3.up*.9f-start;
            foreach(var h in Physics.RaycastAll(start,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!h.transform.IsChildOf(battle.transform)&&!h.transform.IsChildOf(battle.Player))return;
            hit=true;battle.ReceivePlayerDamage(damage);
        }
    }
}
