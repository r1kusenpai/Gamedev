using System.Collections.Generic;
using UnityEngine;
namespace StarterAssets {
 public partial class WeaponController {
  readonly HashSet<ICombatDamageReceiver> combatReceiverHits=new HashSet<ICombatDamageReceiver>();
  void ClearCombatReceiverHits(){combatReceiverHits.Clear();}
  void CheckCombatReceiverHits(Vector3 center,float radius){
   foreach(var col in Physics.OverlapSphere(center,radius,~0,QueryTriggerInteraction.Ignore)){
    var receiver=col.GetComponentInParent<ICombatDamageReceiver>();if(receiver==null||!receiver.IsDamageable||!receiver.DamageRoot||receiver.DamageRoot.IsChildOf(transform)||combatReceiverHits.Contains(receiver))continue;
    var toward=receiver.DamageRoot.position-transform.position;toward.y=0;if(toward.sqrMagnitude>.01f&&Vector3.Dot(transform.forward,toward.normalized)<.1f)continue;
    Vector3 start=transform.position+Vector3.up,finish=col.ClosestPoint(center);var delta=finish-start;bool blocked=false;
    if(delta.sqrMagnitude>.01f)foreach(var hit in Physics.RaycastAll(start,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)){
     if(hit.transform.IsChildOf(transform))continue;var other=hit.collider.GetComponentInParent<ICombatDamageReceiver>();if(other==receiver)continue;blocked=true;break;
    }
    if(blocked)continue;combatReceiverHits.Add(receiver);receiver.ReceiveDamage(attackDamage,transform);NoteSlashContact();
   }
  }
 }
}
