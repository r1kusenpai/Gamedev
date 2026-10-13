using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace StarterAssets
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(Animator))]
    public class CQCHostageVictimClips : MonoBehaviour
    {
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimatorControllerPlayable controller;
        private AnimationClip takeClip, idleClip, releaseClip;
        private AnimationClipPlayable take, idle, release;
        private AnimationClip stunClip, backwardClip, dyingClip, shoulderThrowClip;
        private AnimationClipPlayable stun, backward, dying, shoulderThrow;
        private int phase;
        private float elapsed, duration;
        private Animator animator;
        private CharacterController bodyController, throwerController;
        private bool shoulderThrowFinished;
        private bool initializingThrow;
        private bool previousThrowCollisionIgnored;
        public bool IsSequenceActive => phase > 0 && phase <= 3;
        public bool IsReactionActive => phase >= 4;
        public float CurrentReactionDuration => duration;
        public bool IsReady => graph.IsValid();
        public float TakeLength => takeClip ? takeClip.length : 0f;
        public float ReleaseLength => releaseClip ? releaseClip.length : 0f;
        public float ShoulderThrowProgress => phase == 7 ? Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration)) : 0f;
        private const float ShoulderThrowPlaybackSpeed = 1.35f;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            bodyController = GetComponentInParent<CharacterController>();
            takeClip = Load("Taken Hostage - Victim");
            idleClip = Load("Hostage Situation Idle - Hostage");
            releaseClip = Load("Release Hostage - Hostage");
            stunClip = Load("Getting Hit");
            if (!stunClip) stunClip = Load("Stunned");
            backwardClip = Load("Dying Backwards");
            dyingClip = Load("Dying");
            shoulderThrowClip = Load("Shoulder Throw, Victim");
            if (!takeClip || !idleClip || !releaseClip || !animator.runtimeAnimatorController)
            { Debug.LogWarning("Bot hostage clips are not ready. Prepare the animations first.", this); enabled = false; return; }
            graph = PlayableGraph.Create("CQC Hostage Bot");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            controller = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            take = AnimationClipPlayable.Create(graph, takeClip);
            idle = AnimationClipPlayable.Create(graph, idleClip);
            release = AnimationClipPlayable.Create(graph, releaseClip);
            mixer = AnimationMixerPlayable.Create(graph, 8);
            graph.Connect(controller, 0, mixer, 0);
            graph.Connect(take, 0, mixer, 1);
            graph.Connect(idle, 0, mixer, 2);
            graph.Connect(release, 0, mixer, 3);
            if (stunClip) { stun = AnimationClipPlayable.Create(graph, stunClip); graph.Connect(stun, 0, mixer, 4); }
            if (backwardClip) { backward = AnimationClipPlayable.Create(graph, backwardClip); graph.Connect(backward, 0, mixer, 5); }
            if (dyingClip) { dying = AnimationClipPlayable.Create(graph, dyingClip); graph.Connect(dying, 0, mixer, 6); }
            if (shoulderThrowClip) { shoulderThrow = AnimationClipPlayable.Create(graph, shoulderThrowClip); graph.Connect(shoulderThrow, 0, mixer, 7); }
            Weights(0);
            var output = AnimationPlayableOutput.Create(graph, "Hostage Victim", animator);
            output.SetSourcePlayable(mixer);
            // Consume movement only for the shoulder throw; other reactions stay baked in place.
            animator.applyRootMotion = true;
            graph.Play();
        }

        private AnimationClip Load(string name)
        {
            foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>("CQCAnimations/" + name))
                if (!clip.name.StartsWith("__preview__") && clip.isHumanMotion) return clip;
            return null;
        }
        public void SetFloat(string name, float value) { if (controller.IsValid()) controller.SetFloat(name, value); }
        public void SetBool(string name, bool value) { if (controller.IsValid()) controller.SetBool(name, value); }
        public void Begin(float pairDuration)
        {
            if (!IsReady) return;
            phase = 1; elapsed = 0f; duration = Mathf.Max(0.1f, pairDuration);
            take.SetTime(0f); take.SetSpeed(takeClip.length / duration); Weights(1);
        }
        public void Release(float pairDuration)
        {
            if (!IsReady) return;
            phase = 3; elapsed = 0f; duration = Mathf.Max(0.1f, pairDuration);
            release.SetTime(0f); release.SetSpeed(releaseClip.length / duration); Weights(3);
        }
        public void Cancel() { RestoreThrowCollision(); phase = 0; if (mixer.IsValid()) Weights(0); }
        public bool PlayStun(float seconds)
        {
            if (!IsReady || !stunClip || IsSequenceActive) return false;
            phase = 4; elapsed = 0f; duration = Mathf.Max(0.1f, stunClip.isLooping ? seconds : stunClip.length);
            stun.SetTime(0f); stun.SetSpeed(1f); Weights(4);
            return true;
        }
        public bool PlayKnockdown(bool fallBackwards, out float seconds)
        {
            seconds = 0f;
            if (!IsReady) return false;
            AnimationClip clip = fallBackwards && backwardClip ? backwardClip : dyingClip ? dyingClip : backwardClip;
            if (!clip) return false;
            phase = clip == backwardClip ? 5 : 6; elapsed = 0f; duration = clip.length; seconds = duration;
            var playable = phase == 5 ? backward : dying;
            playable.SetTime(0f); playable.SetSpeed(1f); Weights(phase);
            return true;
        }
        public bool PlayShoulderThrow(out float seconds, CharacterController thrower = null, Vector3 direction = default)
        {
            seconds = 0f;
            if (!IsReady || !shoulderThrowClip) return false;
            RestoreThrowCollision();
            throwerController = thrower;
            if (bodyController && throwerController)
            {
                previousThrowCollisionIgnored = Physics.GetIgnoreCollision(bodyController, throwerController);
                Physics.IgnoreCollision(bodyController, throwerController, true);
            }
            // This paired source travels diagonally, not along the robot's forward axis.
            // Orient its recorded travel toward the requested WASD direction.
            Vector3 sourceTravel = Vector3.ProjectOnPlane(shoulderThrowClip.averageSpeed, Vector3.up);
            if (bodyController && direction.sqrMagnitude > 0.001f && sourceTravel.sqrMagnitude > 0.001f)
                bodyController.transform.rotation = Quaternion.LookRotation(direction)
                    * Quaternion.Inverse(Quaternion.LookRotation(sourceTravel));
            phase = 7; elapsed = 0f; duration = shoulderThrowClip.length / ShoulderThrowPlaybackSpeed; seconds = duration;
            shoulderThrowFinished = false;
            shoulderThrow.SetTime(0f); shoulderThrow.SetSpeed(ShoulderThrowPlaybackSpeed); Weights(7);
            // Evaluate the first pose now so the throw starts on this frame, not one frame after the player.
            initializingThrow = true;
            graph.Evaluate(0f);
            initializingThrow = false;
            return true;
        }
        private void OnAnimatorMove()
        {
            if (initializingThrow || phase != 7 || shoulderThrowFinished || !bodyController || !bodyController.enabled) return;
            Vector3 delta = animator.deltaPosition;
            delta.y = 0f; // Clip supplies the body's lift; normal bot gravity keeps the collision root on the floor.
            if (float.IsNaN(delta.x) || float.IsNaN(delta.z) || float.IsInfinity(delta.x) || float.IsInfinity(delta.z)) return;
            bodyController.Move(delta);
        }
        private void RestoreThrowCollision()
        {
            if (bodyController && throwerController) Physics.IgnoreCollision(bodyController, throwerController, previousThrowCollisionIgnored);
            throwerController = null;
        }
        private void Weights(int input)
        { for (int i = 0; i < mixer.GetInputCount(); i++) mixer.SetInputWeight(i, i == input ? 1f : 0f); }
        private void Update()
        {
            if (!IsReady || phase == 0) return;
            elapsed += Time.deltaTime;
            if (phase == 1 && elapsed >= duration)
            {
                // Match the player's frozen end-of-grab pose for as long as the hold lasts.
                phase = 2;
                take.SetTime(Mathf.Max(0f, takeClip.length - 0.001f));
                take.SetSpeed(0f);
                Weights(1);
            }
            else if (phase == 3 && elapsed >= duration) Cancel();
            else if (phase == 4 && elapsed >= duration) Cancel();
            else if (phase == 5 || phase == 6 || phase == 7)
            {
                var playable = phase == 7 ? shoulderThrow : phase == 5 ? backward : dying;
                float endTime = phase == 7 ? shoulderThrowClip.length : duration;
                if (elapsed >= duration) { playable.SetTime(Mathf.Max(0f, endTime - 0.001f)); playable.SetSpeed(0f); }
            }
            if (phase == 4 && stun.GetTime() >= stunClip.length) stun.SetTime(stun.GetTime() % stunClip.length);
        }
        private void LateUpdate()
        {
            // Keep the final root delta before freezing the fallen pose.
            if (phase == 7 && elapsed >= duration && !shoulderThrowFinished)
            { shoulderThrowFinished = true; RestoreThrowCollision(); }
        }
        private void OnDisable() { RestoreThrowCollision(); phase = 0; if (graph.IsValid()) graph.Destroy(); }
        private void OnDestroy() { RestoreThrowCollision(); if (graph.IsValid()) graph.Destroy(); }
    }
}
