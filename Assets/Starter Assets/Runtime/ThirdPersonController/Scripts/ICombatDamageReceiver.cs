using UnityEngine;
namespace StarterAssets {
 public interface ICombatDamageReceiver {
  bool IsDamageable {get;}
  Transform DamageRoot {get;}
  void ReceiveDamage(float damage,Transform attacker);
 }
}
