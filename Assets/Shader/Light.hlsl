#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED
#define LIGHT_MAX_COUNT  4
#include "Surface.hlsl"
#include "Shadow.hlsl"
CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float3 _DirectionalLightColors[LIGHT_MAX_COUNT];
    float3 _DirectionalLightDirections[LIGHT_MAX_COUNT];
    float4 _DirectionalLightShadowData[LIGHT_MAX_COUNT];
    
CBUFFER_END

///颜色,方向，序号，衰减
struct Light {
    float3 color;
    float3 direction;
    int index;
    float attenuation;
};
//获取_DirectionalLightShadowData 数据 , strength , tileIndex,normalBias,shadowMaskChannel
DirectionalShadowData GetDirectionalShadowData (int lightIndex) {
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
    return data;
}

///
///获取Directional阴影衰减
///
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, CustomShadowData shadowData ,int lightIndex,Surface surface)
{
    #if defined(SHADOWS_SHADOWMASK)
        if (directional.strength * shadowData.strength <= 0.0) {//shadowData.strength cull了的为负值， 直接去Baked阴影（ShadowMask）
            return lerp(1.0, GetBakedShadow(shadowData.shadowMask,directional.shadowMaskChannel), abs(directional.strength));
        }
    #endif
    float shadow = GetRuntimeShadow(surface,shadowData,lightIndex,directional);//实时阴影
    return MixBakedAndRealtimeShadows(shadowData, shadow, directional.strength,directional.shadowMaskChannel);//实时阴影与混合
}

//获取直射灯光
Light GetDirectionalLight (int index, Surface surface, CustomShadowData shadowData) {
    Light light;
    light.color = _DirectionalLightColors[index];
    light.direction = _DirectionalLightDirections[index];
    light.index = index;

    DirectionalShadowData dirShadowData = GetDirectionalShadowData(index);
    dirShadowData.tileIndex = dirShadowData.tileIndex + shadowData.cascadeIndex;//需要根据cascadeIndex进行偏移
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData,shadowData,index, surface);
    //light.attenuation = shadowData.cascadeIndex * 0.25;//TODO:测试
    return light;
}
#endif
