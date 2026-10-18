using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-200)]
public sealed partial class ChapelThroneDomain : MonoBehaviour
{
    public const string ScenePath="Assets/Scenes/ThroneDomain.unity";
    public Transform ChapelDoor,OutsideReturn,Player;
    public Transform BackDoor,BackOutsideReturn;
    Transform selectedOutsideReturn;
    public float InteractionRange=5f;
    public bool InDomain {get;private set;}
    public bool MenuOpen=>menu;
    public bool IsTravelling=>busy;
    public string LastError=>error;
    bool menu,busy,locked,oldCursorVisible,oldCursorInput,oldCursorLocked,oldMovement,oldWeapon;
    float oldTimeScale=1,fade,nextInteraction;CursorLockMode oldCursor;
    StarterAssetsInputs input;ThirdPersonController movement;WeaponController weapon;CharacterController controller;
    Scene outsideScene;ThroneDomainRoom room;string error="";
    Camera sceneCamera;CameraClearFlags previousClearFlags;Color previousBackground;
    readonly List<Light> outsideLights=new List<Light>();
    GUIStyle titleStyle,bodyStyle,buttonStyle,hintStyle;Texture2D panelTexture;

    void FindPlayer()
    {
        if(!Player){var go=GameObject.FindGameObjectWithTag("Player");if(go)Player=go.transform;}
        if(!Player)return;
        input=Player.GetComponent<StarterAssetsInputs>();movement=Player.GetComponent<ThirdPersonController>();weapon=Player.GetComponent<WeaponController>();controller=Player.GetComponent<CharacterController>();
    }
    void Start(){FindPlayer();CaptureMapStart();outsideScene=gameObject.scene;}
    bool NearDoor(Transform door)
    {
        if(!door||!Player)return false;
        var delta=Player.position-door.position;
        if(Mathf.Abs(delta.y)>=3f||new Vector2(delta.x,delta.z).sqrMagnitude>InteractionRange*InteractionRange)return false;
        return true;
    }
    Transform NearbyOutsideReturn()
    {
        bool front=NearDoor(ChapelDoor)&&OutsideReturn;
        bool back=NearDoor(BackDoor)&&BackOutsideReturn;
        if(back&&(!front||(Player.position-BackDoor.position).sqrMagnitude<(Player.position-ChapelDoor.position).sqrMagnitude))return BackOutsideReturn;
        return front?OutsideReturn:null;
    }
    public bool CanInteract()
    {
        if(!Player||busy||Time.unscaledTime<nextInteraction||QueenBattle.QueenBossBattle.BattleActive)return false;
        return InDomain?room&&NearDoor(room.ReturnDoor):NearbyOutsideReturn()!=null;
    }
    void Update()
    {
        if(!Player)FindPlayer();CaptureMapStart();if(busy)return;
        bool use=false,cancel=false,accept=false,toggleMap=false;
#if ENABLE_INPUT_SYSTEM
        var keyboard=Keyboard.current;if(keyboard!=null){use=keyboard.eKey.wasPressedThisFrame;cancel=keyboard.escapeKey.wasPressedThisFrame;accept=keyboard.enterKey.wasPressedThisFrame;toggleMap=keyboard.mKey.wasPressedThisFrame;}
#else
        use=Input.GetKeyDown(KeyCode.E);cancel=Input.GetKeyDown(KeyCode.Escape);accept=Input.GetKeyDown(KeyCode.Return);toggleMap=Input.GetKeyDown(KeyCode.M);
#endif
        if(mapOpen){if(toggleMap||cancel)CloseMap();return;}
        if(toggleMap&&!menu){OpenMap();return;}
        if(menu){if(cancel)CancelConfirmation();else if(accept)ConfirmTravel();}
        else if(use&&CanInteract())OpenConfirmation();
    }
    public bool OpenConfirmation()
    {
        if(menu||mapOpen||!CanInteract())return false;
        if(!InDomain)selectedOutsideReturn=NearbyOutsideReturn();
        error="";menu=true;LockPlayer();return true;
    }
    public void CancelConfirmation(){if(busy)return;menu=false;error="";UnlockPlayer();nextInteraction=Time.unscaledTime+.25f;}
    public void ConfirmTravel(){if(!menu||busy)return;menu=false;busy=true;StartCoroutine(InDomain?Leave():Enter());}
    void LockPlayer()
    {
        if(locked)return;FindPlayer();locked=true;oldTimeScale=Time.timeScale;Time.timeScale=0;DomainInteractionState.IsModal=true;
        oldCursor=Cursor.lockState;oldCursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        if(input){oldCursorInput=input.cursorInputForLook;oldCursorLocked=input.cursorLocked;input.cursorInputForLook=false;input.cursorLocked=false;ClearInput();}
        if(movement){oldMovement=movement.enabled;movement.enabled=false;}
        if(weapon){oldWeapon=weapon.enabled;weapon.enabled=false;}
    }
    void ClearInput(){if(input){input.move=Vector2.zero;input.look=Vector2.zero;input.jump=false;input.sprint=false;}}
    void UnlockPlayer()
    {
        if(!locked)return;ClearInput();Time.timeScale=oldTimeScale;DomainInteractionState.IsModal=false;
        if(input){input.cursorInputForLook=oldCursorInput;input.cursorLocked=oldCursorLocked;}
        if(movement)movement.enabled=oldMovement;if(weapon)weapon.enabled=oldWeapon;
        Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;locked=false;
    }
    IEnumerator FadeTo(float target)
    {
        while(Mathf.Abs(fade-target)>.001f){fade=Mathf.MoveTowards(fade,target,Time.unscaledDeltaTime*3f);yield return null;}fade=target;
    }
    IEnumerator Enter()
    {
        yield return FadeTo(1);Scene destination=SceneManager.GetSceneByPath(ScenePath);AsyncOperation operation=null;
        if(!destination.isLoaded)
        {
            try{operation=SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Additive);}
            catch(Exception e){error="The throne hall could not be opened. "+e.Message;}
            if(operation!=null)while(!operation.isDone)yield return null;
            destination=SceneManager.GetSceneByPath(ScenePath);
        }
        if(!destination.isLoaded){Fail("The throne hall could not be loaded. Please try again.");yield break;}
        foreach(var root in destination.GetRootGameObjects()){room=root.GetComponentInChildren<ThroneDomainRoom>();if(room)break;}
        if(!room||!room.Arrival||!room.ReturnDoor){SceneManager.UnloadSceneAsync(destination);Fail("The throne hall entrance is unavailable.");yield break;}
        if(!Supported(room.Arrival.position)){SceneManager.UnloadSceneAsync(destination);Fail("The throne hall arrival floor is unavailable.");yield break;}
        outsideScene=gameObject.scene;outsideLights.Clear();foreach(var root in outsideScene.GetRootGameObjects())foreach(var light in root.GetComponentsInChildren<Light>())if(light.enabled){outsideLights.Add(light);light.enabled=false;}
        sceneCamera=Camera.main;if(sceneCamera){previousClearFlags=sceneCamera.clearFlags;previousBackground=sceneCamera.backgroundColor;sceneCamera.clearFlags=CameraClearFlags.SolidColor;sceneCamera.backgroundColor=new Color(.02f,.025f,.035f);}
        SceneManager.SetActiveScene(destination);Teleport(room.Arrival.position,room.Arrival.rotation);InDomain=true;
        yield return null;yield return FadeTo(0);busy=false;nextInteraction=Time.unscaledTime+1f;UnlockPlayer();
    }
    IEnumerator Leave()
    {
        yield return FadeTo(1);
        var returnPoint=selectedOutsideReturn?selectedOutsideReturn:OutsideReturn;
        if(!returnPoint||!Supported(returnPoint.position)){Fail("The chapel return point is unavailable.");yield break;}
        var destination=room?room.gameObject.scene:SceneManager.GetSceneByPath(ScenePath);
        if(outsideScene.IsValid()&&outsideScene.isLoaded)SceneManager.SetActiveScene(outsideScene);
        Teleport(returnPoint.position,returnPoint.rotation);InDomain=false;RestoreLights();room=null;
        if(destination.IsValid()&&destination.isLoaded){var operation=SceneManager.UnloadSceneAsync(destination);if(operation!=null)while(!operation.isDone)yield return null;}
        yield return null;yield return FadeTo(0);busy=false;nextInteraction=Time.unscaledTime+1f;UnlockPlayer();
    }
    static bool Supported(Vector3 p)=>Physics.Raycast(p+Vector3.up*.8f,Vector3.down,2f,~0,QueryTriggerInteraction.Ignore);
    void Teleport(Vector3 position,Quaternion rotation)
    {
        var passenger=Player.GetComponent<TransportPassenger>();if(passenger)passenger.Release();
        Vector3 delta=position-Player.position;bool enabledBefore=controller&&controller.enabled;if(controller)controller.enabled=false;
        Player.SetPositionAndRotation(position,rotation);if(controller)controller.enabled=enabledBefore;ClearInput();if(movement)movement.ResetAfterDomainTravel(rotation.eulerAngles.y);Physics.SyncTransforms();
        foreach(var camera in UnityEngine.Object.FindObjectsByType<CinemachineCamera>()){camera.OnTargetObjectWarped(Player,delta);if(movement&&movement.CinemachineCameraTarget)camera.OnTargetObjectWarped(movement.CinemachineCameraTarget.transform,delta);camera.PreviousStateIsValid=false;}
        if(Camera.main)Camera.main.transform.position+=delta;
    }
    void Fail(string message){if(string.IsNullOrEmpty(error))error=message;busy=false;menu=true;fade=0;Debug.LogWarning(error,this);}
    void RestoreLights(){foreach(var light in outsideLights)if(light)light.enabled=true;outsideLights.Clear();if(sceneCamera){sceneCamera.clearFlags=previousClearFlags;sceneCamera.backgroundColor=previousBackground;sceneCamera=null;}}
    void OnDisable(){StopAllCoroutines();busy=menu=mapOpen=false;fade=0;RestoreLights();UnlockPlayer();}
    void OnDestroy(){if(panelTexture)Destroy(panelTexture);}
    void Styles()
    {
        if(titleStyle!=null)return;
        titleStyle=new GUIStyle(GUI.skin.label){fontSize=30,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,normal={textColor=new Color(.95f,.84f,.59f)}};
        bodyStyle=new GUIStyle(GUI.skin.label){fontSize=19,alignment=TextAnchor.MiddleCenter,wordWrap=true,normal={textColor=new Color(.9f,.9f,.92f)}};
        buttonStyle=new GUIStyle(GUI.skin.button){fontSize=20,fontStyle=FontStyle.Bold};hintStyle=new GUIStyle(bodyStyle){fontSize=17};
        panelTexture=new Texture2D(1,1);panelTexture.SetPixel(0,0,new Color(.055f,.065f,.09f,.98f));panelTexture.Apply();
    }
    void OnGUI()
    {
        Styles();int oldDepth=GUI.depth;GUI.depth=-100;
        float width=Mathf.Min(600,Screen.width-32),x=(Screen.width-width)*.5f;
        if(mapOpen){DrawWorldMap();GUI.depth=oldDepth;return;}
        if(menu)
        {
            GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            float y=(Screen.height-320)*.5f;GUI.DrawTexture(new Rect(x,y,width,320),panelTexture);
            GUI.Label(new Rect(x+20,y+22,width-40,46),InDomain?"LEAVE THRONE HALL":"THRONE HALL",titleStyle);
            GUI.Label(new Rect(x+30,y+82,width-60,86),!string.IsNullOrEmpty(error)?error:InDomain?"Return to the chapel courtyard?":"Enter the chapel domain and approach the royal throne.",bodyStyle);
            if(string.IsNullOrEmpty(error)&&GUI.Button(new Rect(x+38,y+187,width-76,46),InDomain?"Return to Chapel":"Enter Throne Hall",buttonStyle))ConfirmTravel();
            if(GUI.Button(new Rect(x+38,y+245,width-76,42),string.IsNullOrEmpty(error)?"Cancel":"Close",buttonStyle))CancelConfirmation();
        }
        else if(!busy&&CanInteract())GUI.Label(new Rect(x,Screen.height-145,width,54),InDomain?"[ E ]  Return to Chapel":"[ E ]  Throne Hall",bodyStyle);
        if(fade>0){GUI.color=new Color(0,0,0,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=new Color(1,1,1,fade);GUI.Label(new Rect(x,Screen.height*.5f-30,width,60),InDomain?"THRONE HALL":"CHAPEL DOMAIN",titleStyle);GUI.color=Color.white;}
        GUI.depth=oldDepth;
    }
}
