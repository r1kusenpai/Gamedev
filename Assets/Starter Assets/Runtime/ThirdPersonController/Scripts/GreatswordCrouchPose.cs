using UnityEngine;
using UnityEngine.Playables;

namespace StarterAssets
{
    public partial class WeaponController
    {
        bool greatCrouchActionFrame;
        public bool UsesNormalWeaponRunningBody { get; private set; }
        public bool PreservesGreatswordCrouchArms => !UsesCopiedSwordAttacks && CurrentWeapon==Weapon.Greatsword
            && posture && posture.CurrentStance==CQCPosture.Stance.Crouching && usingUpperBody
            && animationSource && !animationSource.IsRolling && !animationSource.IsTransitioning;
        public bool PreservesGreatswordCrouchTorso => PreservesGreatswordCrouchArms && greatCrouchActionFrame;

        bool ConfigureWeaponPoseMask(bool crouched)
        {
            bool greatGrip=crouched && CurrentWeapon==Weapon.Greatsword && actionTime<=0;
            greatCrouchActionFrame=crouched && CurrentWeapon==Weapon.Greatsword && actionTime>0;
            overlay.SetInputWeight(1,crouched && actionTime<=0 && !charging && !greatGrip ? 0:1);
            bool bowRecoil=CurrentWeapon==Weapon.Bow && actionTime>0 && currentClip && currentClip.name.Contains("recoil");
            bool upper=greatCrouchActionFrame || (crouched || input&&input.move.sqrMagnitude>.01f)
                && movement.Grounded && !dodging && (charging || actionTime>0&&attackDamage>0 || bowRecoil)
                && currentClip && !currentClip.name.Contains("slide") && !currentClip.name.Contains("jump");
            UsesNormalWeaponRunningBody=(CurrentWeapon==Weapon.Greatsword || CurrentWeapon==Weapon.Bow)
                && !crouched && movement && movement.Grounded && input && input.move.sqrMagnitude>.01f
                && (actionTime<=0 || attackDamage<=0 && !bowRecoil) && !charging;
            usingUpperBody=upper || greatGrip || bowRecoil || UsesNormalWeaponRunningBody;
            overlay.SetLayerMaskFromAvatarMask(1,greatGrip || UsesNormalWeaponRunningBody ? greatGripMask : upper||bowRecoil ? upperMask : fullMask);
            return greatGrip;
        }
    }
}

