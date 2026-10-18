using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace QueenBattle
{
    public sealed class QueenSpectralAssistant:MonoBehaviour,StarterAssets.ICombatDamageReceiver
    {
        QueenBossBattle battle;
        Renderer[] skins;
        readonly List<Material> materials=new List<Material>();
        float age;
        float health=30;
        CapsuleCollider hurt;
        public bool IsDamageable=>battle&&battle.Fighting&&health>0;
        public Transform DamageRoot=>transform;
        public void ReceiveDamage(float damage,Transform attacker)
        {if(!IsDamageable||damage<=0)return;health-=damage;if(health<=0){hurt.enabled=false;Destroy(gameObject);}}
        public void Initialize(QueenBossBattle owner,GameObject model)
        {
            battle=owner;skins=model.GetComponentsInChildren<Renderer>();
            hurt=gameObject.AddComponent<CapsuleCollider>();hurt.radius=.38f;hurt.height=1.9f;
            foreach(var skin in skins)
            {
                var slots=skin.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var m=new Material(owner.TelegraphMaterial);m.name="Spectral queen glow";
                    m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;
                    slots[i]=m;materials.Add(m);
                }
                skin.sharedMaterials=slots;skin.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        void LateUpdate()
        {
            if(!battle||!battle.Fighting){Destroy(gameObject);return;}
            age+=Time.deltaTime;
            float alpha=.4f*Mathf.Min(Mathf.Clamp01(age/.3f),Mathf.Clamp01((6-age)/.7f));
            foreach(var m in materials)m.color=new Color(.25f,.7f,1,alpha);
            if(skins.Length==0)return;
            var bounds=skins[0].bounds;foreach(var skin in skins)bounds.Encapsulate(skin.bounds);
            float lift=battle.SurfaceHeightAt(bounds.center)+.03f-bounds.min.y;if(lift>0)transform.position+=Vector3.up*lift;
            hurt.center=transform.InverseTransformPoint(bounds.center+Vector3.up*Mathf.Max(0,lift));hurt.height=Mathf.Clamp(bounds.size.y,1,2.6f);
        }
        void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);}
    }
}
