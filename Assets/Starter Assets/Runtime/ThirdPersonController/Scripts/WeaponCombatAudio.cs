using UnityEngine;
namespace StarterAssets {
// Actor-local spatial cues. Clips are shared; nearby enemy releases are rate limited.
public sealed class CombatSoundEmitter:MonoBehaviour {
 static AudioClip miss,hit,cyclops,magic;static AudioClip[] cyclopsVariants;static float nextEnemyCue;AudioSource source;float nextCue;
 public AudioClip LastClip{get;private set;}public int PlayCount{get;private set;}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetBudget(){nextEnemyCue=0;}
 public static AudioClip SlashMiss=>miss?miss:miss=Resources.Load<AudioClip>("CombatAudio/SlashMiss");
 public static AudioClip SlashHit=>hit?hit:hit=Resources.Load<AudioClip>("CombatAudio/SlashHit");
 public static AudioClip CyclopsAttack=>cyclops?cyclops:cyclops=Resources.Load<AudioClip>("CombatAudio/CyclopsAttack_01");
 public static AudioClip FlyingAttack=>magic?magic:magic=Resources.Load<AudioClip>("QueenBossAudio/grand_warden_attack_01");
 public void Slash(bool connected){Play(connected?SlashHit:SlashMiss,connected?.65f:.30f,false);}
 public void EnemyAttack(bool isCyclops){if(Time.time<nextCue||Time.time<nextEnemyCue)return;nextCue=Time.time+.22f;nextEnemyCue=Time.time+.04f;if(isCyclops&&cyclopsVariants==null){cyclopsVariants=new AudioClip[5];for(int i=0;i<5;i++)cyclopsVariants[i]=Resources.Load<AudioClip>("CombatAudio/CyclopsAttack_0"+(i+1));}var cue=isCyclops?cyclopsVariants[Random.Range(0,5)]:FlyingAttack;Play(cue,isCyclops?.5f:.16f,true);}
 void Play(AudioClip clip,float volume,bool enemy){if(!clip)return;if(!source){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.minDistance=enemy?3:1;source.maxDistance=enemy?35:22;source.rolloffMode=AudioRolloffMode.Linear;source.dopplerLevel=0;source.priority=enemy?150:80;}
 source.Stop();source.clip=clip;source.volume=volume;source.pitch=1;source.Play();LastClip=clip;PlayCount++;}
 void OnDisable(){if(source)source.Stop();}
}
public partial class WeaponController {
 CombatSoundEmitter slashSounds;bool slashContact,slashMissPlayed,slashHitPlayed;
 void ResetSlashSoundWindow(){slashContact=slashMissPlayed=slashHitPlayed=false;}
 void NoteSlashContact(){slashContact=true;}
 void ResolveSlashSound(){if(CurrentWeapon!=Weapon.Sword&&CurrentWeapon!=Weapon.Greatsword)return;if(slashContact?slashHitPlayed:slashMissPlayed)return;
 if(!slashSounds)slashSounds=GetComponent<CombatSoundEmitter>();if(!slashSounds)slashSounds=gameObject.AddComponent<CombatSoundEmitter>();slashSounds.Slash(slashContact);if(slashContact)slashHitPlayed=true;else slashMissPlayed=true;}
}
}

