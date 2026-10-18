using UnityEngine;using StarterAssets;
namespace WildernessCombat {
public sealed class WildernessPlayerHealth:MonoBehaviour {
 public float MaxHealth=120;public float Health{get;private set;}float invulnerableUntil,showUntil;Vector3 safeStart;CQCCrouchClips roll;CharacterController controller;
 void Awake(){Health=MaxHealth;safeStart=transform.position;roll=GetComponent<CQCCrouchClips>();controller=GetComponent<CharacterController>();}
 public void Receive(float damage,Vector3? attackerPosition=null){if(damage<=0||Time.time<invulnerableUntil||roll&&roll.IsRolling)return;Health=Mathf.Max(0,Health-damage);if(Health>0){var hit=GetComponent<WeaponController>();if(hit)hit.PlayDamageReaction(attackerPosition);}showUntil=Time.time+8;invulnerableUntil=Time.time+.55f;
 if(Health<=0){var weapon=GetComponent<WeaponController>();if(weapon)weapon.CancelForRoll();if(controller)controller.enabled=false;transform.position=safeStart;if(controller)controller.enabled=true;var motor=GetComponent<ThirdPersonController>();if(motor)motor.ResetAfterDomainTravel(transform.eulerAngles.y);Health=MaxHealth;invulnerableUntil=Time.time+4;}}
 void Update(){if(Time.time>showUntil+8)Health=Mathf.Min(MaxHealth,Health+Time.deltaTime*5);}
 void OnGUI(){if(Time.time>showUntil||QueenBattle.QueenBossBattle.BattleActive)return;var old=GUI.color;GUI.color=new Color(.06f,.07f,.08f,.85f);GUI.DrawTexture(new Rect(24,24,210,12),Texture2D.whiteTexture);GUI.color=new Color(.3f,.9f,.4f);GUI.DrawTexture(new Rect(24,24,210*Mathf.Clamp01(Health/MaxHealth),12),Texture2D.whiteTexture);GUI.color=old;}
}}





