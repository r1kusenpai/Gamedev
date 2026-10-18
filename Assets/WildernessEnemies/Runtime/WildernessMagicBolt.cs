using UnityEngine;
namespace WildernessCombat {
public sealed class WildernessMagicBolt:MonoBehaviour {
 WildernessEnemyWorld world;WildernessEnemy owner;Vector3 direction;float damage,age;
 public static void Fire(WildernessEnemyWorld world,WildernessEnemy owner,Vector3 origin,Vector3 target,float damage,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Wilderness magic bolt";Destroy(go.GetComponent<Collider>());go.transform.position=origin;go.transform.localScale=Vector3.one*.24f;go.GetComponent<Renderer>().sharedMaterial=material;var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=material;trail.time=.13f;trail.startWidth=.13f;trail.endWidth=0;var bolt=go.AddComponent<WildernessMagicBolt>();bolt.world=world;bolt.owner=owner;bolt.damage=damage;bolt.direction=(target-origin).normalized;Destroy(go,4);}
 void Update(){if(!world||!owner||world.PlayerSafe){Destroy(gameObject);return;}age+=Time.deltaTime;Vector3 delta=direction*(16*Time.deltaTime);var hits=Physics.SphereCastAll(transform.position,.14f,direction,delta.magnitude,~0,QueryTriggerInteraction.Ignore);System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));foreach(var hit in hits){if(hit.collider.GetComponentInParent<WildernessEnemy>()||hit.transform.IsChildOf(transform))continue;if(hit.transform.IsChildOf(world.Player))world.HurtPlayer(damage,transform.position-direction);Destroy(gameObject);return;}transform.position+=delta;}
}}

