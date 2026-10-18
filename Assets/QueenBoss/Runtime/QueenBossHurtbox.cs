using UnityEngine;
using StarterAssets;
namespace QueenBattle
{
    public sealed class QueenBossHurtbox : MonoBehaviour,ICombatDamageReceiver
    {
        public QueenBossBattle Battle;
        public bool IsDamageable=>Battle&&Battle.Fighting&&!Battle.Dodging;
        public Transform DamageRoot=>transform;
        public void ReceiveDamage(float damage,Transform attacker){if(IsDamageable)Battle.ReceiveBossDamage(damage);}
    }
}
