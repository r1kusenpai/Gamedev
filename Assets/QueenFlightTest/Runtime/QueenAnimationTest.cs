using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace QueenFlightTest
{
    public sealed class QueenAnimationTest : MonoBehaviour
    {
        public Animator Queen;
        public AnimationClip[] Clips;
        public GameObject[] EffectPrefabs;
        public Transform Target;
        public bool AutoSequence = true;
        public int CurrentIndex { get; private set; }
        public int CompletedClips { get; private set; }
        public int SpawnedEffects { get; private set; }
        public bool Playing => graph.IsValid() && graph.IsPlaying();
        public float ClipTime => (float)(current.IsValid()?current.GetTime():0);
        public string Status { get; private set; }
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable current, previous;
        float elapsed,blend;
        bool fired;
        Transform rightHand,leftHand;
        Vector3 origin;
        static readonly string[] Labels={"Hands-together idle","Flying movement","Fast dodge left","Fast dodge right","Fireball / right-hand cast","Lightning / left-hand cast","Light blast / heavy attack"};

        void Start()
        {
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(!Queen || Clips==null || Clips.Length!=7 || Array.Exists(Clips,c=>!c)){Status="Seven animation clips are required.";enabled=false;return;}
            origin=Queen.transform.localPosition;
            foreach(var t in Queen.GetComponentsInChildren<Transform>())
            {if(t.name=="Right_Hand")rightHand=t;if(t.name=="Left_Hand")leftHand=t;}
            // A single owner plays the imported Generic rig. No old controller or exit-time conditions.
            Queen.runtimeAnimatorController=null;Queen.applyRootMotion=false;Queen.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Clean queen animation test");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,2);var output=AnimationPlayableOutput.Create(graph,"Queen",Queen);output.SetSourcePlayable(mixer);
            graph.Play();Play(0);
        }

        public void Play(int index)
        {
            if(!graph.IsValid()||index<0||index>=Clips.Length)return;
            FinishBlend();
            if(current.IsValid())
            {graph.Disconnect(mixer,0);previous=current;graph.Connect(previous,0,mixer,1);mixer.SetInputWeight(1,1);}
            current=AnimationClipPlayable.Create(graph,Clips[index]);current.SetApplyFootIK(false);current.SetApplyPlayableIK(false);current.SetTime(0);current.SetDuration(double.PositiveInfinity);
            graph.Connect(current,0,mixer,0);mixer.SetInputWeight(0,previous.IsValid()?0:1);
            CurrentIndex=index;elapsed=0;blend=0;fired=false;Queen.transform.localPosition=origin;
            Status=Labels[index];graph.Evaluate(0);
        }

        void FinishBlend()
        {
            if(previous.IsValid()){graph.Disconnect(mixer,1);graph.DestroyPlayable(previous);previous=default;}
            if(mixer.IsValid()){mixer.SetInputWeight(0,1);mixer.SetInputWeight(1,0);}
        }

        void Update()
        {
            if(!graph.IsValid())return;
            HandleInput();float dt=Time.deltaTime;elapsed+=dt;
            if(previous.IsValid())
            {blend+=dt;float weight=Mathf.SmoothStep(0,1,blend/.12f);mixer.SetInputWeight(0,weight);mixer.SetInputWeight(1,1-weight);if(weight>=1)FinishBlend();}
            // Explicit clip time and completion prevent the first looping idle from trapping the sequence.
            double time=Math.Min(elapsed,Clips[CurrentIndex].length);
            current.SetTime(time);if(previous.IsValid())previous.SetTime(previous.GetTime()+dt);
            graph.Evaluate(0);
            if(!fired && CurrentIndex>=4 && elapsed>=Clips[CurrentIndex].length*(CurrentIndex==6?.65f:.47f))
            {fired=true;SpawnSpell(CurrentIndex-4);}
            if(elapsed>=Clips[CurrentIndex].length+(CurrentIndex<2?.2f:.3f))
            {
                CompletedClips++;
                if(AutoSequence)Play((CurrentIndex+1)%Clips.Length);
                else if(CurrentIndex!=0)Play(0);
                else elapsed=0;
            }
        }

        public void SpawnSpell(int index)
        {
            if(index<0||index>=EffectPrefabs.Length||!EffectPrefabs[index])return;
            Transform hand=index==1?leftHand:rightHand;
            Vector3 position=index==2?Queen.transform.position+Vector3.up*.6f:hand?hand.position:Queen.transform.position+Vector3.up*1.4f;
            var effect=Instantiate(EffectPrefabs[index],position,Quaternion.identity).GetComponent<QueenSpellEffect>();effect.Target=Target;SpawnedEffects++;
        }

        void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var k=Keyboard.current;if(k==null)return;
            var keys=new[]{k.digit1Key,k.digit2Key,k.digit3Key,k.digit4Key,k.digit5Key,k.digit6Key,k.digit7Key};
            for(int i=0;i<keys.Length;i++)if(keys[i].wasPressedThisFrame){AutoSequence=false;Play(i);}
            if(k.spaceKey.wasPressedThisFrame){AutoSequence=!AutoSequence;if(AutoSequence)Play(0);}
            if(k.rKey.wasPressedThisFrame)ResetTest();
            if(Target){Vector3 move=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),0,(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));Target.position+=move.normalized*Time.deltaTime*2.5f;var p=Target.position;p.x=Mathf.Clamp(p.x,-3,3);p.z=Mathf.Clamp(p.z,2,5);Target.position=p;}
#endif
        }

        public void ResetTest()
        {foreach(var e in FindObjectsByType<QueenSpellEffect>(FindObjectsSortMode.None))Destroy(e.gameObject);if(Target)Target.position=new Vector3(0,0,3.5f);Play(0);}

        void OnGUI()
        {
            float scale=Mathf.Clamp(Screen.height/800f,.75f,1.4f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUILayout.BeginArea(new Rect(16,16,330,430),GUI.skin.box);
            GUILayout.Label("QUEEN — CLEAN ANIMATION TEST");GUILayout.Label(Status??"Loading...");
            GUILayout.Label("1–7: choose animation    Space: auto sequence");GUILayout.Label("WASD: move target    R: reset");
            bool auto=GUILayout.Toggle(AutoSequence,"Play all animations automatically");if(auto!=AutoSequence){AutoSequence=auto;if(auto)Play(0);}
            for(int i=0;i<Labels.Length;i++){int pick=i;if(GUILayout.Button((i+1)+"  "+Labels[i],GUILayout.Height(31))){AutoSequence=false;Play(pick);}}
            GUILayout.Label("Effects: "+SpawnedEffects+"   Completed clips: "+CompletedClips);
            if(GUILayout.Button("Reset test"))ResetTest();GUILayout.EndArea();
        }
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
