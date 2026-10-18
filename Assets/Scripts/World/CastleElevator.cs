using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace StarterAssets
{
 [DefaultExecutionOrder(-100)]
 public class CastleElevator : MonoBehaviour
 {
  public Transform Platform;
  public BoxCollider Deck;
  public float LowerHeight,UpperHeight,Speed=2.5f;
  public Vector3 LowerCallPoint,UpperCallPoint;
  public float InteractionRange=4f;
  CharacterController player;TransportPassenger passenger;float target;bool moving;
  void Start(){target=LowerHeight;FindPlayer();}
  void FindPlayer(){var go=GameObject.FindGameObjectWithTag("Player");if(go)player=go.GetComponent<CharacterController>();}
  bool Riding(){return TransportPassenger.Contains(player,Deck);}
  void Update()
  {
   if(!player)FindPlayer();if(!Platform)return;
   bool pressed=false;
#if ENABLE_INPUT_SYSTEM
   pressed=Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame;
#else
   pressed=Input.GetKeyDown(KeyCode.E);
#endif
   if(pressed&&!moving&&player){Interact();}
   Advance();
  }
  void Interact()
  {
   if(Riding()){passenger=TransportPassenger.Get(player);passenger.Attach(Platform,Deck);target=Mathf.Abs(Platform.position.y-LowerHeight)<.1f?UpperHeight:LowerHeight;moving=true;}
   else if(Vector3.Distance(player.transform.position,LowerCallPoint)<InteractionRange){target=LowerHeight;moving=true;}
   else if(Vector3.Distance(player.transform.position,UpperCallPoint)<InteractionRange){target=UpperHeight;moving=true;}
  }
  void Advance()
  {
   if(!Platform||!Deck)return;
   if(!moving){if(Riding())TransportPassenger.Support(player,Platform,Deck);return;}
   if(Riding()&&(!passenger||!passenger.Attached)){passenger=TransportPassenger.Get(player);passenger.Attach(Platform,Deck);}
   Vector3 previous=Platform.position;float y=Mathf.MoveTowards(previous.y,target,Speed*Time.deltaTime);Platform.position=new Vector3(previous.x,y,previous.z);Physics.SyncTransforms();

   if(passenger)passenger.Follow();
   if(Mathf.Abs(y-target)<.001f){moving=false;if(passenger)passenger.Release();passenger=null;}
  }
  public bool NpcMoving=>moving;
  public bool NpcAtStop(bool upper)=>!moving&&Platform&&Mathf.Abs(Platform.position.y-(upper?UpperHeight:LowerHeight))<.08f;
  public bool TryNpcMove(bool upper){if(moving||!Platform)return false;target=upper?UpperHeight:LowerHeight;moving=true;return true;}
  void OnDisable(){if(passenger)passenger.Release();}
 }
}


