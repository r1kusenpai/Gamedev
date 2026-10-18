Shader "Castle/Mixed UNS Garden Ground"
{
 Properties
 {
  _DirtTex("Original UNS dirt",2D)="white"{}
  _DirtScale("Dirt tile size",Vector)=(10,10,0,0)

  _StoneTex("Original medieval wall stone",2D)="white"{}
  _StoneNormal("Original wall surface",2D)="bump"{}
  _StoneScale("Stone metres per tile",Vector)=(4,2.887,0,0)
  _BaseMap("Unused compatibility map",2D)="white"{}
  _BaseColor("Stone",Color)=(0.58,0.59,0.57,1)
  _MortarColor("Mortar",Color)=(0.29,0.30,0.29,1)
  _RoofColor("Slate roof",Color)=(0.15,0.18,0.22,1)
  _BlockWidth("Block width metres",Float)=0.75
  _BlockHeight("Block height metres",Float)=0.32
  _Joint("Joint width metres",Float)=0.018
  _RoofStartY("Roof starts above height",Float)=0
  _AutoRoof("Roof on sloped upper surfaces",Float)=1
  _ForceRoof("Entire section is roof",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForward"} Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_MortarColor,_RoofColor;
   float4 _StoneScale,_DirtScale;
   float _BlockWidth,_BlockHeight,_Joint,_RoofStartY,_AutoRoof,_ForceRoof;
   CBUFFER_END
   TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
   TEXTURE2D(_StoneTex); SAMPLER(sampler_StoneTex);
   TEXTURE2D(_StoneNormal); SAMPLER(sampler_StoneNormal);
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float fog:TEXCOORD2;float brown:TEXCOORD3;};
   Varyings vert(Attributes i){Varyings o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);o.brown=i.color.r;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float3 brick(float2 p,float roof)
   {
    if(roof<.5) return SAMPLE_TEXTURE2D(_StoneTex,sampler_StoneTex,p/_StoneScale.xy).rgb*_BaseColor.rgb;
    float width=lerp(max(.1,_BlockWidth),.38,roof),height=lerp(max(.1,_BlockHeight),.24,roof);
    float row=floor(p.y/height);p.x+=fmod(abs(row),2)*width*.5;
    float2 cell=floor(p/float2(width,height));float2 q=frac(p/float2(width,height));
    float2 edge=min(q,1-q)*float2(width,height);float dist=min(edge.x,edge.y);
    float aa=max(fwidth(dist),.002);float joint=smoothstep(_Joint-aa,_Joint+aa,dist);
    float3 color=lerp(_BaseColor.rgb,_RoofColor.rgb,roof)*(0.94+hash(cell)*.12);
    return lerp(lerp(_MortarColor.rgb,_RoofColor.rgb*.55,roof),color,joint);
   }
   half4 frag(Varyings i):SV_Target
   {
    float3 n=normalize(i.normalWS);float3 w=pow(abs(n),6);w/=max(w.x+w.y+w.z,.001);
    float slope=smoothstep(.18,.3,abs(n.y))*(1-smoothstep(.82,.94,abs(n.y)));
    float roof=max(_ForceRoof,_AutoRoof*slope*step(_RoofStartY,i.positionWS.y));
    float3 color=brick(i.positionWS.zy,roof)*w.x+brick(i.positionWS.xz,roof)*w.y+brick(i.positionWS.xy,roof)*w.z;
    if(roof<.5)
    {
     color=lerp(SAMPLE_TEXTURE2D(_StoneTex,sampler_StoneTex,i.positionWS.xz/_StoneScale.xy).rgb,SAMPLE_TEXTURE2D(_DirtTex,sampler_DirtTex,i.positionWS.xz/_DirtScale.xy).rgb,saturate(i.brown));
     float3 tx=UnpackNormal(SAMPLE_TEXTURE2D(_StoneNormal,sampler_StoneNormal,i.positionWS.zy/_StoneScale.xy));
     float3 ty=UnpackNormal(SAMPLE_TEXTURE2D(_StoneNormal,sampler_StoneNormal,i.positionWS.xz/_StoneScale.xy));
     float3 tz=UnpackNormal(SAMPLE_TEXTURE2D(_StoneNormal,sampler_StoneNormal,i.positionWS.xy/_StoneScale.xy));
     float3 stoneN=float3(tx.z*sign(n.x),tx.y,tx.x)*w.x+float3(ty.x,ty.z*sign(n.y),ty.y)*w.y+float3(tz.x,tz.y,tz.z*sign(n.z))*w.z;
     n=normalize(lerp(n,stoneN,.65));
    }
    Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));float3 lighting=max(SampleSH(n),.08)+main.color*saturate(dot(n,main.direction))*main.shadowAttenuation*main.distanceAttenuation;
    #ifdef _ADDITIONAL_LIGHTS
    uint count=GetAdditionalLightsCount();for(uint j=0;j<count;j++){Light light=GetAdditionalLight(j,i.positionWS);lighting+=light.color*saturate(dot(n,light.direction))*light.distanceAttenuation*light.shadowAttenuation;}
    #endif
    return half4(MixFog(color*lighting,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ColorMask 0 Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex shadowVert
   #pragma fragment shadowFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
   float3 _LightDirection,_LightPosition;
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   float4 shadowVert(A i):SV_POSITION
   {
    float3 p=TransformObjectToWorld(i.positionOS.xyz),n=TransformObjectToWorldNormal(i.normalOS);
    float3 dir=_LightDirection;
    #ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
    dir=normalize(_LightPosition-p);
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,dir));
    #if UNITY_REVERSED_Z
    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #else
    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #endif
    return clip;
   }
   half4 shadowFrag():SV_Target{return 0;}
   ENDHLSL
  }
 }
}


