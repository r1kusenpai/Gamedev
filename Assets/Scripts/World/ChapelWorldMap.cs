using System.Collections;
using StarterAssets;
using UnityEngine;
public sealed partial class ChapelThroneDomain
{
    public Texture2D WorldMapTexture;
    public Vector2 MapWorldMin=new Vector2(-500,-100),MapWorldMax=new Vector2(300,700);
    public bool MapOpen=>mapOpen;
    bool mapOpen;int mapSelection;
    GUIStyle mapHeading,mapText,mapSmall,mapChoice,mapSelected;
    public bool OpenMap()
    {
        if(mapOpen||menu||busy||DomainInteractionState.IsModal||QueenBattle.QueenBossBattle.BattleActive)return false;
        FindPlayer();if(!Player)return false;mapOpen=true;error="";ResolveMapDestinations();LockPlayer();return true;
    }
    public void CloseMap(){if(!mapOpen||busy)return;mapOpen=false;UnlockPlayer();nextInteraction=Time.unscaledTime+.25f;}
    public bool MapTeleportToThrone()
    {
        if(!mapOpen||busy||!Player||!OutsideReturn)return false;
        selectedOutsideReturn=OutsideReturn;mapOpen=false;busy=true;
        StartCoroutine(InDomain?MapMove(room?room.Arrival:null):Enter());return true;
    }
    public bool MapTeleportToCourtyard()
    {
        if(!mapOpen||busy||!Player||!OutsideReturn)return false;
        selectedOutsideReturn=OutsideReturn;mapOpen=false;busy=true;
        StartCoroutine(InDomain?Leave():MapMove(OutsideReturn));return true;
    }
    IEnumerator MapMove(Transform destination)
    {
        yield return FadeTo(1);
        if(!destination||!Supported(destination.position)){Fail("This destination is unavailable. Please try again.");yield break;}
        Teleport(destination.position,destination.rotation);yield return null;yield return FadeTo(0);
        busy=false;nextInteraction=Time.unscaledTime+1;UnlockPlayer();
    }

    public Transform CastleCircleTeleport,CastleFirstFloorTeleport,WildernessTeleport;
    Vector3 mapInitialPlayerPosition;Quaternion mapInitialPlayerRotation;bool mapInitialPlayerCaptured;
    void CaptureMapStart(){if(!mapInitialPlayerCaptured&&Player){mapInitialPlayerPosition=Player.position;mapInitialPlayerRotation=Player.rotation;mapInitialPlayerCaptured=true;}}
    static readonly string[] DestinationNames={"Throne Hall","Chapel Courtyard","Outside Castle Circle","Castle - First Floor","Outside Wilderness"};
    static readonly string[] DestinationDescriptions={"Enter the royal throne hall.","Return to the chapel courtyard.","Travel to the center of the circle outside the castle.","Arrive beside the cart boarding area on the castle's first floor.","Return to the central wilderness point where you first start playing."};
    Transform FindMapFloor(string path){var go=GameObject.Find(path);return go?go.transform:null;}
    bool TryMapLanding(Vector3 center,float surfaceY,out Vector3 point){
        for(int ring=0;ring<=8;ring++)for(int step=0;step<(ring==0?1:16);step++){
            float angle=step*Mathf.PI/8;var seed=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*2;
            var origin=new Vector3(seed.x,surfaceY+1.5f,seed.z);
            var hits=Physics.RaycastAll(origin,Vector3.down,8f,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits){
                if(hit.normal.y<.75f||hit.collider is CharacterController||Player&&hit.transform.IsChildOf(Player))continue;
                var pos=hit.point+Vector3.up*.06f;bool blocked=false;
                foreach(var c in Physics.OverlapCapsule(pos+Vector3.up*.38f,pos+Vector3.up*1.5f,.30f,~0,QueryTriggerInteraction.Ignore))
                    if(c!=hit.collider&&!(c is CharacterController)&&(!Player||!c.transform.IsChildOf(Player))){blocked=true;break;}
                if(!blocked){point=pos;return true;}
            }
        }
        point=default;return false;
    }
    Transform CreateMapAnchor(string label,Vector3 center,float surfaceY){
        if(!TryMapLanding(center,surfaceY,out var point))return null;
        var anchor=new GameObject(label).transform;anchor.SetParent(transform,false);anchor.SetPositionAndRotation(point,Quaternion.identity);return anchor;
    }
    void ResolveMapDestinations(){
        Physics.SyncTransforms();
        if(!CastleCircleTeleport){
            var roads=FindMapFloor("Lower Castle City/Winding Roads and Market Square");
            if(roads){var renderers=roads.GetComponentsInChildren<Renderer>();var loops=new System.Collections.Generic.List<Renderer>();foreach(var r in renderers)if(r.name.StartsWith("Neighborhood Loop"))loops.Add(r);if(loops.Count>0)renderers=loops.ToArray();if(renderers.Length>0){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);CastleCircleTeleport=CreateMapAnchor("Map - Outside Castle Circle",b.center,b.max.y);}}
        }
        {
            // First castle floor is the funicular's upper station, beside the stationary
            // boarding bridge, not a guessed position inside the castle floor bounds.
            var cartRoot=FindMapFloor("Castle Mountain/Castle Two Track Funicular");
            var cart=cartRoot?cartRoot.GetComponent<CastleFunicular>():null;
            if(cart){
                Vector3 approach=cart.UpperStation+Vector3.forward*2f;
                if(TryMapLanding(approach,cart.UpperStation.y,out var landing)){
                    if(!CastleFirstFloorTeleport){CastleFirstFloorTeleport=new GameObject("Map - Castle First Floor Near Cart").transform;CastleFirstFloorTeleport.SetParent(transform,false);}
                    Vector3 towardCart=cart.UpperStation-landing;towardCart.y=0;
                    CastleFirstFloorTeleport.SetPositionAndRotation(landing,towardCart.sqrMagnitude>.01f?Quaternion.LookRotation(towardCart):Quaternion.identity);
                }
            }
        }
        if(mapInitialPlayerCaptured){
            // Return to the original Play-mode spawn, not an enemy-pack spawn.
            if(!WildernessTeleport){WildernessTeleport=new GameObject("Map - Wilderness Starting Point").transform;WildernessTeleport.SetParent(transform,false);}
            Vector3 landing=mapInitialPlayerPosition;
            if(TryMapLanding(mapInitialPlayerPosition,mapInitialPlayerPosition.y,out var supported))landing=supported;
            WildernessTeleport.SetPositionAndRotation(landing,mapInitialPlayerRotation);
        }
    }
    Transform SelectedMapAnchor()=>mapSelection==1?OutsideReturn:mapSelection==2?CastleCircleTeleport:mapSelection==3?CastleFirstFloorTeleport:mapSelection==4?WildernessTeleport:null;
    public bool MapTeleportSelected(){
        if(mapSelection==0)return MapTeleportToThrone();
        if(mapSelection==1)return MapTeleportToCourtyard();
        if(!mapOpen||busy||!Player)return false;
        ResolveMapDestinations();var destination=SelectedMapAnchor();
        if(!destination){error="This destination has no clear landing point.";return false;}
        selectedOutsideReturn=destination;mapOpen=false;busy=true;
        StartCoroutine(InDomain?Leave():MapMove(destination));return true;
    }
    void MapStyles(){
        if(mapHeading!=null)return;
        mapHeading=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold,normal={textColor=new Color(.95f,.85f,.62f)}};
        mapText=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true,normal={textColor=new Color(.86f,.88f,.9f)}};
        mapSmall=new GUIStyle(mapText){fontSize=14};
        mapChoice=new GUIStyle(GUI.skin.button){fontSize=18,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(16,10,6,6)};
        mapSelected=new GUIStyle(mapChoice){fontStyle=FontStyle.Bold,normal={textColor=new Color(1,.85f,.42f)}};
    }
    static void MapBox(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
    void DrawWorldMap(){
        MapStyles();var oldMatrix=GUI.matrix;float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        float x=Screen.width/scale-424,y=(Screen.height/scale-620)*.5f;
        MapBox(new Rect(x,y,400,620),new Color(.035f,.045f,.065f,.95f));
        GUI.Label(new Rect(x+20,y+16,260,40),"TELEPORT",mapHeading);
        if(GUI.Button(new Rect(x+306,y+20,74,30),"Close"))CloseMap();
        for(int i=0;i<DestinationNames.Length;i++)if(GUI.Button(new Rect(x+20,y+76+i*58,360,50),DestinationNames[i],mapSelection==i?mapSelected:mapChoice)){mapSelection=i;error="";}
        GUI.Label(new Rect(x+20,y+384,360,70),DestinationDescriptions[mapSelection],mapText);
        GUI.Label(new Rect(x+20,y+460,360,42),InDomain?"Current location: Throne Hall":"Current location: Castle & City",mapSmall);
        if(!string.IsNullOrEmpty(error))GUI.Label(new Rect(x+20,y+498,360,38),error,mapSmall);
        var oldEnabled=GUI.enabled;GUI.enabled=Player&&!busy&&(mapSelection==0?OutsideReturn:SelectedMapAnchor());
        if(GUI.Button(new Rect(x+20,y+542,360,50),"Teleport",buttonStyle))MapTeleportSelected();
        GUI.enabled=oldEnabled;GUI.matrix=oldMatrix;
    }
}
