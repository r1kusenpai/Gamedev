using UnityEngine;
namespace WildernessCombat {
[CreateAssetMenu(menuName="Combat/Wilderness enemy settings")]
public sealed class WildernessEnemySettings:ScriptableObject {
 public GameObject Male,Female;public AnimationClip Hover,Attack,Hit,Knockback;public Material MagicMaterial,SwordMaterial;public GameObject SwordModel;
 public GameObject Cyclops,CyclopsClub;public AnimationClip CyclopsIdle,CyclopsWalk,CyclopsSwing,CyclopsScratch,CyclopsCombo;
 [Min(1f)]public float CyclopsHeight=3f;[Min(.2f)]public float CyclopsClubLength=1.65f;
 public Vector3[] SpawnPoints;public Bounds[] CitySafeZones;
 public float MoveSpeed=9f,Health=130f,AttackDamage=14f,AggroDistance=24f,LeashDistance=42f,RespawnSeconds=50f;
 public bool Ready=>Male&&Female&&Hover&&Attack&&Hit&&Knockback&&SpawnPoints!=null&&SpawnPoints.Length>0;
}}
