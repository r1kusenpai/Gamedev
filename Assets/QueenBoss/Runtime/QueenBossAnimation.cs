using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace QueenBattle
{
    public sealed class QueenBossAnimation : MonoBehaviour
    {
        public AnimationClip[] Clips;
        public int Current { get; private set; }
        public float Elapsed { get; private set; }
        public float PlaybackSpeed=1;
        public bool IsReady=>graph.IsValid()&&graph.IsPlaying();
        public float Duration => Clips[Current].length;
        Animator animator;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable incoming,outgoing;
        Vector3 localOrigin;
        float blend;
        public void Initialize(Animator target,AnimationClip[] clips)
        {
            animator=target;Clips=clips;localOrigin=target.transform.localPosition;
            animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Queen boss animation");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,2);AnimationPlayableOutput.Create(graph,"Queen",animator).SetSourcePlayable(mixer);graph.Play();Play(0);
        }
        public void Play(int index)
        {
            if(!graph.IsValid())return;
            if(outgoing.IsValid()){graph.Disconnect(mixer,1);graph.DestroyPlayable(outgoing);outgoing=default;}
            if(incoming.IsValid()){graph.Disconnect(mixer,0);outgoing=incoming;graph.Connect(outgoing,0,mixer,1);}
            incoming=AnimationClipPlayable.Create(graph,Clips[index]);incoming.SetApplyFootIK(false);incoming.SetApplyPlayableIK(false);
            incoming.SetDuration(double.PositiveInfinity);graph.Connect(incoming,0,mixer,0);
            mixer.SetInputWeight(0,outgoing.IsValid()?0:1);mixer.SetInputWeight(1,outgoing.IsValid()?1:0);
            animator.transform.localPosition=localOrigin;Current=index;Elapsed=blend=0;graph.Evaluate(0);
        }
        void Update()
        {
            if(!graph.IsValid())return;float dt=Time.deltaTime;Elapsed+=dt*PlaybackSpeed;blend+=dt;
            float t=Current<2?Elapsed%Duration:Mathf.Min(Elapsed,Duration);incoming.SetTime(t);
            if(outgoing.IsValid())
            {
                outgoing.SetTime(outgoing.GetTime()+dt);float w=Mathf.SmoothStep(0,1,blend/.12f);mixer.SetInputWeight(0,w);mixer.SetInputWeight(1,1-w);
                if(w>=1){graph.Disconnect(mixer,1);graph.DestroyPlayable(outgoing);outgoing=default;}
            }
            graph.Evaluate(0);
        }
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
