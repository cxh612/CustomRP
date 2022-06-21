#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Lighting.hlsl"
TEXTURE2D(_EmissionMap);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
    UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
    UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float3 DecodeNormal (float4 sample, float scale) {
    #if defined(UNITY_NO_DXT5nm)
    return UnpackNormalRGB(sample, scale);
    #else
    return UnpackNormalmapRGorAG(sample, scale);
    #endif
}

//获取切空间法线
float3 GetNormalTS (float2 baseUV) {
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, baseUV);
    float scale = _NormalScale;
    float3 normal = DecodeNormal(map, scale);
    return normal;
}

struct Attributes {
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 baseUV : TEXCOORD0;
	float2 lightMapUV:TEXCOORD1;
    float4 tangentOS : TANGENT;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings {
    float4 positionCS : SV_POSITION;
    float3 normalWS : VAR_NORMAL;
    float3 positionWS : TEXCOORD1;
    float2 baseUV : VAR_BASE_UV;
	float2 lightMapUV:TEXCOORD2;// TEXCOORD2;
    float4 tangentWS : VAR_TANGENT;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

 //sampler2D _MainTex;
Varyings LitPassVertex (Attributes input)
{
    Varyings output = (Varyings)0;
    output.baseUV = input.baseUV;
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.lightMapUV = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    return output;
}

void ClipLOD (float2 positionCS, float fade) {
    #if defined(LOD_FADE_CROSSFADE)
    float2 dither = (positionCS.xy % 10) / 10;                  //网格消失
    //float2 dither = InterleavedGradientNoise(positionCS.xy, 0);//TODO:随机噪声消失
    clip(fade + (fade < 0.0 ? dither : -dither));
    #endif
}

float GetFresnel () {
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Fresnel);
}

float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
    float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);//构建切空间变换矩阵
    return mul(normalTS, tangentToWorld);//TransformTangentToWorld(normalTS, tangentToWorld);
}

//世界空间
Surface GetSurface(Varyings input)
{
    Surface surface;
    float4 BaseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    surface.color = BaseTex.rgb*_Color.rgb;
    surface.alpha = BaseTex.a*_Color.a;
    surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
    surface.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.position = input.positionWS;
    surface.lightMapUV = input.lightMapUV;
    surface.fresnelStrength = GetFresnel();
    surface.interpolatedNormal = normalize(input.normalWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.dither = InterleavedGradientNoise(input.positionCS.xy,0);
    #ifdef _NORMAL  //采用法线贴图
    surface.normal = NormalTangentToWorld(GetNormalTS(input.baseUV), input.normalWS, input.tangentWS);
    #else
    surface.normal = surface.interpolatedNormal;
    #endif
    return surface;
}

float3 GetEmission (float2 baseUV) {
    float4 emissionColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
    return emissionColor.rgb * color.rgb;
}

float4 LitPassFragment (Varyings input) : SV_TARGET {
    #if defined(LOD_FADE_CROSSFADE)
        ClipLOD(input.positionCS.xy, unity_LODFade.x);
    #endif
    Surface surface = GetSurface(input);
    half3 color = GetLighting(surface);//gi and brdf 用于LightReflection 用于环境光反射计算
    color += GetEmission(input.baseUV);        //自发光
    return float4(color, surface.alpha);
}
#endif