using System.Collections.Generic;
using UnityEngine;
namespace QueenBattle
{
    public sealed partial class QueenBossBattle
    {
        public enum ReferenceMove { SurroundingOrbs,GroundSpikes,ProjectileRain,TwinDarkSpheres,DarkVolley,SerpentStream,ShadowRush,WarpBladeCombo,RedBladeFan,TendrilOrbs,PanelTrap,DiveSlam,ElectricCross,DomeBarrage }
        public ReferenceMove CurrentReferenceMove {get;private set;}
        public int ReferenceMovesCompleted {get;private set;}
        public readonly int[] ReferenceUses=new int[14];
        public int FireballsSpawned {get;private set;}
        public int LightningSpawned {get;private set;}
        public int HeavyOrbsSpawned {get;private set;}
        public string ReferenceAttackName=>CurrentReferenceMove.ToString();
        int referenceIndex;
        Vector3 rushPoint;
        bool referenceRush;
        readonly Queue<QueenRoyalProjectile> projectilePool=new Queue<QueenRoyalProjectile>();
        Material fireMaterial,darkMaterial,redMaterial,blueMaterial;
        Mesh orbMesh;
        // Every observed attack family is available before the half-health transition.
        readonly ReferenceMove[] referenceOrder={ReferenceMove.SurroundingOrbs,ReferenceMove.WarpBladeCombo,ReferenceMove.GroundSpikes,ReferenceMove.RedBladeFan,ReferenceMove.ProjectileRain,ReferenceMove.TendrilOrbs,ReferenceMove.TwinDarkSpheres,ReferenceMove.PanelTrap,ReferenceMove.DarkVolley,ReferenceMove.DiveSlam,ReferenceMove.SerpentStream,ReferenceMove.ElectricCross,ReferenceMove.ShadowRush,ReferenceMove.DomeBarrage};
        void ResetReferencePhase()
        {
            referenceIndex=ReferenceMovesCompleted=FireballsSpawned=LightningSpawned=HeavyOrbsSpawned=0;referenceRush=false;
            for(int i=0;i<ReferenceUses.Length;i++)ReferenceUses[i]=0;
        }
        void TickDelayedSpells()
        {
            // Remove due work before invoking it: callbacks may schedule more work or finish the battle.
            for(int i=pending.Count-1;i>=0;i--)
                if(Time.time>=pending[i].Due){var fire=pending[i].Fire;pending.RemoveAt(i);fire();if(!Fighting)return;}
        }
        void TickReferencePhase()
        {
            TickDelayedSpells();if(!Fighting)return;
            if(State==Phase.Moving&&TryArrowDodge())return;
            var facing=Vector3.ProjectOnPlane((State==Phase.Casting?lockedAim:Player.position)-Actor.position,Vector3.up);
            if(facing.sqrMagnitude>.01f)Actor.rotation=Quaternion.Slerp(Actor.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-8*ResponseSpeed*Time.deltaTime));
            switch(State)
            {
                case Phase.Rising:
                    MoveTo(Actor.position.WithY(GroundHeight+1.1f),1.6f*ResponseSpeed);
                    if(phaseTime>1.2f/ResponseSpeed)BeginReferenceTravel();break;
                case Phase.Moving:
                    MoveTo(destination,3.2f*ResponseSpeed);
                    if(phaseTime>1.5f/ResponseSpeed)BeginReferenceCast();break;
                case Phase.Dodge:
                    MoveTo(dodgeTarget,15*ResponseSpeed);
                    if(phaseTime>animationPlayer.Duration/ResponseSpeed+.04f){arrowDodge=false;BeginReferenceTravel();}break;
                case Phase.Casting:
                    float duration=animationPlayer.Duration/ResponseSpeed;
                    if(!released&&phaseTime>duration*.4f){released=true;ReleaseReferenceAttack();}
                    if(referenceRush)MoveTo(rushPoint,12*ResponseSpeed);
                    if(phaseTime>duration+ReferenceTail(CurrentReferenceMove))
                    {
                        referenceRush=false;CompletedAttacks++;ReferenceMovesCompleted++;
                        animationPlayer.PlaybackSpeed=1;SetPhase(Phase.Recovery,0);
                    }
                    break;
                case Phase.Recovery:
                    MoveTo(Actor.position.WithY(GroundHeight+.04f),2.4f*ResponseSpeed);
                    if(phaseTime>3.4f/ResponseSpeed){referenceIndex++;BeginReferenceTravel();}break;
            }
        }
        float ReferenceTail(ReferenceMove move)=>move==ReferenceMove.DomeBarrage?5:move==ReferenceMove.DarkVolley?2.5f:move==ReferenceMove.SerpentStream?2:move==ReferenceMove.WarpBladeCombo?1.2f:.35f;
        void BeginReferenceTravel()
        {
            float side=referenceIndex%2==0?1:-1;
            destination=FindTravelPoint(Player.position-ArenaForward*5+ArenaRight*side*3);
            animationPlayer.PlaybackSpeed=ResponseSpeed;SetPhase(Phase.Moving,1);
        }
        void BeginReferenceCast()
        {
            CurrentReferenceMove=referenceOrder[referenceIndex%referenceOrder.Length];lockedAim=Player.position+Vector3.up*.9f;released=false;
            animationPlayer.PlaybackSpeed=ResponseSpeed;
            bool heavy=CurrentReferenceMove==ReferenceMove.DomeBarrage||CurrentReferenceMove==ReferenceMove.DarkVolley||CurrentReferenceMove==ReferenceMove.DiveSlam;
            SetPhase(Phase.Casting,heavy?6:referenceIndex%2==0?4:5);
        }
        void ReleaseReferenceAttack()
        {
            bossAudio.PlayAttack();
            ReferenceUses[(int)CurrentReferenceMove]++;
            Vector3 chest=lockedAim,ground=chest.WithY(SurfaceHeightAt(chest)+.08f),hand=rightHand?rightHand.position:Actor.position+Vector3.up*1.5f;
            switch(CurrentReferenceMove)
            {
                case ReferenceMove.SurroundingOrbs:
                    for(int i=0;i<10;i++)
                    {
                        float a=i*Mathf.PI*.2f;Vector3 p=chest+new Vector3(Mathf.Cos(a),.3f,Mathf.Sin(a))*4;
                        ReferenceOrb(p,chest,14,9,.2f,.65f+i*.07f,0);
                    }break;
                case ReferenceMove.GroundSpikes:
                    for(int i=0;i<10;i++)
                    {float a=i*Mathf.PI*.2f;Vector3 p=ground+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*2;Hazard(QueenReferenceHazard.Form.Spike,p,p,.6f,18,.55f,1.1f);}
                    Later(1.15f,()=>RoyalExplosion(ground,22));break;
                case ReferenceMove.ProjectileRain:
                    for(int i=0;i<16;i++)
                    {int shot=i;Later(i*.055f,()=>{float a=shot*2.4f;Vector3 p=chest+new Vector3(Mathf.Cos(a)*2.5f,5,Mathf.Sin(a)*2.5f);ReferenceOrb(p,p.WithY(ground.y),14,12,.18f,0,1);});}break;
                case ReferenceMove.TwinDarkSpheres:
                    for(int i=-1;i<=1;i+=2)
                    {Vector3 p=hand+ArenaRight*i*2;var orb=ReferenceOrb(p,chest,24,6,.8f,.65f,0);orb.transform.localScale=Vector3.one*1.6f;}break;
                case ReferenceMove.DarkVolley:
                    Hazard(QueenReferenceHazard.Form.DarkZone,ground,ground,7,0,0,4);
                    for(int i=0;i<6;i++)
                    {int wave=i;Later(i*.38f,()=>{for(int j=-1;j<=1;j++)ReferenceOrb(Actor.position+Vector3.up*1.5f+ArenaRight*j*.6f,Player.position+Vector3.up*.9f,14,11,.19f,0,0);Vector3 p=Player.position;Hazard(QueenReferenceHazard.Form.Spike,p,p,.8f,18,.5f,.5f);});}break;
                case ReferenceMove.SerpentStream:
                    for(int i=0;i<30;i++)
                    {int shot=i;Later(i*.065f,()=>{var p=ReferenceOrb(Actor.position+Vector3.up*1.5f,chest+ArenaRight*Mathf.Sin(shot*.4f)*2.2f,13,9,.23f,0,0);p.Curve=1.2f;p.CurvePhase=shot*.35f;});}break;
                case ReferenceMove.ShadowRush:
                    BeginRush(chest);Hazard(QueenReferenceHazard.Form.Slash,Actor.position,chest.WithY(ground.y),1,22,.35f,.35f);break;
                case ReferenceMove.WarpBladeCombo:
                    for(int i=0;i<3;i++)
                    {int step=i;Later(i*.4f,()=>{rushPoint=FindTravelPoint(Player.position+ArenaRight*(step%2==0?1.5f:-1.5f));referenceRush=true;animationPlayer.Play(step%2==0?4:5);
                        var p=Player.position;Hazard(QueenReferenceHazard.Form.Slash,p-ArenaRight*2,p+ArenaRight*2,.6f,18,.3f,.18f);});}break;
                case ReferenceMove.RedBladeFan:
                    for(int i=-3;i<=3;i++)
                    {Vector3 aim=hand+Quaternion.AngleAxis(i*11,Vector3.up)*(chest-hand);var p=ReferenceOrb(hand,aim,16,13,.24f,.3f,1);p.transform.localScale=new Vector3(.16f,.18f,1.5f);}
                    break;
                case ReferenceMove.TendrilOrbs:
                    for(int i=-1;i<=1;i+=2)
                    {var p=ReferenceOrb(hand+ArenaRight*i*1.4f,chest+ArenaRight*i,18,6,.55f,.5f,2);p.Curve=.7f;p.CurvePhase=i;p.Tendril=true;}
                    break;
                case ReferenceMove.PanelTrap:
                    for(int i=-1;i<=1;i+=2)
                    {Vector3 p=ground+ArenaRight*i*2.3f;Hazard(QueenReferenceHazard.Form.Panel,p-ArenaForward*1.3f,p+ArenaForward*1.3f,.3f,14,.6f,2.3f);}
                    Later(.85f,()=>{for(int i=-1;i<=1;i++)ReferenceOrb(hand,chest+ArenaRight*i*.75f,14,11,.2f,0,1);});break;
                case ReferenceMove.DiveSlam:
                    rushPoint=FindTravelPoint(ground);referenceRush=true;WarnAt(ground,2.8f,.75f);Later(.65f,()=>RoyalExplosion(ground,26));break;
                case ReferenceMove.ElectricCross:
                    Hazard(QueenReferenceHazard.Form.Electric,ground-ArenaRight*4,ground+ArenaRight*4,.45f,20,.65f,.3f);
                    Hazard(QueenReferenceHazard.Form.Electric,ground-ArenaForward*4,ground+ArenaForward*4,.45f,20,1.05f,.3f);break;
                case ReferenceMove.DomeBarrage:
                    Hazard(QueenReferenceHazard.Form.Dome,ground,ground,8,0,0,5.5f);
                    for(int wave=0;wave<12;wave++)
                    {int w=wave;Later(.5f+wave*.35f,()=>{Vector3 target=Player.position+Vector3.up*.9f;for(int j=0;j<8;j++)
                        {float a=j*Mathf.PI*.25f+w*.17f;Vector3 p=ground+new Vector3(Mathf.Cos(a)*7,1.2f+(w%3)*.6f,Mathf.Sin(a)*7);ReferenceOrb(p,target,12,11,.17f,0,1);}});}break;
            }
        }
        void BeginRush(Vector3 target){rushPoint=FindTravelPoint(target);referenceRush=true;}
        QueenRoyalProjectile ReferenceOrb(Vector3 point,Vector3 aim,float damage,float speed,float radius,float delay,int palette)
        {return CreateRoyalProjectile(point,aim,damage,speed,radius,delay,-1,false,palette);}
        void Hazard(QueenReferenceHazard.Form form,Vector3 from,Vector3 to,float radius,float damage,float delay,float duration)
        {
            from.y=SurfaceHeightAt(from)+.08f;to.y=SurfaceHeightAt(to)+.08f;
            if(damage>0)WarnAt((from+to)*.5f,form==QueenReferenceHazard.Form.Spike?radius:Mathf.Max(radius,Vector3.Distance(from,to)*.5f),delay+.1f);
            var go=new GameObject("Queen "+form);go.transform.SetParent(transform,false);
            var hazard=go.AddComponent<QueenReferenceHazard>();hazard.Initialize(this,form,from,to,radius,damage,delay,duration,ProjectileMaterial(form==QueenReferenceHazard.Form.Electric?2:1));
        }
        Vector3 FindSummonPoint(int slot)
        {
            for(int attempt=0;attempt<30;attempt++)
            {
                float angle=(slot+attempt)*Mathf.PI/3;float distance=3.2f+(attempt/6)*1.2f;
                Vector3 p=FindOpenArenaPoint(Actor.position+(ArenaRight*Mathf.Cos(angle)+ArenaForward*Mathf.Sin(angle))*distance,1.1f);
                bool near=false;foreach(var ghost in phantoms)if(ghost&&Vector3.ProjectOnPlane(ghost.transform.position-p,Vector3.up).sqrMagnitude<1.5f*1.5f){near=true;break;}
                if(!near&&BodyPathClear(p,p))return p;
            }
            return FindOpenArenaPoint(Actor.position+ArenaForward*(slot+2),1.1f);
        }
        Material ProjectileMaterial(int palette)
        {
            if(!fireMaterial)
            {
                fireMaterial=new Material(TelegraphMaterial);fireMaterial.color=new Color(3,.45f,.03f);
                darkMaterial=new Material(TelegraphMaterial);darkMaterial.color=new Color(1.2f,.04f,2);
                redMaterial=new Material(TelegraphMaterial);redMaterial.color=new Color(3,.05f,.1f);
                blueMaterial=new Material(TelegraphMaterial);blueMaterial.color=new Color(.15f,1.5f,3);
                var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);orbMesh=sphere.GetComponent<MeshFilter>().sharedMesh;sphere.SetActive(false);Destroy(sphere);
            }
            return palette==0?darkMaterial:palette==1?redMaterial:palette==2?blueMaterial:fireMaterial;
        }
        QueenRoyalProjectile CreateRoyalProjectile(Vector3 point,Vector3 aim,float damage,float speed,float radius,float delay,int orbit,bool heavy,int palette=3)
        {
            bossAudio.PlayAttack();
            Material material=ProjectileMaterial(palette);QueenRoyalProjectile p=null;
            while(projectilePool.Count>0&&!p)p=projectilePool.Dequeue();
            if(!p)
            {
                var go=new GameObject("Queen pooled magic");go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=orbMesh;go.AddComponent<MeshRenderer>();
                var trail=go.AddComponent<TrailRenderer>();trail.time=.18f;trail.startWidth=.12f;trail.endWidth=0;trail.minVertexDistance=.07f;
                p=go.AddComponent<QueenRoyalProjectile>();
            }
            p.ResetProjectile(this,point,aim,damage,speed,radius,delay,orbit,heavy,material);FireballsSpawned++;if(heavy)HeavyOrbsSpawned++;return p;
        }
        public void RecycleRoyalProjectile(QueenRoyalProjectile projectile)
        {projectile.enabled=false;projectile.gameObject.SetActive(false);projectilePool.Enqueue(projectile);}
        void ClearReferenceEffects()
        {
            projectilePool.Clear();
            foreach(var h in GetComponentsInChildren<QueenReferenceHazard>(true)){h.enabled=false;Destroy(h.gameObject);}
        }
        void OnDestroy()
        {if(fireMaterial)Destroy(fireMaterial);if(darkMaterial)Destroy(darkMaterial);if(redMaterial)Destroy(redMaterial);if(blueMaterial)Destroy(blueMaterial);}
    }
    static class QueenVectorExtensions { public static Vector3 WithY(this Vector3 v,float y)=>new Vector3(v.x,y,v.z); }
}
