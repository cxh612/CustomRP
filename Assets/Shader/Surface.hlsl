#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
    float3 position;
    float3 normal;
    float3 interpolatedNormal;//原始法线，用于阴影偏移，世界空间
    float3 viewDirection;
    float3 color;
    float alpha;
    float metallic;
    float smoothness;
    float2 lightMapUV;
    float fresnelStrength;
    float depth;
    float dither;//级联阴影混合模式
};

#endif