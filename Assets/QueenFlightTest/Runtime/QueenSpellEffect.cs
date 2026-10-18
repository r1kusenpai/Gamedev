using UnityEngine;

namespace QueenFlightTest
{
    // Standalone effects: each prefab has its own lifetime and can be reused by attacks.
    public sealed class QueenSpellEffect : MonoBehaviour
    {
        public enum Spell { Fireball, Lightning, LightBlast }
        public Spell Kind;
        public Transform Target;
        public bool UseTargetPoint;
        public Vector3 TargetPoint;
        public bool LightningAsStrike=true;
        public Material GlowMaterial;
        public float Lifetime = 1.4f;
        public float Speed = 8f;
        public bool Finished { get; private set; }
        float age;
        Vector3 aim, start;
        Transform core;
        LineRenderer bolt, branchA, branchB;
        LineRenderer[] rings;
        ParticleSystem sparks;
        Material instanceMaterial;

        void Start()
        {
            start = transform.position;
            aim = UseTargetPoint ? TargetPoint : Target ? Target.position + Vector3.up * .8f : start + transform.forward * 5f;
            instanceMaterial = new Material(GlowMaterial);
            Color color = Kind == Spell.Fireball ? new Color(1f,.22f,.015f) : Kind == Spell.Lightning ? new Color(.18f,.5f,1f) : new Color(1f,.82f,.3f);
            instanceMaterial.color = color * 3f;
            if (instanceMaterial.HasProperty("_EmissionColor")) instanceMaterial.SetColor("_EmissionColor",color*4f);
            if (Kind == Spell.Fireball || Kind == Spell.LightBlast)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name = "Emissive Core";
                Destroy(ball.GetComponent<Collider>()); core=ball.transform; core.SetParent(transform,false);
                core.localScale = Vector3.one * .24f; ball.GetComponent<Renderer>().sharedMaterial=instanceMaterial;
            }
            if (Kind == Spell.Fireball)
            {
                var trail = gameObject.AddComponent<TrailRenderer>(); trail.sharedMaterial=instanceMaterial;
                trail.time=.24f; trail.startWidth=.2f; trail.endWidth=0; trail.minVertexDistance=.025f;
                trail.emitting=true;
                var go=new GameObject("Embers");go.transform.SetParent(transform,false);sparks=go.AddComponent<ParticleSystem>();
                sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=sparks.main;main.duration=1;main.loop=true;main.startLifetime=.35f;main.startSpeed=.35f;main.startSize=.045f;main.startColor=new Color(1,.35f,.03f);main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=sparks.emission;emission.rateOverTime=35;
                var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.08f;
                sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial=instanceMaterial;sparks.Play();
            }
            else if (Kind == Spell.Lightning)
            {
                bolt=Line("Main arc",.045f);branchA=Line("Branch A",.02f);branchB=Line("Branch B",.015f);
            }
            else
            {
                rings=new[]{Line("Outer shockwave",.035f),Line("Inner shockwave",.025f)};
                foreach(var ring in rings)ring.loop=true;
            }
        }

        LineRenderer Line(string name,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=instanceMaterial;line.startWidth=width;line.endWidth=width*.35f;line.useWorldSpace=true;line.numCapVertices=3;return line;
        }

        void Update()
        {
            age+=Time.deltaTime;
            if(age>=Lifetime){Finished=true;Destroy(gameObject);return;}
            float t=age/Lifetime;
            if(Kind==Spell.Fireball)
            {
                transform.position=Vector3.MoveTowards(transform.position,aim,Speed*Time.deltaTime);
                if(core)core.localScale=Vector3.one*(.23f+Mathf.Sin(age*37)*.035f);
                if(Vector3.Distance(transform.position,aim)<.13f){Finished=true;Destroy(gameObject);}
            }
            else if(Kind==Spell.Lightning && bolt)
            {
                Vector3 end=UseTargetPoint?TargetPoint:Target?Target.position+Vector3.up*.8f:aim;
                Vector3 origin=LightningAsStrike?end+Vector3.up*2.4f:start;
                Vector3 direction=(end-origin).normalized,side=Vector3.Cross(direction,Vector3.up).normalized;
                if(side.sqrMagnitude<.1f)side=Vector3.right;
                var points=new Vector3[18];
                int tick=Mathf.FloorToInt(age*28f);
                for(int i=0;i<points.Length;i++)
                {
                    float p=(float)i/(points.Length-1);float envelope=Mathf.Sin(p*Mathf.PI);
                    float a=(Mathf.PerlinNoise(i*4.3f,tick*2.7f)-.5f)*.6f;
                    float b=(Mathf.PerlinNoise(i*7.1f,tick*1.3f+55)-.5f)*.6f;
                    points[i]=Vector3.Lerp(origin,end,p)+(side*a+Vector3.up*b)*envelope;
                }
                bolt.positionCount=points.Length;bolt.SetPositions(points);
                Branch(branchA,points[6],points[10]+Vector3.up*.45f,side);
                Branch(branchB,points[11],points[15]-Vector3.up*.4f,-side);
            }
            else if(Kind==Spell.LightBlast && rings!=null)
            {
                if(core)core.localScale=Vector3.one*Mathf.Max(.005f,Mathf.Sin(Mathf.Min(1,t*2)*Mathf.PI)*.75f);
                for(int j=0;j<rings.Length;j++)
                {
                    float radius=.08f+t*(3f-j*.7f);rings[j].positionCount=65;
                    for(int i=0;i<65;i++){float a=i/64f*Mathf.PI*2;rings[j].SetPosition(i,transform.position+new Vector3(Mathf.Cos(a)*radius,.03f+j*.1f,Mathf.Sin(a)*radius));}
                    rings[j].widthMultiplier=Mathf.Max(.001f,1-t);
                }
            }
        }
        static void Branch(LineRenderer line,Vector3 a,Vector3 b,Vector3 side)
        {line.positionCount=4;line.SetPositions(new[]{a,Vector3.Lerp(a,b,.33f)+side*.15f,Vector3.Lerp(a,b,.66f)-side*.1f,b});}
        void OnDestroy(){if(instanceMaterial)Destroy(instanceMaterial);}
    }
}
