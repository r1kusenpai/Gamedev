using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using QueenFlightTest;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace QueenBattle
{
    public sealed partial class QueenBossBattle : MonoBehaviour
    {
        public Transform RoomRoot,ThroneSeat,Arrival,Player;
        public BoxCollider FloorSupport;
        public GameObject QueenPrefab;
        public AnimationClip[] Clips;
        public GameObject[] Spells;
        public Material TelegraphMaterial;
        public float MaxHealth=4800,PlayerMaxHealth=120;
        float highlightedBossHealth,bossDamageHighlightUntil;
        GUIStyle bossNameStyle;
        [Min(1)] public float ResponseSpeed=3;
        [Range(1,6)] public int PhaseTwoSummons=6;
        [Range(1,10)] public int PhaseTwoSpellMultiplier=10;
        public AudioClip AttackSound,AuraLoopSound,ShieldSound;
        [Range(0,1)] public float BossSfxVolume=.75f;
        QueenBossAudio bossAudio;
        public enum Phase { Dormant,Rising,Moving,Dodge,Casting,Recovery,Victory,Defeat }
        public Phase State {get;private set;}
        public float Health {get;private set;}
        public float PlayerHealth {get;private set;}
        public int CompletedAttacks {get;private set;}
        public Transform Actor {get;private set;}
        public bool Fighting=>State>=Phase.Rising&&State<=Phase.Recovery;
        public bool AnimationReady=>animationPlayer&&animationPlayer.IsReady;
        public bool Dodging=>State==Phase.Dodge;
        public bool ChallengeOpen=>modal==1;
        public static QueenBossBattle Active {get;private set;}
        public static bool BattleActive=>Active&&Active.Fighting;
        QueenBossAnimation animationPlayer;
        Transform standingQueen,rightHand,leftHand;
        CapsuleCollider hurtCollider;
        Renderer[] renderers;
        Bounds arena;
        Collider[] walkableSurfaces;
        Bounds[] walkableBounds;
        float surfaceRayTop;
        public float GroundHeight=>Actor?SurfaceHeightAt(Actor.position):floorY;
        Vector3 destination,lockedAim;
        LineRenderer telegraph;
        float floorY,phaseTime,invulnerableUntil;
        int modal;
        bool released,oldCursorVisible;
        CursorLockMode oldCursor;
        float previousTime;
        Behaviour[] pausedBehaviours;
        bool[] pausedStates;
        Component roll;
        PropertyInfo rolling;

        void Start()
        {
            // Also upgrade the original serialized default in a hall already open in the editor.
            if(Mathf.Approximately(MaxHealth,480))MaxHealth=4800;
            if(!RoomRoot||!ThroneSeat||!FloorSupport||!QueenPrefab||Clips==null||Clips.Length!=7||Spells.Length!=3)
            {Debug.LogError("Queen boss setup is incomplete.",this);enabled=false;return;}
            arena=FloorSupport.bounds;floorY=arena.max.y+.05f;Health=MaxHealth;highlightedBossHealth=MaxHealth;PlayerHealth=PlayerMaxHealth;
            walkableSurfaces=RoomRoot.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&IsWalkableSurface(c)).ToArray();
            walkableBounds=walkableSurfaces.Select(c=>c.bounds).ToArray();
            surfaceRayTop=walkableBounds.Length>0?walkableBounds.Max(b=>b.max.y)+2:arena.max.y+8;
            standingQueen=RoomRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Queen Beside Throne");
            var root=new GameObject("Flying Queen Boss");root.transform.SetParent(transform,false);Actor=root.transform;
            bossAudio=root.AddComponent<QueenBossAudio>();bossAudio.Initialize(this);
            var model=Instantiate(QueenPrefab,Actor);model.name="Queen latest flying animations";
            animationPlayer=root.AddComponent<QueenBossAnimation>();animationPlayer.Initialize(model.GetComponentInChildren<Animator>(),Clips);
            renderers=model.GetComponentsInChildren<Renderer>();
            foreach(var t in model.GetComponentsInChildren<Transform>()){if(t.name=="Right_Hand")rightHand=t;if(t.name=="Left_Hand")leftHand=t;}
            var hurt=new GameObject("Queen damage collider");hurt.transform.SetParent(Actor,false);hurtCollider=hurt.AddComponent<CapsuleCollider>();hurtCollider.height=1.9f;hurtCollider.radius=.38f;hurt.AddComponent<QueenBossHurtbox>().Battle=this;
            Actor.gameObject.SetActive(false);FindPlayer();
        }
        static bool IsWalkableSurface(Collider collider)
        {
            string name=collider.name.ToLowerInvariant();
            return name.Contains("floor")||name.StartsWith("stair")||name.StartsWith("step")||name.Contains("walkable platform");
        }
        public float SurfaceHeightAt(Vector3 position)
        {
            float height=arena.max.y;
            if(walkableSurfaces!=null)
            {
                var ray=new Ray(new Vector3(position.x,surfaceRayTop,position.z),Vector3.down);
                for(int i=0;i<walkableSurfaces.Length;i++)
                {
                    var surface=walkableSurfaces[i];var bounds=walkableBounds[i];
                    if(position.x<bounds.min.x-.01f||position.x>bounds.max.x+.01f||position.z<bounds.min.z-.01f||position.z>bounds.max.z+.01f)continue;
                    if(surface&&surface.enabled&&surface.gameObject.activeInHierarchy&&surface.Raycast(ray,out var hit,surfaceRayTop-arena.min.y+20)&&hit.normal.y>.55f)height=Mathf.Max(height,hit.point.y);
                }
            }
            return height+.05f;
        }
        void FindPlayer()
        {
            if(!Player){var go=GameObject.FindGameObjectWithTag("Player");if(go)Player=go.transform;}
            if(!Player)return;
            roll=Player.GetComponentsInChildren<MonoBehaviour>().FirstOrDefault(b=>b.GetType().Name=="CQCCrouchClips");
            if(roll)rolling=roll.GetType().GetProperty("IsRolling");
        }
        bool NearThrone=>Player&&Vector3.ProjectOnPlane(Player.position-ThroneSeat.position,Vector3.up).magnitude<4.5f&&Mathf.Abs(Player.position.y-ThroneSeat.position.y)<4;
        public bool OpenChallenge()
        {
            if(State!=Phase.Dormant||!NearThrone||modal!=0||StarterAssets.DomainInteractionState.IsModal)return false;
            Pause(1);return true;
        }
        public void StartBattle()
        {
            if(!Player)return;Unpause();ClearSpells();ResetSecondPhase();ResetReferencePhase();Health=MaxHealth;highlightedBossHealth=MaxHealth;PlayerHealth=PlayerMaxHealth;CompletedAttacks=0;invulnerableUntil=0;
            if(standingQueen)standingQueen.gameObject.SetActive(false);
            Actor.gameObject.SetActive(true);var spawn=FindOpenArenaPoint(ThroneSeat.position+ArenaForward*9,.04f);
            floorY=SurfaceHeightAt(spawn);spawn.y=floorY+.04f;Actor.position=spawn;
            MovePlayerToBattleStart();
            Actor.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(Player.position-Actor.position,Vector3.up).normalized);
            Active=this;SetPhase(Phase.Rising,0);bossAudio.Begin();
        }
        public void CancelChallenge(){Unpause();}
        void SetPhase(Phase phase,int clip)
        {State=phase;phaseTime=0;animationPlayer.Play(clip);}
        void Update()
        {
            if(Health>highlightedBossHealth)highlightedBossHealth=Health;
            if(Time.unscaledTime>bossDamageHighlightUntil)highlightedBossHealth=Mathf.MoveTowards(highlightedBossHealth,Health,MaxHealth*.55f*Time.unscaledDeltaTime);
            if(!Player)FindPlayer();if(!Player||!Actor)return;
            bool use=false,escape=false,enter=false;
#if ENABLE_INPUT_SYSTEM
            var k=Keyboard.current;if(k!=null){use=k.eKey.wasPressedThisFrame;escape=k.escapeKey.wasPressedThisFrame;enter=k.enterKey.wasPressedThisFrame;}
#endif
            if(modal!=0){if(escape){if(modal==3)AbortBattle();else Unpause();}else if(enter){if(modal==1)StartBattle();else AbortBattle();}return;}
            if(State==Phase.Dormant){if(use)OpenChallenge();return;}
            if(!Fighting)return;
            if(Player.gameObject.scene!=gameObject.scene&&Vector3.Distance(Player.position,arena.center)>120){AbortBattle();return;}
            if(escape){Pause(2);return;}
            if(StarterAssets.DomainInteractionState.IsModal||Time.timeScale==0)return;
            phaseTime+=Time.deltaTime;floorY=SurfaceHeightAt(Actor.position);
            if(!SecondPhase&&Health<=MaxHealth*.5f&&State!=Phase.Casting&&State!=Phase.Dodge)EnterSecondPhase();
            if(SecondPhase){TickSecondPhase();return;}
            TickReferencePhase();return;

        }
        void MoveTo(Vector3 target,float speed)
        {
            target.y=Mathf.Max(target.y,SurfaceHeightAt(target)+.04f);
            Vector3 delta=Vector3.MoveTowards(Actor.position,target,speed*Time.deltaTime)-Actor.position;
            if(delta.sqrMagnitude<.000001f)return;
            var next=Actor.position+delta;next.y=Mathf.Max(next.y,SurfaceHeightAt(next)+.04f);Actor.position=next;
            if(Mathf.Abs(delta.x)+Mathf.Abs(delta.z)>.0001f&&!ClearBodyPath(next-delta,next))
            {
                Actor.position=new Vector3(next.x-delta.x,next.y,next.z-delta.z);
                if(State==Phase.Moving)destination=FindTravelPoint(destination);
            }
        }
        public void ReceiveBossDamage(float damage)
        {if(!Fighting||Dodging||damage<=0)return;highlightedBossHealth=Mathf.Max(highlightedBossHealth,Health);bossDamageHighlightUntil=Time.unscaledTime+.35f;Health=Mathf.Max(0,Health-damage);if(Health<=0)Finish(true);}
        public void ReceivePlayerDamage(float damage)
        {
            if(!Fighting||Time.time<invulnerableUntil||damage<=0)return;
            if(roll&&rolling!=null&&(bool)rolling.GetValue(roll))return;
            PlayerHealth=Mathf.Max(0,PlayerHealth-damage);if(PlayerHealth>0&&Player){var hit=Player.GetComponent<StarterAssets.WeaponController>();if(hit)hit.PlayDamageReaction(Actor?Actor.position:Player.position+Player.forward);}invulnerableUntil=Time.time+.65f;if(PlayerHealth<=0)Finish(false);
        }
        void Finish(bool victory)
        {bossAudio.Stop();ClearSpells();ClearTelegraph();State=victory?Phase.Victory:Phase.Defeat;animationPlayer.PlaybackSpeed=1;animationPlayer.Play(0);Actor.position=new Vector3(Actor.position.x,floorY+.04f,Actor.position.z);Active=null;Pause(3);}
        public void AbortBattle()
        {Unpause();if(bossAudio)bossAudio.Stop();ClearSpells();ClearTelegraph();ResetSecondPhase();State=Phase.Dormant;Active=null;Health=MaxHealth;highlightedBossHealth=MaxHealth;PlayerHealth=PlayerMaxHealth;if(Actor)Actor.gameObject.SetActive(false);if(standingQueen)standingQueen.gameObject.SetActive(true);}
        public void Retry()
        {
            StartBattle();
        }
        void ClearSpells(){ClearSecondPhaseEffects();foreach(var hit in GetComponentsInChildren<QueenBossSpellHit>())Destroy(hit.gameObject);}
        void ClearTelegraph(){if(telegraph)Destroy(telegraph.gameObject);telegraph=null;}
        void Pause(int reason)
        {
            if(bossAudio)bossAudio.SetPaused(true);
            if(modal!=0)return;modal=reason;previousTime=Time.timeScale;Time.timeScale=0;oldCursor=Cursor.lockState;oldCursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;StarterAssets.DomainInteractionState.IsModal=true;
            pausedBehaviours=Player?Player.GetComponents<Behaviour>().Where(b=>b.GetType().Name=="ThirdPersonController"||b.GetType().Name=="WeaponController"||b.GetType().Name=="CQCCombat").ToArray():Array.Empty<Behaviour>();pausedStates=pausedBehaviours.Select(b=>b.enabled).ToArray();foreach(var b in pausedBehaviours)b.enabled=false;
        }
        void Unpause()
        {
            if(modal==0)return;modal=0;Time.timeScale=previousTime;Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;StarterAssets.DomainInteractionState.IsModal=false;
            for(int i=0;i<pausedBehaviours.Length;i++)if(pausedBehaviours[i])pausedBehaviours[i].enabled=pausedStates[i];
            if(bossAudio)bossAudio.SetPaused(false);
        }
        void LateUpdate()
        {
            if(!Actor||!Actor.gameObject.activeInHierarchy||renderers==null)return;
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            // Sampling the real floor tiles also protects fast sideways poses across raised steps.
            float visualGround=Mathf.Max(SurfaceHeightAt(Actor.position),SurfaceHeightAt(bounds.center));
            float lift=Mathf.Max(0,visualGround+.025f-bounds.min.y);
            if(lift>0){Actor.position+=Vector3.up*lift;bounds.center+=Vector3.up*lift;}
            hurtCollider.transform.position=bounds.center;hurtCollider.transform.rotation=Quaternion.identity;hurtCollider.center=Vector3.zero;hurtCollider.height=Mathf.Clamp(bounds.size.y,1,2.6f);
        }
        void OnDisable(){if(Active==this)Active=null;Unpause();if(bossAudio)bossAudio.Stop();ClearSpells();ClearTelegraph();if(standingQueen)standingQueen.gameObject.SetActive(true);}
        void OnGUI()
        {
            if(!Player||!Actor)return;
            var save=GUI.matrix;float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            if(Fighting)
            {
                float bossBarWidth=Mathf.Min(640f,Screen.width/scale-64f);
                var bossBar=new Rect((Screen.width/scale-bossBarWidth)*.5f,Screen.height/scale-48f,bossBarWidth,14f);
                if(bossNameStyle==null)bossNameStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=17,fontStyle=FontStyle.Bold,normal={textColor=new Color(.96f,.9f,.8f)}};
                GUI.Label(new Rect(bossBar.x,bossBar.y-30,bossBar.width,26),SecondPhase?"QUEEN OF THE THRONE - ROYAL FURY":"QUEEN OF THE THRONE",bossNameStyle);
                var barColor=GUI.color;
                GUI.color=new Color(.06f,.07f,.08f,.85f);GUI.DrawTexture(bossBar,Texture2D.whiteTexture);
                // Both fills share the same left edge: damage retracts the right end only.
                GUI.color=new Color(1f,.72f,.30f);
                GUI.DrawTexture(new Rect(bossBar.x,bossBar.y,bossBar.width*Mathf.Clamp01(highlightedBossHealth/MaxHealth),bossBar.height),Texture2D.whiteTexture);
                GUI.color=new Color(.65f,.08f,.25f);
                GUI.DrawTexture(new Rect(bossBar.x,bossBar.y,bossBar.width*Mathf.Clamp01(Health/MaxHealth),bossBar.height),Texture2D.whiteTexture);
                GUI.color=barColor;
                Bar(new Rect(24,24,210,12),PlayerHealth/PlayerMaxHealth,new Color(.3f,.9f,.4f));
                GUI.Label(new Rect(400,22,700,35),State==Phase.Recovery?"She is vulnerable — attack now":SecondPhase?"Royal Fury — move or roll.  Esc: leave battle":ReferenceAttackName+" — move or roll.  Esc: leave battle");
            }
            if(modal!=0)
            {
                float modalX=(Screen.width/scale-460f)*.5f,modalY=(Screen.height/scale-275f)*.5f;
                GUI.Box(new Rect(modalX,modalY,460,275),modal==1?"CHALLENGE THE QUEEN":modal==2?"LEAVE THE BATTLE?":State==Phase.Victory?"QUEEN DEFEATED":"YOU WERE DEFEATED");
                GUI.Label(new Rect(modalX+30,modalY+50,400,55),modal==1?"Face the flying queen and her magic?":modal==2?"Return the queen to her peaceful state?":"Fight again, or return to the throne hall.");
                if(GUI.Button(new Rect(modalX+40,modalY+120,380,45),modal==1?"Begin battle":modal==2?"Leave battle":"Retry battle")){if(modal==1)StartBattle();else if(modal==2)AbortBattle();else Retry();}
                if(GUI.Button(new Rect(modalX+40,modalY+180,380,42),modal==3?"Return to throne hall":"Cancel")){if(modal==3)AbortBattle();else Unpause();}
            }
            else if(State==Phase.Dormant&&NearThrone&&!StarterAssets.DomainInteractionState.IsModal)GUI.Box(new Rect(430,610,420,45),"[ E ]  Challenge the queen at the throne");
            GUI.matrix=save;
        }
        static void Bar(Rect rect,float amount,Color color){var old=GUI.color;GUI.color=new Color(.06f,.07f,.08f,.85f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=color;GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(amount),rect.height),Texture2D.whiteTexture);GUI.color=old;}
    }
}






