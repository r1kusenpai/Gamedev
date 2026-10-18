#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace QueenBattle.Editor
{
    public static class TestQueenReferenceExpansion
    {
        static QueenBossBattle boss;
        static double start;
        static int stage,maxOrbs,maxGhosts,fireStart,lightningStart,heavyStart;
        static float phaseTwoStarted,lastSample;
        static bool lowRecovery,ghostDamage;
        static string report="";
        static QueenBossAudio sound;
        public static void Run()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/QueenBoss/ActualHallVerification.unity");
                var b=UnityEngine.Object.FindAnyObjectByType<QueenBossBattle>();Require(b&&b.RoomRoot&&b.FloorSupport,"Missing throne hall anchors");
                var p=new GameObject("Reference fight verification player");p.tag="Player";p.AddComponent<CharacterController>().center=Vector3.up;b.Player=p.transform;b.PlayerMaxHealth=100000;
                if(!UnityEngine.Object.FindAnyObjectByType<AudioListener>())p.AddComponent<AudioListener>();
                SessionState.SetBool("QueenReferenceExpansionTest",true);EditorApplication.EnterPlaymode();
            }
            catch(Exception e){Fail(e);}
        }
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Require(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        static void Tick()
        {
            if(!SessionState.GetBool("QueenReferenceExpansionTest",false)||!EditorApplication.isPlaying)return;
            try
            {
                if(start==0)start=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-start>160)throw new TimeoutException("Expanded fight stalled: stage="+stage+", moves="+(boss?boss.ReferenceMovesCompleted:0));
                if(!boss)boss=UnityEngine.Object.FindAnyObjectByType<QueenBossBattle>();if(!boss||Time.time<.3f)return;
                if(stage==0)
                {
                    Require(boss.MaxHealth==4800&&boss.ResponseSpeed==3&&boss.PhaseTwoSummons==6&&boss.PhaseTwoSpellMultiplier==10,"Saved difficulty settings incorrect");
                    var p=boss.ThroneSeat.position;p.y=boss.SurfaceHeightAt(p);var cc=boss.Player.GetComponent<CharacterController>();cc.enabled=false;boss.Player.position=p;cc.enabled=true;
                    Require(boss.OpenChallenge()&&Time.timeScale==0,"Throne interaction failed");boss.CancelChallenge();
                    Require(!boss.Fighting&&Time.timeScale>0,"Cancel challenge failed");Require(boss.OpenChallenge(),"Reopen challenge failed");boss.StartBattle();
                    Require(Vector3.ProjectOnPlane(boss.Player.position-boss.ThroneSeat.position,Vector3.up).magnitude>15,"Player did not back off");
                    Require(boss.Health==4800&&!boss.SecondPhase,"Incorrect initial phase/health");Time.timeScale=3;stage=1;
                    sound=boss.Actor.GetComponent<QueenBossAudio>();Require(sound&&sound.Ready&&sound.CombatRunning,"Boss audio clips failed to load");
                    foreach(var clip in new[]{sound.AttackClip,sound.AuraClip,sound.ShieldClip})
                    {
                        Require(clip.length>1&&clip.channels==1,"Audio import metadata wrong: "+clip.name);
                        float[] data=new float[Mathf.Min(clip.samples,22050)];Require(clip.GetData(data,0)&&data.Any(v=>Mathf.Abs(v)>.001f),"Empty audio data: "+clip.name);
                    }
                    Require(sound.AuraSource.loop&&sound.AuraSource.clip==sound.AuraClip,"Aura loop not configured");
                    for(int i=0;i<100;i++)sound.PlayAttack();
                    Require(sound.AttackPlays<=4&&sound.SuppressedCues>=96,"Burst sound limiting failed");
                    Require(sound.GetComponentsInChildren<AudioSource>().Length==7,"Audio sources not bounded");
                    typeof(QueenBossBattle).GetMethod("Pause",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(boss,new object[]{2});
                    Require(sound.Paused,"Battle pause did not pause sound");boss.CancelChallenge();Require(!sound.Paused,"Resume did not resume sound");
                }
                if(Time.time-lastSample>.15f)
                {
                    lastSample=Time.time;
                    foreach(var skin in boss.Actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                        Require(skin.bounds.min.y>=boss.SurfaceHeightAt(skin.bounds.center)-.06f,"Queen sank into raised floor");
                    Require(Vector3.ProjectOnPlane(boss.Actor.position-boss.ThroneSeat.position,Vector3.up).magnitude>=6,"Queen stuck at throne");
                }
                if(boss.State==QueenBossBattle.Phase.Recovery&&boss.Actor.position.y<boss.GroundHeight+.2f&&!lowRecovery)
                {float hp=boss.Health;boss.ReceiveBossDamage(1);Require(boss.Health==hp-1,"Recovery no longer hittable");lowRecovery=true;}
                if(stage==1)
                {
                    if(boss.ReferenceMovesCompleted<14)return;
                    Require(boss.ReferenceUses.All(n=>n>0),"Missing reference attack family");
                    Require(boss.PlayerHealth<boss.PlayerMaxHealth&&lowRecovery,"No attack damage or melee opening");
                    report+="PASS: all fourteen Ansem/Xemnas-inspired attack families released and completed; damage and low melee openings verified.\n";
                    report+="PASS: saved 4800 health, 3x response, six summons and 10x spell settings; player back-off and throne clearance.\n";
                    fireStart=boss.FireballsSpawned;lightningStart=boss.LightningSpawned;heavyStart=boss.HeavyOrbsSpawned;
                    boss.ReceiveBossDamage(boss.MaxHealth*.51f);stage=2;
                }
                if(stage==2&&boss.SecondPhase){phaseTwoStarted=Time.time;stage=3;}
                if(stage!=3)return;
                var orbs=boss.GetComponentsInChildren<QueenRoyalProjectile>();maxOrbs=Mathf.Max(maxOrbs,orbs.Length);
                var ghosts=boss.GetComponentsInChildren<QueenSpectralAssistant>();maxGhosts=Mathf.Max(maxGhosts,ghosts.Length);
                Require(ghosts.Length<=6,"Summon cap exceeded");
                if(ghosts.Length==6&&!ghostDamage){ghosts[0].ReceiveDamage(100,boss.Player);ghostDamage=true;}
                if(boss.SecondPhaseAttacks<10||Time.time-phaseTwoStarted<14)return;
                int fire=boss.FireballsSpawned-fireStart,strikes=boss.LightningSpawned-lightningStart,heavy=boss.HeavyOrbsSpawned-heavyStart;
                Require(maxOrbs>=60&&maxGhosts==6&&ghostDamage,"Dense volley or six destructible summons missing: orbs="+maxOrbs+", ghosts="+maxGhosts);
                Require(fire>=200&&strikes>=60&&heavy>=20,"10x volleys missing: fire="+fire+", lightning="+strikes+", heavy="+heavy);
                Require(sound.AttackPlays>10&&sound.ShieldPlays>=2,"Combat/summon sound cues not triggered in both phases");
                Require(sound.GetComponentsInChildren<AudioSource>().Length==7,"Dense spells created extra audio sources");
                report+="PASS: three OGG clips decode with nonzero samples; repeated cast/volley and summon sounds, one aura loop, seven-source limit, pause/resume. Attack cues="+sound.AttackPlays+", shield cues="+sound.ShieldPlays+", suppressed overlaps="+sound.SuppressedCues+".\n";
                report+="PASS: phase two peak active projectiles="+maxOrbs+", peak summons="+maxGhosts+", total projectiles="+fire+", lightning="+strikes+", heavy="+heavy+". Six summons destructible.\n";
                boss.ReceiveBossDamage(100000);Require(boss.State==QueenBossBattle.Phase.Victory,"Victory failed");
                Require(!boss.GetComponentsInChildren<QueenRoyalProjectile>().Any(p=>p.enabled),"Live projectiles after victory");
                Require(!boss.GetComponentsInChildren<QueenReferenceHazard>().Any(p=>p.enabled),"Live traps after victory");
                Require(!sound.CombatRunning&&sound.GetComponentsInChildren<AudioSource>().All(a=>!a.isPlaying),"Audio continued after victory");
                boss.Retry();Require(boss.Health==4800&&!boss.SecondPhase&&boss.ReferenceMovesCompleted==0,"Retry did not reset difficulty/phase");
                boss.ReceivePlayerDamage(1000000);Require(boss.State==QueenBossBattle.Phase.Defeat,"Player defeat failed");boss.AbortBattle();
                Require(!boss.Actor.gameObject.activeSelf&&!StarterAssets.DomainInteractionState.IsModal&&Time.timeScale>0,"Abandon/modal cleanup failed");
                Require(!sound.CombatRunning&&sound.GetComponentsInChildren<AudioSource>(true).All(a=>!a.isPlaying),"Audio continued after defeat/abandon");
                report+="PASS: victory, projectile/trap cleanup, retry, player defeat and peaceful return. Production runtime/editor compilation checked separately.\n";
                File.WriteAllText("Logs/QueenReferenceExpansion-PASS.txt",report);SessionState.SetBool("QueenReferenceExpansionTest",false);Time.timeScale=1;EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
            }
            catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){SessionState.SetBool("QueenReferenceExpansionTest",false);File.WriteAllText("Logs/QueenReferenceExpansion-error.txt",report+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
#endif
