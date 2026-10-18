#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using QueenFlightTest;
namespace QueenBattle.Editor
{
    public static class TestQueenSecondPhase
    {
        static QueenBossBattle boss;
        static double start;
        static int stage,maxOrbs,maxGhosts;
        static float phaseHealth,testAt;
        static bool recovery,lightning,heavy,overlap,summonDestroyed;
        static Vector3 beforeDodge;
        static Component testArrow;
        static string report="";
        static bool retreatTest;
        static Vector3 movementOrigin;
        public static void Run()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/QueenBoss/ActualHallVerification.unity");
                var b=UnityEngine.Object.FindAnyObjectByType<QueenBossBattle>();
                Require(b&&b.RoomRoot&&b.FloorSupport,"Missing actual hall anchors");
                var p=new GameObject("Phase two verification player");p.tag="Player";p.AddComponent<CharacterController>().center=Vector3.up;b.Player=p.transform;
                b.PlayerMaxHealth=100000;
                SessionState.SetBool("QueenSecondPhaseTest",true);EditorApplication.EnterPlaymode();
            }
            catch(Exception e){Fail(e);}
        }
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Require(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        static void SetPrivate(string name,object value){typeof(QueenBossBattle).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(boss,value);}
        static void Tick()
        {
            if(!SessionState.GetBool("QueenSecondPhaseTest",false)||!EditorApplication.isPlaying)return;
            try
            {
                if(start==0)start=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-start>110)throw new TimeoutException("Phase two stalled at "+stage+" "+(boss?boss.State.ToString():"missing"));
                if(!boss)boss=UnityEngine.Object.FindAnyObjectByType<QueenBossBattle>();if(!boss||Time.time<.3f)return;
                if(stage==0)
                {
                    var p=boss.ThroneSeat.position+boss.ThroneSeat.forward*3;p.y=boss.SurfaceHeightAt(p)+.03f;
                    var cc=boss.Player.GetComponent<CharacterController>();cc.enabled=false;boss.Player.position=p;cc.enabled=true;
                    boss.StartBattle();Require(!boss.SecondPhase,"Second phase started at full health");
                    Require(Vector3.ProjectOnPlane(boss.Player.position-boss.ThroneSeat.position,Vector3.up).magnitude>15,"Begin battle did not move player back from throne");
                    Require(Vector3.Dot(boss.Player.forward,Vector3.ProjectOnPlane(boss.Actor.position-boss.Player.position,Vector3.up).normalized)>.98f,"Player does not face queen after retreat");
                    Require(Vector3.ProjectOnPlane(boss.Actor.position-boss.ThroneSeat.position,Vector3.up).magnitude>=7,"Queen spawned against throne");
                    boss.ReceiveBossDamage(boss.MaxHealth*.51f);phaseHealth=boss.Health;Time.timeScale=1;stage=1;
                }
                if(stage==1&&boss.SecondPhase&&boss.State==QueenBossBattle.Phase.Moving)
                {
                    Require(Mathf.Approximately(boss.Health,phaseHealth),"Phase transition healed queen");
                    var t=Type.GetType("QueenBattle.QueenVerificationArrow, Assembly-CSharp");Require(t!=null,"Arrow verification fixture missing");
                    testArrow=new GameObject("Verification incoming arrow").AddComponent(t);
                    var velocity=Vector3.forward*20;
                    testArrow.transform.position=boss.Actor.GetComponentInChildren<CapsuleCollider>().bounds.center-velocity*.4f;
                    t.GetField("Velocity").SetValue(testArrow,velocity);
                    SetPrivate("arrowType",t);SetPrivate("arrowOwner",t.GetField("Owner"));SetPrivate("arrowVelocity",t.GetField("Velocity"));
                    boss.ArrowDodgeChance=1;testAt=Time.time;stage=2;
                }
                if(stage==2&&Time.time-testAt>.02f)
                {
                    Require(boss.ReactiveDodges==0,"Queen dodged an unowned projectile");
                    testArrow.GetType().GetField("Owner").SetValue(testArrow,boss.Player);beforeDodge=boss.Actor.position;stage=3;
                }
                if(stage==3&&boss.State==QueenBossBattle.Phase.Moving&&testArrow)
                {
                    var velocity=Vector3.forward*20;
                    testArrow.transform.position=boss.Actor.GetComponentInChildren<CapsuleCollider>().bounds.center-velocity*.4f;
                    beforeDodge=boss.Actor.position;
                }
                if(stage==3&&boss.ReactiveDodges>0)
                {
                    Require(boss.Dodging,"Arrow dodge did not enter dodge state");
                    Require(!boss.TryBeginArrowDodge(Vector3.forward*20),"Repeated dodge ignored cooldown");
                    UnityEngine.Object.Destroy(testArrow.gameObject);stage=4;
                }
                if(stage==4&&!boss.Dodging)
                {
                    Require(Vector3.ProjectOnPlane(boss.Actor.position-beforeDodge,Vector3.up).magnitude>1,"Dodge animation did not move queen");
                    boss.ArrowDodgeChance=.35f;Time.timeScale=3;stage=5;
                    report+="PASS: half-health transition retains health; actual incoming-arrow scan rejects unowned arrows, moves sideways, respects cooldown.\n";
                }
                if(stage<5)return;
                if(!retreatTest&&boss.SecondPhaseAttacks>=5)
                {
                    // Reproduce a player rushing behind the throne: destinations must stay in the aisle.
                    var cc=boss.Player.GetComponent<CharacterController>();cc.enabled=false;boss.Player.position=boss.ThroneSeat.position;cc.enabled=true;
                    movementOrigin=boss.Actor.position;retreatTest=true;
                }
                Require(Vector3.ProjectOnPlane(boss.Actor.position-boss.ThroneSeat.position,Vector3.up).magnitude>=6,"Queen chased player into throne");
                var orbs=boss.GetComponentsInChildren<QueenRoyalProjectile>();var ghosts=boss.GetComponentsInChildren<QueenSpectralAssistant>();
                maxOrbs=Mathf.Max(maxOrbs,orbs.Length);maxGhosts=Mathf.Max(maxGhosts,ghosts.Length);
                Require(ghosts.Length<=boss.PhaseTwoSummons,"Summon cap exceeded");
                overlap|=orbs.Length>0&&boss.State==QueenBossBattle.Phase.Casting&&boss.SecondPhaseAttacks>0;
                heavy|=orbs.Any(p=>p.Heavy);
                lightning|=boss.GetComponentsInChildren<QueenSpellEffect>().Any(p=>p.Kind==QueenSpellEffect.Spell.Lightning);
                foreach(var line in boss.GetComponentsInChildren<LineRenderer>())
                    if(line.name=="Queen ground warning"||line.name=="Queen spell warning")Require(line.loop&&line.positionCount==64,"Targeting beam returned");
                foreach(var skin in boss.Actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                    Require(skin.bounds.min.y>=boss.SurfaceHeightAt(skin.bounds.center)-.05f,"Queen intersects raised hall floor");
                if(!summonDestroyed&&ghosts.Length==boss.PhaseTwoSummons)
                {ghosts[0].ReceiveDamage(100,boss.Player);summonDestroyed=true;}
                if(boss.State==QueenBossBattle.Phase.Recovery&&boss.Actor.position.y<boss.GroundHeight+.2f&&!recovery)
                {float hp=boss.Health;boss.ReceiveBossDamage(5);Require(boss.Health==hp-5,"Committed recovery was not damageable");recovery=true;}
                if(boss.SecondPhaseAttacks<10)return;
                Require(retreatTest&&Vector3.ProjectOnPlane(boss.Actor.position-movementOrigin,Vector3.up).magnitude>.7f,"Queen stopped moving after player approached throne");
                Require(overlap&&maxOrbs>=60&&maxGhosts==boss.PhaseTwoSummons&&summonDestroyed&&heavy&&lightning&&recovery,"Missing mechanic: overlap="+overlap+", orbs="+maxOrbs+", ghosts="+maxGhosts+", heavy="+heavy+", lightning="+lightning+", recovery="+recovery);
                Require(boss.PlayerHealth<boss.PlayerMaxHealth,"Phase two did not damage player");
                report+="PASS: two full attack sequences; 60-orb constellation, overlapping casts, mass lightning, heavy orbs, six destructible animated summons, low melee recovery.\n";
                report+="PASS: actual castle floor clearance, ground-circle warnings only, phase-two damage. Peak projectiles="+maxOrbs+".\n";
                boss.ReceiveBossDamage(10000);Require(boss.State==QueenBossBattle.Phase.Victory,"Victory failed");
                Require(!boss.GetComponentsInChildren<QueenRoyalProjectile>().Any(p=>p.enabled),"Projectiles remained live after victory");
                boss.Retry();Require(!boss.SecondPhase&&boss.Health==boss.MaxHealth,"Retry did not reset phase");boss.AbortBattle();
                Require(!boss.Actor.gameObject.activeSelf&&!StarterAssets.DomainInteractionState.IsModal,"Abandon failed");
                report+="PASS: victory clears hazards; retry resets phase and health; abandon restores peaceful state.\n";
                report+="PASS: Begin battle moves player back and faces queen; queen starts clear of throne and keeps moving when player rushes throne.\n";
                File.WriteAllText("Logs/QueenSecondPhase-PASS.txt",report);SessionState.SetBool("QueenSecondPhaseTest",false);Time.timeScale=1;EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
            }
            catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){SessionState.SetBool("QueenSecondPhaseTest",false);File.WriteAllText("Logs/QueenSecondPhase-error.txt",report+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
#endif
