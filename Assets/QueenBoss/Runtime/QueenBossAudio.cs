using UnityEngine;

namespace QueenBattle
{
    // One loop and six reusable voices serve the entire encounter, including summons.
    public sealed class QueenBossAudio:MonoBehaviour
    {
        public AudioClip AttackClip,AuraClip,ShieldClip;
        public int AttackPlays {get;private set;}
        public int ShieldPlays {get;private set;}
        public int SuppressedCues {get;private set;}
        public bool Ready=>AttackClip&&AuraClip&&ShieldClip;
        public bool CombatRunning {get;private set;}
        public bool Paused {get;private set;}
        public AudioSource AuraSource {get;private set;}
        readonly AudioSource[] voices=new AudioSource[6];
        readonly float[] busyUntil=new float[6];
        QueenBossBattle battle;
        float nextAttack,nextShield;
        public void Initialize(QueenBossBattle owner)
        {
            battle=owner;
            AttackClip=owner.AttackSound?owner.AttackSound:Resources.Load<AudioClip>("QueenBossAudio/grand_warden_attack_01");
            AuraClip=owner.AuraLoopSound?owner.AuraLoopSound:Resources.Load<AudioClip>("QueenBossAudio/grand_warden_aura_loop_01");
            ShieldClip=owner.ShieldSound?owner.ShieldSound:Resources.Load<AudioClip>("QueenBossAudio/grand_warden_shield_01");
            AuraSource=Source("Queen aura loop");AuraSource.loop=true;AuraSource.clip=AuraClip;
            for(int i=0;i<voices.Length;i++)voices[i]=Source(i<4?"Queen attack voice "+i:"Queen summon voice "+i);
            if(!Ready)Debug.LogWarning("Queen boss sound clips are missing. Check Resources/QueenBossAudio.",owner);
        }
        AudioSource Source(string label)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=false;source.spatialBlend=.55f;source.rolloffMode=AudioRolloffMode.Linear;
            source.minDistance=5;source.maxDistance=65;source.dopplerLevel=0;source.priority=90;return source;
        }
        public void Begin()
        {
            Stop();CombatRunning=true;AttackPlays=ShieldPlays=SuppressedCues=0;
            AuraSource.volume=battle.BossSfxVolume*.3f;AuraSource.pitch=1;
            if(AuraClip)AuraSource.Play();PlayShield();
        }
        public void PlayAttack()
        {
            if(!CombatRunning||Paused||!battle.Fighting||!AttackClip)return;
            if(Time.time<nextAttack){SuppressedCues++;return;}
            if(Play(AttackClip,0,4,.16f)){nextAttack=Time.time+.14f;AttackPlays++;}
        }
        public void PlayShield()
        {
            if(!CombatRunning||Paused||!battle.Fighting||!ShieldClip)return;
            if(Time.time<nextShield){SuppressedCues++;return;}
            if(Play(ShieldClip,4,6,.18f)){nextShield=Time.time+.3f;ShieldPlays++;}
        }
        bool Play(AudioClip clip,int first,int end,float gain)
        {
            for(int i=first;i<end;i++)
            {
                if(Time.time<busyUntil[i])continue;
                var voice=voices[i];voice.clip=clip;voice.volume=battle.BossSfxVolume*gain;voice.pitch=Random.Range(.96f,1.04f);
                busyUntil[i]=Time.time+clip.length/voice.pitch;voice.Play();return true;
            }
            SuppressedCues++;return false;
        }
        public void SetPaused(bool value)
        {
            if(!CombatRunning||Paused==value)return;Paused=value;
            if(value){AuraSource.Pause();foreach(var voice in voices)voice.Pause();}
            else {AuraSource.UnPause();foreach(var voice in voices)voice.UnPause();}
        }
        public void Stop()
        {
            CombatRunning=false;Paused=false;nextAttack=nextShield=0;
            if(AuraSource)AuraSource.Stop();
            for(int i=0;i<voices.Length;i++){busyUntil[i]=0;if(voices[i])voices[i].Stop();}
        }
        void OnDisable(){Stop();}
    }
}
