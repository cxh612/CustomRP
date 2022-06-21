#ifndef CUSTOM_SHADOW_INCLUDED
#define CUSTOM_SHADOW_INCLUDED
#define CASCADE_MAX_COUNT 4                     //最大级联数量
#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4 //阴影最大灯光数量
#include "GI.hlsl"
#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
#define SHADOWS_SHADOWMASK
#endif
#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
SAMPLER_CMP(sampler_linear_clamp_compare);

struct ShadowMask {
    bool always;
    bool distance;//是否启用距离
    float4 shadows;
};

struct CustomShadowData {
    int cascadeIndex;
    float cascadeBlend;
    float strength;
    ShadowMask shadowMask;
};

struct DirectionalShadowData {
    float strength;
    int tileIndex;
    float normalBias;
    int shadowMaskChannel;
};

CBUFFER_START(_CustomShadow)
    int _CascadeCount;
    float4 _CascadeCullingSpheres[CASCADE_MAX_COUNT];
    float4 _CascadeDatas[CASCADE_MAX_COUNT];
    float4 _ShadowDistanceFade;
    float _ShadowDistance;
    float4x4 _ShadowMats[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT*CASCADE_MAX_COUNT];
    float4 _ShadowAtlasSize;
CBUFFER_END

float DistanceSquared(float3 pA, float3 pB) { 
    return dot(pA - pB, pA - pB);
}

float SampleDirectionalShadowAtlas (float3 positionSTS) {
    return SAMPLE_TEXTURE2D_SHADOW(
        _DirectionalShadowAtlas, sampler_linear_clamp_compare, positionSTS
    );
}

float FadedShadowStrength (float distance, float scale, float fade) {
    return saturate((1.0 - distance * scale) * fade);
}

///ShadowMask
float4 SampleShadowMask (float2 lightMapUV,float3 positionWS) {
    #if defined(LIGHTMAP_ON)//静态物体采样
    return  SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
    #else					//LPPV:动态物体shadowMask 光探针代理体积也可以与阴影遮罩一起使用
    if (unity_ProbeVolumeParams.x) {
        return SampleProbeOcclusion(
            TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
            positionWS, unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
        );
    }
    else {
        return unity_ProbesOcclusion;
    }
    #endif
}

ShadowMask GetShadowMask(Surface surface)
{
    ShadowMask shadowMask =(ShadowMask)0;
    shadowMask.distance = false;
    shadowMask.always = false;
    shadowMask.shadows = 1.0;
    //采用ShaderMask
    #if defined(SHADOWS_SHADOWMASK)
    shadowMask.shadows =  SampleShadowMask(surface.lightMapUV, surface.position);//Shader Mask 采样
    #if defined(_SHADOW_MASK_ALWAYS)
    shadowMask.always = true;
    #elif defined(_SHADOW_MASK_DISTANCE)
    shadowMask.distance = true;
    #endif
    #endif
    return shadowMask;
}

////
//获取阴影数据，每个灯光具有相同的CustomShadowData
////
CustomShadowData GetShadowData (Surface surface) {
    CustomShadowData data=(CustomShadowData)0;
    //1.0f/settings.MaxDistance, 1.0f /_ShadowDistanceFade,1f / (1f - f * f)
    data.strength = FadedShadowStrength(surface.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    data.shadowMask = GetShadowMask(surface);
    data.cascadeBlend = 1.0;
    int i=0;
    //根据距离查询当前级联id
    for(;i<_CascadeCount;i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];//sphere
        float distanceSqr = DistanceSquared(surface.position, sphere.xyz);
        if (distanceSqr < sphere.w) {
            float fade = FadedShadowStrength(distanceSqr, _CascadeDatas[i].x, _ShadowDistanceFade.z);
            if (i == _CascadeCount - 1) {
	            data.strength *=fade;
            }else {
	            data.cascadeBlend = fade;
            }
            break;
        }
    }
    
    if (i == _CascadeCount) {
        data.strength = 0.0;
    }
    #if defined(_CASCADE_BLEND_DITHER)
        else if (data.cascadeBlend < surface.dither) {
            i += 1;
        }
    #endif
    #if !defined(_CASCADE_BLEND_SOFT)
        data.cascadeBlend = 1.0;
    #endif
    data.cascadeIndex = i;
    return data;
}
            
float FilterDirectionalShadow (float3 positionSTS) {
    #if defined(DIRECTIONAL_FILTER_SETUP)////软阴影
        float shadow = 0;
		float weights[DIRECTIONAL_FILTER_SAMPLES];
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.yyxx;
        DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
           shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
        }
      return shadow; 
    #else
        return  SampleDirectionalShadowAtlas(positionSTS);
   #endif
}
//获取实时阴影
float GetRuntimeShadow (Surface surface, CustomShadowData shadowData,int lightIndex,DirectionalShadowData directional)
{
    float3 worldPos = surface.position;
    float texelSize = _CascadeDatas[shadowData.cascadeIndex].y;//texelSize * 1.4142136f;
    float3 normal = surface.interpolatedNormal;
    float3 normalBias = normal * texelSize;// * directional.normalBias;//根据法线偏移
    float4x4 mat = _ShadowMats[lightIndex * _CascadeCount + shadowData.cascadeIndex];
    float4 shadwPos = mul(mat,float4(worldPos + normalBias,1.0));
    float shadow = FilterDirectionalShadow(shadwPos);//采样
   
    if (shadowData.cascadeBlend < 1.0) {//每级联之间平滑过度
        texelSize =  _CascadeDatas[shadowData.cascadeIndex + 1].y;
        normalBias = surface.normal * (directional.normalBias * texelSize);
        mat = _ShadowMats[directional.tileIndex + 1];
        shadwPos.xyz = mul(mat,float4(worldPos + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(shadwPos), shadow, shadowData.cascadeBlend);
    }
    return shadow;
}

float GetBakedShadow (ShadowMask mask,int shadowMaskChannel) {
	float shadow = 1.0;
	if (mask.distance || mask.always) {
		shadow = mask.shadows[shadowMaskChannel];// 物体Mask
	}
	return shadow;
}

///
///实时阴影和shaderMask混合.
///
float MixBakedAndRealtimeShadows (CustomShadowData shadowData, float runtimeShadow, float lightShadowStrength,int shadowMaskChannel) {
    #if defined(SHADOWS_SHADOWMASK)
        float baked = GetBakedShadow(shadowData.shadowMask,shadowMaskChannel);
        if (shadowData.shadowMask.always) {
            runtimeShadow = lerp(1.0, runtimeShadow, shadowData.strength);
            runtimeShadow = min(baked, runtimeShadow);
        }
        if (shadowData.shadowMask.distance) {
	        runtimeShadow = lerp(baked, runtimeShadow, shadowData.strength);
	    }
        return  lerp(1.0, runtimeShadow, lightShadowStrength);
    #endif
	return lerp(1.0, runtimeShadow, lightShadowStrength * shadowData.strength);
}

#endif