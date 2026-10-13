using UnityEngine;
namespace StarterAssets
{
    // The practice bot is silent, but must receive the sound events in shared locomotion clips.
    public sealed class PracticeBotAnimationEvents : MonoBehaviour
    {
        public void OnFootstep(AnimationEvent animationEvent) { }
        public void OnLand(AnimationEvent animationEvent) { }
    }
}
