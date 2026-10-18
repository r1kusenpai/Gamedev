using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using QueenFlightTest;

namespace QueenBattle
{
    public sealed partial class QueenBossBattle
    {
        public bool SecondPhase {get;private set;}
        public int SummonsCreated {get;private set;}
        public int SecondPhaseAttacks {get;private set;}
        public int ReactiveDodges {get;private set;}
        [Range(0,1)] public float ArrowDodgeChance=.35f;
        struct DelayedSpell { public float Due; public Action Fire; }
        readonly List<DelayedSpell> pending=new List<DelayedSpell>();
        readonly List<GameObject> phantoms=new List<GameObject>();
        readonly HashSet<Behaviour> consideredArrows=new HashSet<Behaviour>();
        Type arrowType;
        FieldInfo arrowOwner,arrowVelocity;
        float nextArrowScan,nextArrowDodge,nextSummons,nextCloseBurst;
        int royalMove,royalCycle;
        Vector3 dodgeTarget;
        bool arrowDodge;
        // A predictable heavy finisher follows several overlapping threats.
        readonly int[][] patterns={new[]{0,1,2,3,4},new[]{0,3,1,2,4},new[]{1,0,2,3,4}};
        int moveIndex;

        void ResetSecondPhase()
        {
            SecondPhase=false;SummonsCreated=SecondPhaseAttacks=ReactiveDodges=royalCycle=moveIndex=0;
            nextArrowDodge=nextArrowScan=nextSummons=nextCloseBurst=0;arrowDodge=false;consideredArrows.Clear();
            animationPlayer.PlaybackSpeed=1;
        }
        void EnterSecondPhase()
        {
            ClearSpells();SecondPhase=true;ClearTelegraph();moveIndex=0;royalCycle=0;bossAudio.PlayShield();
            BeginRoyalTravel();
        }
        void Later(float delay,Action action){pending.Add(new DelayedSpell{Due=Time.time+delay,Fire=action});}
        void ClearSecondPhaseEffects()
        {
            ClearReferenceEffects();
            pending.Clear();
            foreach(var ghost in phantoms)if(ghost)Destroy(ghost);phantoms.Clear();
            foreach(var projectile in GetComponentsInChildren<QueenRoyalProjectile>(true))
            {projectile.enabled=false;Destroy(projectile.gameObject);}
            foreach(var warning in GetComponentsInChildren<QueenGroundWarning>(true))Destroy(warning.gameObject);
        }
        Vector3 ArenaPoint(Vector3 point,float height=1.1f)
        {
            return ClampCombatPoint(point,height);
        }
        void BeginRoyalTravel()
        {
            float side=(moveIndex+royalCycle)%2==0?1:-1;
            destination=FindTravelPoint(Player.position-ArenaForward*5+ArenaRight*side*3);
            animationPlayer.PlaybackSpeed=ResponseSpeed;SetPhase(Phase.Moving,1);
        }
        void BeginRoyalCast()
        {
            royalMove=patterns[royalCycle%patterns.Length][moveIndex];
            lockedAim=Player.position+Vector3.up*.9f;released=false;
            animationPlayer.PlaybackSpeed=ResponseSpeed;
            SetPhase(Phase.Casting,royalMove==4?6:royalMove==3?5:4);
            if(royalMove==4)WarnAt(lockedAim,2.8f,Clips[6].length/ResponseSpeed+3);
        }
        void TickSecondPhase()
        {
            TickDelayedSpells();if(!Fighting)return;
            phantoms.RemoveAll(g=>!g);
            Vector3 toward=Vector3.ProjectOnPlane((State==Phase.Casting?lockedAim:Player.position)-Actor.position,Vector3.up);
            if(toward.sqrMagnitude>.01f)Actor.rotation=Quaternion.Slerp(Actor.rotation,Quaternion.LookRotation(toward),1-Mathf.Exp(-10*ResponseSpeed*Time.deltaTime));
            if(State==Phase.Moving&&TryArrowDodge())return;
            if(State==Phase.Moving)
            {
                if(Time.time>=nextCloseBurst&&Vector3.ProjectOnPlane(Player.position-Actor.position,Vector3.up).magnitude<2)
                {
                    nextCloseBurst=Time.time+10;Vector3 burst=Actor.position;burst.y=SurfaceHeightAt(burst)+.08f;
                    WarnAt(burst,2.8f,.6f);Later(.55f,()=>SpawnGroundSpell(2,burst,16));
                }
                MoveTo(destination,4.5f*ResponseSpeed);
                if(phaseTime>.55f/ResponseSpeed)BeginRoyalCast();
            }
            else if(State==Phase.Dodge&&arrowDodge)
            {
                MoveTo(dodgeTarget,15*ResponseSpeed);
                if(phaseTime>animationPlayer.Duration/ResponseSpeed+.06f){arrowDodge=false;BeginRoyalTravel();}
            }
            else if(State==Phase.Casting)
            {
                float duration=animationPlayer.Duration/animationPlayer.PlaybackSpeed;
                if(!released&&phaseTime>=duration*(royalMove==4?.58f:.36f))
                {released=true;ReleaseRoyalMove();}
                if(phaseTime>=duration+.05f)
                {
                    CompletedAttacks++;SecondPhaseAttacks++;moveIndex++;
                    if(moveIndex>=patterns[royalCycle%patterns.Length].Length)
                    {animationPlayer.PlaybackSpeed=1;SetPhase(Phase.Recovery,0);}
                    else BeginRoyalTravel();
                }
            }
            else if(State==Phase.Recovery)
            {
                MoveTo(new Vector3(Actor.position.x,GroundHeight+.04f,Actor.position.z),3.5f*ResponseSpeed);
                if(phaseTime>Mathf.Max(.4f,1/ResponseSpeed)){royalCycle++;moveIndex=0;BeginRoyalTravel();}
            }
        }
        void ReleaseRoyalMove()
        {
            bossAudio.PlayAttack();
            Vector3 hand=rightHand?rightHand.position:Actor.position+Vector3.up*1.5f;
            if(royalMove==0)
            {
                for(int i=0;i<6*PhaseTwoSpellMultiplier;i++)
                {
                    float a=i*Mathf.PI/3;
                    LaunchOrb(Actor.position+Vector3.up*1.6f+new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.65f,0)*1.1f,
                        lockedAim,14,8,.18f,.4f+i*.035f,i);
                }
            }
            else if(royalMove==1)
            {
                var forward=(lockedAim-hand).normalized;
                for(int wave=0;wave<PhaseTwoSpellMultiplier;wave++)
                {int index=wave;Later(wave*.08f,()=>{for(int i=-1;i<=1;i++)
                    {Vector3 aim=hand+Quaternion.AngleAxis(i*13+(index%2==0?-3:3),Vector3.up)*forward*Vector3.Distance(hand,lockedAim);LaunchOrb(hand,aim,12,10,.18f);}});}
            }
            else if(royalMove==2)
            {
                if(Time.time>=nextSummons){SummonAssistants();nextSummons=Time.time+12;}
                else {for(int i=0;i<PhaseTwoSpellMultiplier;i++){Later(i*.06f,()=>LaunchOrb(hand,lockedAim,14,9,.22f));}}
            }
            else if(royalMove==3)
            {
                for(int i=0;i<3*PhaseTwoSpellMultiplier;i++)
                {
                    int index=i;Later(i*.09f,()=>
                    {
                        // Lock each warning before impact; running never drags a live strike.
                        Vector3 point=Player.position;
                        if(index%3!=0){float angle=index*2.399f;point+=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*1.4f;}
                        point.y=SurfaceHeightAt(point)+.08f;
                        WarnAt(point,.9f,.75f);
                        Later(.65f,()=>SpawnGroundSpell(1,point,18));
                    });
                }
            }
            else for(int i=0;i<PhaseTwoSpellMultiplier;i++)
            {int shot=i;Later(i*.09f,()=>LaunchOrb(hand,lockedAim+ArenaRight*((shot-(PhaseTwoSpellMultiplier-1)*.5f)*.35f),26,5.5f,.48f,0,-1,true));}
        }
        void LaunchOrb(Vector3 point,Vector3 aim,float damage,float speed,float radius,float delay=0,int orbit=-1,bool heavy=false)
        {
            CreateRoyalProjectile(point,aim,damage,speed,radius,delay,orbit,heavy);
        }
        void SpawnGroundSpell(int kind,Vector3 point,float damage)
        {
            bossAudio.PlayAttack();
            if(kind==1)LightningSpawned++;
            var go=Instantiate(Spells[kind],point,Quaternion.identity,transform);
            var effect=go.GetComponent<QueenSpellEffect>();effect.UseTargetPoint=true;effect.TargetPoint=point;effect.LightningAsStrike=true;
            var hit=go.AddComponent<QueenBossSpellHit>();hit.Battle=this;hit.Kind=kind;hit.Aim=point;hit.Damage=damage;
        }
        public void RoyalExplosion(Vector3 point,float damage)
        {point.y=SurfaceHeightAt(point)+.08f;SpawnGroundSpell(2,point,damage);}
        void WarnAt(Vector3 point,float radius,float lifetime)
        {
            var go=new GameObject("Queen ground warning");go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=TelegraphMaterial;line.startWidth=line.endWidth=.045f;line.loop=true;line.useWorldSpace=true;line.positionCount=64;
            float y=SurfaceHeightAt(point)+.025f;
            for(int i=0;i<64;i++){float a=i*Mathf.PI/32;line.SetPosition(i,new Vector3(point.x+Mathf.Cos(a)*radius,y,point.z+Mathf.Sin(a)*radius));}
            go.AddComponent<QueenGroundWarning>();Destroy(go,lifetime);
        }
        void SummonAssistants()
        {
            bossAudio.PlayShield();
            phantoms.RemoveAll(g=>!g);
            for(int i=phantoms.Count;i<PhaseTwoSummons;i++)
            {
                Vector3 point=FindSummonPoint(i);
                if(!BodyPathClear(point,point))continue;
                var go=new GameObject("Spectral queen assistant");go.transform.SetParent(transform,false);go.transform.SetPositionAndRotation(point,Actor.rotation);
                var model=Instantiate(QueenPrefab,go.transform);
                foreach(var c in model.GetComponentsInChildren<Collider>())c.enabled=false;
                var anim=go.AddComponent<QueenBossAnimation>();anim.Initialize(model.GetComponentInChildren<Animator>(),Clips);anim.PlaybackSpeed=1.4f;anim.Play(i==0?4:5);
                var ghost=go.AddComponent<QueenSpectralAssistant>();ghost.Initialize(this,model);
                phantoms.Add(go);SummonsCreated++;
                for(int shot=0;shot<2;shot++)
                {
                    var source=go.transform;float delay=1+shot*1.8f+i*.35f;
                    Later(delay,()=>{if(!source)return;source.LookAt(new Vector3(Player.position.x,source.position.y,Player.position.z));anim.Play(4);
                        for(int j=0;j<PhaseTwoSpellMultiplier;j++){int shot=j;Later(j*.07f,()=>{if(source)LaunchOrb(source.position+Vector3.up*1.5f,Player.position+Vector3.up*.9f+ArenaRight*((shot%3-1)*.4f),10,7,.17f);});}});
                }
                Destroy(go,6);
            }
        }
        bool ClearBodyPath(Vector3 from,Vector3 to)
        {
            return BodyPathClear(from,to);
        }
        bool TryArrowDodge()
        {
            if(Time.time<nextArrowScan||Time.time<nextArrowDodge)return false;nextArrowScan=Time.time+.08f/ResponseSpeed;
            if(arrowType==null)
            {
                arrowType=Type.GetType("StarterAssets.WeaponArrow, Unity.StarterAssets");if(arrowType==null)return false;
                arrowOwner=arrowType.GetField("Owner");arrowVelocity=arrowType.GetField("Velocity");
            }
            if(arrowOwner==null||arrowVelocity==null)return false;
            consideredArrows.RemoveWhere(a=>!a);
            foreach(var obj in UnityEngine.Object.FindObjectsByType(arrowType))
            {
                var arrow=obj as Behaviour;if(!arrow||!arrow.enabled)continue;
                var owner=arrowOwner.GetValue(arrow) as Transform;if(!owner||!(owner==Player||owner.IsChildOf(Player)))continue;
                Vector3 velocity=(Vector3)arrowVelocity.GetValue(arrow),offset=hurtCollider.bounds.center-arrow.transform.position;
                float time=Vector3.Dot(offset,velocity)/Mathf.Max(.001f,velocity.sqrMagnitude);
                if(time<.08f||time>.65f||(offset-velocity*time).sqrMagnitude>1.2f*1.2f)continue;
                if(!consideredArrows.Add(arrow))continue;
                if(UnityEngine.Random.value>ArrowDodgeChance)continue;
                if(TryBeginArrowDodge(velocity))return true;
            }
            return false;
        }
        public bool TryBeginArrowDodge(Vector3 incomingVelocity)
        {
            if(!Fighting||State!=Phase.Moving||Time.time<nextArrowDodge)return false;
            Vector3 side=Vector3.Cross(Vector3.up,incomingVelocity).normalized;if(side.sqrMagnitude<.1f)return false;
            if(UnityEngine.Random.value<.5f)side=-side;
            for(int i=0;i<2;i++)
            {
                Vector3 target=ArenaPoint(Actor.position+side*2.3f);
                if(Vector3.ProjectOnPlane(target-Actor.position,Vector3.up).magnitude>1.5f&&ClearBodyPath(Actor.position,target))
                {dodgeTarget=target;arrowDodge=true;nextArrowDodge=Time.time+3/ResponseSpeed;ReactiveDodges++;animationPlayer.PlaybackSpeed=ResponseSpeed;SetPhase(Phase.Dodge,Vector3.Dot(side,Actor.right)<0?2:3);bossAudio.PlayShield();return true;}
                side=-side;
            }
            return false;
        }
    }
}
