using UnityEngine;
namespace StarterAssets
{
 [DefaultExecutionOrder(-50)] public class TransportPassenger : MonoBehaviour
 {
  public bool Attached { get; private set; }
  Transform carrier;BoxCollider deck;CharacterController controller;Matrix4x4 previous;
  public bool IsSupported => Attached&&controller&&deck&&Contains(controller,deck)&&Foot(controller).y<=deck.bounds.max.y+.15f;
  public static TransportPassenger Get(CharacterController player){var p=player.GetComponent<TransportPassenger>();return p?p:player.gameObject.AddComponent<TransportPassenger>();}
  public void Attach(Transform vehicle,BoxCollider floor)
  {
   if(Attached&&carrier==vehicle)return;Release();controller=GetComponent<CharacterController>();carrier=vehicle;deck=floor;previous=carrier.localToWorldMatrix;Attached=true;
  }
  public void Follow()
  {
   if(!Attached||!carrier||!controller)return;
   // Apply only the carrier's displacement. The player's own position and input are retained.
   Vector3 next=carrier.TransformPoint(previous.inverse.MultiplyPoint3x4(transform.position));previous=carrier.localToWorldMatrix;
   if((next-transform.position).sqrMagnitude<.00000001f)return;
   bool enabledBefore=controller.enabled;controller.enabled=false;transform.position=next;controller.enabled=enabledBefore;
  }
  public void Release(){Attached=false;carrier=null;deck=null;}
  void LateUpdate(){if(Attached&&(!carrier||!deck||!Contains(controller,deck)))Release();}
  void OnDisable(){Release();}
  public static Vector3 Foot(CharacterController c){return c.transform.TransformPoint(c.center)-Vector3.up*(c.height*.5f*Mathf.Abs(c.transform.lossyScale.y));}
  public static bool Contains(CharacterController c,BoxCollider floor)
  {
   if(!c||!floor||!floor.enabled)return false;Vector3 p=floor.transform.InverseTransformPoint(Foot(c))-floor.center;Vector3 half=floor.size*.5f;Vector3 scale=floor.transform.lossyScale;
   float h=(p.y-half.y)*Mathf.Abs(scale.y);
   return Mathf.Abs(p.x)<=half.x-.04f/Mathf.Abs(scale.x)&&Mathf.Abs(p.z)<=half.z-.04f/Mathf.Abs(scale.z)&&h>=-.25f&&h<=.7f;
  }
  public static void Support(CharacterController c,Transform vehicle,BoxCollider floor)
  {
   if(!c||!c.enabled||!Contains(c,floor))return;float correction=floor.bounds.max.y+.025f-Foot(c).y;
   if(correction<=0||correction>.2f)return;bool enabledBefore=c.enabled;c.enabled=false;c.transform.position+=Vector3.up*correction;c.enabled=enabledBefore;
  }
 }
}
