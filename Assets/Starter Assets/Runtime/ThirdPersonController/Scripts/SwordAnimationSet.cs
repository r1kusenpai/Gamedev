using UnityEngine;

namespace StarterAssets
{
    [CreateAssetMenu(menuName = "Weapons/Sword Animation Set")]
    public sealed class SwordAnimationSet : ScriptableObject
    {
        public AnimationClip Idle, Walk, Run, Jump, CrouchIdle, CrouchWalk;
        public AnimationClip Combo1, Combo2, Combo3, Combo4, ChargedAttack, RunningAttack, AirAttack;
        [Header("Downloaded sword timing")]
        public bool ResponsiveInput, UseAuthoredRootTravel;
        public bool OriginalDownloadedPlayback;
        public bool ResponsiveDownloadedCombos;
        public bool FullComboFinisher;
        [Range(.3f,1f)] public float MovingTorsoWeight = .65f;
        public bool IsFullCombo(SwordAttackSequence.Command command) => FullComboFinisher && (command.Kind == SwordAttackSequence.AttackKind.Combo || command.Kind == SwordAttackSequence.AttackKind.Running) && command.ComboIndex == 2;
        public int HitWindowCount(SwordAttackSequence.Command command) => IsFullCombo(command) ? 4 : 1;
        public Vector2 HitWindow(SwordAttackSequence.Command command,int window) => !IsFullCombo(command) ? HitWindow(command) : new[]{new Vector2(.05f,.14f),new Vector2(.20f,.30f),new Vector2(.34f,.44f),new Vector2(.47f,.58f)}[Mathf.Clamp(window,0,3)];
        [Range(.02f,.15f)] public float AttackBlendSeconds = .045f;
        [Range(.1f,.6f)] public float InputBufferSeconds = .4f;
        public Vector4 ComboLink = new Vector4(.88f,.88f,.85f,.75f);
        public Vector4 HitStart = new Vector4(.30f,.36f,.22f,.10f);
        public Vector4 HitEnd = new Vector4(.66f,.70f,.66f,.42f);
        public int ComboCount => Combo4 ? 4 : 3;
        public float LinkAt(SwordAttackSequence.Command command) => IsFullCombo(command) ? ComboLink[2] : command.Kind == SwordAttackSequence.AttackKind.Combo
            ? ComboLink[Mathf.Clamp(command.ComboIndex,0,3)] : .82f;
        public Vector2 HitWindow(SwordAttackSequence.Command command) => command.Kind == SwordAttackSequence.AttackKind.Combo
            ? new Vector2(HitStart[Mathf.Clamp(command.ComboIndex,0,3)], HitEnd[Mathf.Clamp(command.ComboIndex,0,3)])
            : command.Kind == SwordAttackSequence.AttackKind.Running ? new Vector2(.32f,.67f) : new Vector2(.28f,.7f);
        [Range(.1f, 2f)] public float AttackSpeed = 1.25f;
        [Range(1f, 2.5f)] public float CrouchRunSpeed = 1.75f;
        public bool IsReady => Idle && Walk && Run && Jump && CrouchIdle && CrouchWalk
            && Combo1 && Combo2 && Combo3 && ChargedAttack && RunningAttack && AirAttack;
        public AnimationClip AttackClip(SwordAttackSequence.Command command)
        {
            switch (command.Kind)
            {
                case SwordAttackSequence.AttackKind.Held: return ChargedAttack;
                case SwordAttackSequence.AttackKind.Running: return IsFullCombo(command) ? Combo3 : ResponsiveDownloadedCombos && command.ComboIndex == 1 ? Combo2 : RunningAttack;
                case SwordAttackSequence.AttackKind.Air: return AirAttack;
                default: return command.ComboIndex == 0 ? Combo1 : command.ComboIndex == 1 ? Combo2 : command.ComboIndex == 2 || !Combo4 ? Combo3 : Combo4;
            }
        }
    }
}
