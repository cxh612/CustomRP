#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED
#include "Surface.hlsl"
#include "BRDF.hlsl"
#include "Light.hlsl"
//平方
float Square (float v) {
    return v * v;
}
//环境光(diffuse,specular,以及菲尼尔相关计算)
float3 EnvironmentLight (Surface surface, BRDF brdf,GI gi) {
    float3 diffuse = gi.diffuse;
    float3 specular = gi.specular;
    float fresnelStrength = surface.fresnelStrength * Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
    float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    return reflection+diffuse * brdf.diffuse ;
}

//高光强度计算函数
float SpecularStrength (Surface surface, BRDF brdf, Light light) {
    float3 halfDir = SafeNormalize(light.direction + surface.viewDirection); 
    float nh2 = Square(saturate(dot(surface.normal, halfDir)));
    float lh2 = Square(saturate(dot(light.direction, halfDir)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2) * normalization);
}

//漫反射+高光强度*高光颜色
//直接BRDF计算
float3 DirectBRDF (Surface surface, BRDF brdf, Light light) {
    float specularStrength = SpecularStrength(surface, brdf, light);//计算高光强度
    return specularStrength * brdf.specular + brdf.diffuse;
}

//Phong模型漫反射计算
float3 PhongLight (Surface surface,Light light) {
    return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

//计算单个平行光
float3 GetLighting (Surface surface, BRDF brdf, Light light) {
    float3 phongLight = PhongLight(surface, light) ;      // Phong模型漫反射计算
    float3 directBDRF = DirectBRDF(surface, brdf, light); //漫反射+高光强度*高光颜色
    return phongLight * directBDRF;
}

//计算所有平行光
float3 GetLighting (Surface surface) {
    BRDF brdf = GetBRDF(surface);
    GI gi = GetGI(surface,brdf.perceptualRoughness);
    float3 color = EnvironmentLight(surface, brdf, gi);         //环境光以及环境光反射计算
    int lightCount = _DirectionalLightCount;
    CustomShadowData shadowData = GetShadowData(surface);
    //计算所有灯光
    for (int i = 0; i < lightCount; i++) {
        Light light = GetDirectionalLight(i,surface,shadowData);//获取灯光数据
        color +=  GetLighting(surface,brdf, light );             //光照计算
    }
    return  color;
}
#endif