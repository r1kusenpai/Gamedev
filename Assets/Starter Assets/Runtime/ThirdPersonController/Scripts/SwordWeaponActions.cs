using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace StarterAssets
{
    public partial class WeaponController
    {
        [Header("One-handed sword animations")]
        public SwordAnimationSet SwordAnimations;
        public bool GreatswordUsesSwordAttacks = true;
        bool UsesSwordAttackSet => CurrentWeapon == Weapon.Sword || CurrentWeapon == Weapon.Greatsword && GreatswordUsesSwordAttacks;
        public bool UsesCopiedSwordAttacks => CurrentWeapon == Weapon.Greatsword && GreatswordUsesSwordAttacks && HasNewSwordAnimations;
        public bool SwordUseNormalLocomotion = true;
        public bool PreferDownloadedSwordAnimations = true;
        float swordActionWeight;
        AvatarMask swordMovingArmsMask;
        AnimationMixerPlayable swordArmsClips;
        readonly AnimationClipPlayable[] swordArmPlayers = new AnimationClipPlayable[2];
        readonly AnimationClip[] swordArmSources = new AnimationClip[2];
        int swordHitWindow = -1;
        public bool DownloadedSwordAttackActive => UsesSwordAttackSet && HasNewSwordAnimations && (SwordAnimations.OriginalDownloadedPlayback || SwordAnimations.ResponsiveDownloadedCombos) && swordSequence.Active;
        public bool OriginalSwordAttackActive => UsesSwordAttackSet && HasNewSwordAnimations && SwordAnimations.OriginalDownloadedPlayback && swordSequence.Active;
        bool SwordAuthoredTravel => UsesSwordAttackSet && HasNewSwordAnimations
            && SwordAnimations.UseAuthoredRootTravel && swordSequence.Active
            && movement && movement.Grounded && (SwordAnimations.OriginalDownloadedPlayback || !input || input.move.sqrMagnitude <= .01f)
            && (!posture || posture.CurrentStance == CQCPosture.Stance.Standing);
        readonly SwordAttackSequence swordSequence = new SwordAttackSequence();
        AnimationLayerMixerPlayable swordPoseLayers;
        AnimationClipPlayable swordMovementPlayer;
        AnimationClip swordMovementClip;
        CQCSprintPhase swordSprintPhase;
        bool WeaponSprintActive {
            get {
                if(!swordSprintPhase)swordSprintPhase=GetComponent<CQCSprintPhase>();
                return input && input.sprint || swordSprintPhase && swordSprintPhase.IsSecondPhase;
            }
        }

        bool HasNewSwordAnimations => SwordAnimations && SwordAnimations.IsReady && swordPoseLayers.IsValid();

        void InitializeSwordAnimations()
        {
            if (PreferDownloadedSwordAnimations)
            {
                var downloaded = Resources.Load<SwordAnimationSet>("WeaponAnimations/DownloadedSword/DownloadedSword");
                if (downloaded && downloaded.IsReady) SwordAnimations = downloaded;
            }
            if (!SwordAnimations) SwordAnimations = Resources.Load<SwordAnimationSet>("WeaponAnimations/DoubleLSword/RecommendedSword");
            if (!SwordAnimations || !SwordAnimations.IsReady) return;
            swordPoseLayers = AnimationLayerMixerPlayable.Create(graph, 2);
            overlay.DisconnectInput(1);
            graph.Connect(clips, 0, swordPoseLayers, 1);
            graph.Connect(swordPoseLayers, 0, overlay, 1);
            swordPoseLayers.SetInputWeight(0, 0);
            swordPoseLayers.SetInputWeight(1, 1);
            swordPoseLayers.SetLayerMaskFromAvatarMask(1, fullMask);
            overlay.SetInputCount(3);
            swordArmsClips = AnimationMixerPlayable.Create(graph,2);
            graph.Connect(swordArmsClips,0,overlay,2);
            swordMovingArmsMask = new AvatarMask();
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++) {
                var part=(AvatarMaskBodyPart)i;
                swordMovingArmsMask.SetHumanoidBodyPartActive(part,part==AvatarMaskBodyPart.LeftArm || part==AvatarMaskBodyPart.RightArm || part==AvatarMaskBodyPart.LeftFingers || part==AvatarMaskBodyPart.RightFingers || part==AvatarMaskBodyPart.LeftHandIK || part==AvatarMaskBodyPart.RightHandIK);
            }
            overlay.SetLayerMaskFromAvatarMask(2,swordMovingArmsMask);
            overlay.SetInputWeight(2,0);
            InitializeDownloadedGreatCarry();
        }

        void ResetSwordLowerLayer()
        {
            if (!swordPoseLayers.IsValid()) return;
            overlay.SetInputWeight(2,0);
            swordPoseLayers.SetInputWeight(0, 0);
            swordPoseLayers.SetLayerMaskFromAvatarMask(1, fullMask);
        }

        void UpdateSword(bool down, bool up, bool held, bool crouched)
        {
            if (!Application.isFocused || Time.timeScale <= 0f)
            {
                CancelAction(); overlay.SetInputWeight(1, 0); return;
            }
            bool grounded = movement ? movement.Grounded : controller.isGrounded;
            bool moving = input && input.move.sqrMagnitude > .01f;
            bool sprinting = WeaponSprintActive;
            bool running = !crouched && moving && sprinting;
            UpdateSwordMovement(grounded, moving, running, crouched);
            swordSequence.ComboResetSeconds = Mathf.Max(.1f, ComboResetSeconds);
            swordSequence.ResponsiveInput = SwordAnimations.ResponsiveInput;
            swordSequence.ComboCount = SwordAnimations.ComboCount;
            swordSequence.SeparateRunningChain = SwordAnimations.ResponsiveDownloadedCombos;
            swordSequence.AutoCompleteRunningPair = SwordAnimations.ResponsiveDownloadedCombos;
            swordSequence.RunningComboFinisher = SwordAnimations.ResponsiveDownloadedCombos && SwordAnimations.FullComboFinisher;
            swordSequence.BufferSeconds = SwordAnimations.InputBufferSeconds;
            swordSequence.HoldThreshold = SwordAnimations.OriginalDownloadedPlayback || SwordAnimations.ResponsiveDownloadedCombos ? float.MaxValue : SwordAnimations.ResponsiveInput ? .32f : .25f;
            swordSequence.LinkProgress = SwordAnimations.OriginalDownloadedPlayback ? 1f : SwordAnimations.LinkAt(swordSequence.Current);
            if (SwordAnimations.OriginalDownloadedPlayback) { up = false; held = false; if (swordSequence.Active) down = false; }

            if (SwordAnimations.ResponsiveDownloadedCombos) { held = false; up = false; }
            swordSequence.MovementCancelProgress = SwordAnimations.IsFullCombo(swordSequence.Current) ? .62f : SwordAnimations.ResponsiveDownloadedCombos ? .82f : .75f;

            // Sweeping a time interval keeps a dropped frame from skipping the damage window.
            if (swordSequence.Active)
            {
                float before = swordSequence.Progress;
                float after = before + Time.deltaTime / Mathf.Max(.05f, actionLength);
                for(int window=0;window<SwordAnimations.HitWindowCount(swordSequence.Current);window++) {
                    var hit = SwordAnimations.ResponsiveInput ? SwordAnimations.HitWindow(swordSequence.Current,window) : new Vector2(.28f,.7f);
                    if (before <= hit.y && after >= hit.x) {
                        if(swordHitWindow!=window) { hits.Clear();ClearCombatReceiverHits();ResetSlashSoundWindow();swordHitWindow=window; }
                        CheckHit();
                    }
                }
            }
            var command = swordSequence.Tick(Time.deltaTime, down, up, held, grounded, running);
            if (command.Kind != SwordAttackSequence.AttackKind.None) BeginSwordAttack(command, moving);
            else if (moving && SwordAnimations.ResponsiveInput && !SwordAnimations.OriginalDownloadedPlayback) swordSequence.TryCancelRecovery();
            if (swordSequence.Active)
            {
                actionTime = Mathf.Max(.001f, actionLength * (1f - swordSequence.Progress));
                // Keep locomotion legs while attacking on the move, in a crouch or in the air.
                SetSwordPoseMask(!SwordAnimations.OriginalDownloadedPlayback && (moving || crouched || !grounded));
                if (!SwordAnimations.OriginalDownloadedPlayback && SwordUseNormalLocomotion && !crouched && (moving || !grounded))
                {
                    // Use the original controller's movement below the sword strike.
                    swordPoseLayers.SetInputWeight(0,0);
                    swordPoseLayers.SetLayerMaskFromAvatarMask(1,fullMask);
                    overlay.SetLayerMaskFromAvatarMask(1,upperMask);
                }
            }
            else
            {
                actionTime = attackDamage = 0; charging = false;
                if (!SwordUseNormalLocomotion)
                {
                    SetSwordPoseMask(false);
                    Play(swordMovementClip);
                    players[currentSlot].SetSpeed(SwordMovementSpeed(crouched, moving));
                    ClampSwordClip(players[currentSlot], swordMovementClip, grounded);
                }
                else if (currentClip && players[currentSlot].IsValid() && !currentClip.isLooping)
                {
                    // Fade the final attack pose back to normal locomotion instead of a raised sword idle.
                    players[currentSlot].SetTime(Mathf.Min((float)players[currentSlot].GetTime(),currentClip.length-.01f));
                    players[currentSlot].SetSpeed(0);
                }
            }
            bool showSwordPose = !SwordUseNormalLocomotion || swordSequence.Active;
            swordActionWeight = Mathf.MoveTowards(swordActionWeight,showSwordPose ? 1f : 0f,Time.deltaTime*10f);
            bool movingAttack = !SwordAnimations.OriginalDownloadedPlayback && SwordAnimations.ResponsiveDownloadedCombos && SwordUseNormalLocomotion && moving && !crouched && grounded;
            overlay.SetInputWeight(1,swordActionWeight * (movingAttack ? SwordAnimations.MovingTorsoWeight : 1f));
            if(swordMovingArmsMask) overlay.SetInputWeight(2,movingAttack ? swordActionWeight : 0f);
            fade = Mathf.Min(1, fade + Time.deltaTime / (SwordAnimations.ResponsiveInput ? SwordAnimations.AttackBlendSeconds : .12f));
            if (SwordAnimations.OriginalDownloadedPlayback && swordSequence.Active) fade = 1f;
            clips.SetInputWeight(currentSlot, fade);
            clips.SetInputWeight(1-currentSlot, players[1-currentSlot].IsValid() ? 1-fade : 0);
            SyncSwordArmPlayers();
            UpdateDownloadedGreatCarry();
        }

        void SyncSwordArmPlayers()
        {
            if(!swordArmsClips.IsValid())return;
            for(int slot=0;slot<2;slot++) {
                if(!players[slot].IsValid()){swordArmsClips.SetInputWeight(slot,0);continue;}
                var clip=players[slot].GetAnimationClip();
                if(!swordArmPlayers[slot].IsValid() || swordArmSources[slot]!=clip) {
                    if(swordArmPlayers[slot].IsValid()){swordArmsClips.DisconnectInput(slot);graph.DestroyPlayable(swordArmPlayers[slot]);}
                    swordArmSources[slot]=clip;
                    swordArmPlayers[slot]=AnimationClipPlayable.Create(graph,clip);
                    swordArmPlayers[slot].SetApplyFootIK(false);
                    graph.Connect(swordArmPlayers[slot],0,swordArmsClips,slot);
                }
                // Each graph output has its own clip node, synchronized before evaluation.
                swordArmPlayers[slot].SetTime(players[slot].GetTime());
                swordArmPlayers[slot].SetSpeed(players[slot].GetSpeed());
                swordArmsClips.SetInputWeight(slot,clips.GetInputWeight(slot));
            }
        }

        void BeginSwordAttack(SwordAttackSequence.Command command, bool moving)
        {
            var clip = SwordAnimations.AttackClip(command);
            if (!clip) { CancelAction(); return; }
            if (!moving) FaceCamera();
            float damage = CurrentWeapon == Weapon.Greatsword ? GreatswordDamage : SwordDamage;
            switch (command.Kind)
            {
                case SwordAttackSequence.AttackKind.Held: break;
                case SwordAttackSequence.AttackKind.Running: damage *= 1.2f; break;
                case SwordAttackSequence.AttackKind.Air: damage *= 1.35f; break;
                default: damage *= 1f + command.ComboIndex * .15f; break;
            }
            charging = pending = queued = false;
            BeginAction(clip, damage, false);
            swordHitWindow = -1;
            players[currentSlot].SetSpeed(SwordAnimations.AttackSpeed);
            actionLength = clip.length / Mathf.Max(.1f, SwordAnimations.AttackSpeed);
            actionTime = actionLength;
            swordSequence.SetDuration(actionLength);
            if (SwordAnimations.ResponsiveInput)
            {
                swordActionWeight = 1f;
                overlay.SetInputWeight(1,1f);
            }
        }

        void SetSwordPoseMask(bool upper)
        {
            usingUpperBody = upper;
            // The lower layer supplies the pack's actual crouch/jump legs, rather than idle legs.
            overlay.SetLayerMaskFromAvatarMask(1, fullMask);
            overlay.SetInputWeight(1, 1);
            swordPoseLayers.SetLayerMaskFromAvatarMask(1, upper ? upperMask : fullMask);
            swordPoseLayers.SetInputWeight(0, upper ? 1 : 0);
        }

        void UpdateSwordMovement(bool grounded, bool moving, bool running, bool crouched)
        {
            var clip = !grounded ? SwordAnimations.Jump : crouched
                ? (moving ? SwordAnimations.CrouchWalk : SwordAnimations.CrouchIdle)
                : moving ? (running ? SwordAnimations.Run : SwordAnimations.Walk) : SwordAnimations.Idle;
            if (clip != swordMovementClip || !swordMovementPlayer.IsValid())
            {
                if (swordMovementPlayer.IsValid())
                {
                    swordPoseLayers.DisconnectInput(0);
                    graph.DestroyPlayable(swordMovementPlayer);
                }
                swordMovementClip = clip;
                swordMovementPlayer = AnimationClipPlayable.Create(graph, clip);
                swordMovementPlayer.SetApplyFootIK(false);
                swordMovementPlayer.SetApplyPlayableIK(false);
                graph.Connect(swordMovementPlayer, 0, swordPoseLayers, 0);
            }
            swordMovementPlayer.SetSpeed(SwordMovementSpeed(crouched, moving));
            ClampSwordClip(swordMovementPlayer, clip, grounded);
        }

        float SwordMovementSpeed(bool crouched, bool moving) => crouched && moving && input && input.sprint
            ? SwordAnimations.CrouchRunSpeed : 1f;

        static void ClampSwordClip(AnimationClipPlayable player, AnimationClip clip, bool grounded)
        {
            if (!player.IsValid() || !clip || clip.length <= 0) return;
            if (clip.isLooping && player.GetTime() >= clip.length) player.SetTime(player.GetTime() % clip.length);
            else if (!grounded && player.GetTime() >= clip.length - .02f)
            { player.SetTime(Mathf.Max(0, clip.length - .02f)); player.SetSpeed(0); }
        }
    }
}

