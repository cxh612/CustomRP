#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED
#include "Surface.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#define MIN_REFLECTIVITY 0.04
struct BRDF {
    float3 diffuse;
    float3 specular;
    float roughness;
    float perceptualRoughness;//感知粗糙度
    float fresnel;
};
//
//定义为最小反射率并添加一个OneMinusReflectivity将范围从 0-1 调整到 0-0.96 的函数。与URP 的方法类似。
//Desc:float oneMinusReflectivity = 1.0 - metallic
//限制在 0-0.96
//
float OneMinusReflectivity (float metallic) {
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

///理论：
///粗糙度与平滑度相反，所以我们可以简单地取一减去平滑度。
///这Core RP Library具有执行此操作的函数，名为PerceptualSmoothnessToPerceptualRoughness.
///我们将使用此函数来明确将平滑度以及粗糙度定义为可感知的。
///PerceptualRoughnessToRoughness我们可以通过将感知值平方的函数转换为实际粗糙度值。
///这与迪士尼照明模型采用类似处理方式。这样做是因为在编辑素材时调整感知版本更直观。
///
float GetRoughness(float smoothness)
{
    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);//(1.0 - surface.smoothness);
    return  PerceptualRoughnessToRoughness(perceptualRoughness);                     //perceptualRoughness * perceptualRoughness;
}

BRDF GetBRDF (Surface surface) {
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    BRDF brdf=(BRDF)0;
    brdf.diffuse = surface.color * oneMinusReflectivity;
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
    brdf.perceptualRoughness = GetRoughness(surface.smoothness);             //smoothness to perceptualRoughness(感知粗糙度)
    brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
    return brdf;
}
#endif