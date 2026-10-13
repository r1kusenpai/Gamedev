using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace StarterAssets
{
 [DefaultExecutionOrder(-100)] public class CastleFunicular : MonoBehaviour
 {
  public Transform[] Carts;
  public BoxCollider[] Decks;
  public Vector3[] BottomPoints,TopPoints;
  public Vector3 LowerStation,UpperStation;
  public float Speed=4f;
  CharacterController player;TransportPassenger passenger;float phase,target;bool moving;
  void Start(){FindPlayer();Place();}
  void FindPlayer(){var go=GameObject.FindGameObjectWithTag("Player");if(go)player=go.GetComponent<CharacterController>();}
  int Rider(){for(int i=0;i<2;i++)if(TransportPassenger.Contains(player,Decks[i]))return i;return -1;}
  void Update()
  {
   if(!player)FindPlayer();if(!player)return;bool pressed=false;
#if ENABLE_INPUT_SYSTEM
   pressed=Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame;
#else
   pressed=Input.GetKeyDown(KeyCode.E);
#endif
   if(pressed&&!moving){Interact();}
   Advance();
  }
  void Interact()
  {
   int rider=Rider();if(rider>=0){passenger=TransportPassenger.Get(player);passenger.Attach(Carts[rider],Decks[rider]);target=phase<.5f?1f:0f;moving=true;}else if(Vector3.Distance(player.transform.position,LowerStation)<5f)CallFromStation(false);else if(Vector3.Distance(player.transform.position,UpperStation)<5f)CallFromStation(true);
  }
  void Advance()
  {
   if(!moving){int rider=Rider();if(rider>=0)TransportPassenger.Support(player,Carts[rider],Decks[rider]);return;}
   int aboard=Rider();if(aboard>=0&&(!passenger||!passenger.Attached)){passenger=TransportPassenger.Get(player);passenger.Attach(Carts[aboard],Decks[aboard]);}
   phase=Mathf.MoveTowards(phase,target,Speed*Time.deltaTime/Vector3.Distance(BottomPoints[0],TopPoints[0]));Place();Physics.SyncTransforms();
   if(passenger)passenger.Follow();
   if(Mathf.Abs(phase-target)<.0001f){moving=false;if(passenger)passenger.Release();passenger=null;}
  }
  void OnDisable(){if(passenger)passenger.Release();}
  void Place(){if(Carts==null||Carts.Length!=2)return;Carts[0].position=Vector3.Lerp(BottomPoints[0],TopPoints[0],phase);Carts[1].position=Vector3.Lerp(BottomPoints[1],TopPoints[1],1-phase);}
  public void CallFromStation(bool upper){if(moving)return;int cart=Vector3.Distance(Carts[0].position,upper?UpperStation:LowerStation)<Vector3.Distance(Carts[1].position,upper?UpperStation:LowerStation)?0:1;target=upper?(cart==0?1:0):(cart==0?0:1);moving=true;}
 }
}



