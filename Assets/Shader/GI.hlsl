#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#include "BRDF.hlsl"


struct GI {
	float3 diffuse;
	float3 specular;
	//ShadowMask shadowMask;
};

CBUFFER_START(_CustomGI)
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
CBUFFER_END

TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

//
//探针数据和LightMap采样
//
float3 SampleLightProbeOrLightMap (float3 postionWS ,float3 normalWS,float2 lightMapUV) {
	#if defined(LIGHTMAP_ON)
		//静态物体采样
		#if defined(UNITY_LIGHTMAP_FULL_HDR)
		bool encode =false;
		#else
		bool encode =true;
		#endif
		return SampleSingleLightmap(
			TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightMapUV,
			float4(1.0, 1.0, 0.0, 0.0),
			encode,
			float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0));
	#else
		//动态物体SH
		if (unity_ProbeVolumeParams.x) {//是否使用LPPV(LightProbeProxyVolume) 或插值光探头通过
			return SampleProbeVolumeSH4(
				TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
				postionWS, normalWS,
				unity_ProbeVolumeWorldToObject,
				unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
				unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
			);
		}
		else {
			float4 coefficients[7];
			coefficients[0] = unity_SHAr;
			coefficients[1] = unity_SHAg;
			coefficients[2] = unity_SHAb;
			coefficients[3] = unity_SHBr;
			coefficients[4] = unity_SHBg;
			coefficients[5] = unity_SHBb;
			coefficients[6] = unity_SHC;
			return  max(0.0, SampleSH9(coefficients,  normalWS));
		}
	#endif
}

//环境高光采样
//unity_SpecCube0
float3 SampleSpecular (Surface surface,float roughness) {
	float3 uvw = reflect(-surface.viewDirection, surface.normal);							//反射方向
	float mip = PerceptualRoughnessToMipmapLevel(roughness);					             //随着粗糙度的增加，mip会随之增加，反射变得模糊。 感知粗糙度->MipmapLevel
	float4 color = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mip);//采样
	return DecodeHDREnvironment(color, unity_SpecCube0_HDR);
}

///roughness 用于采样环境光反射的miplevel获取（PerceptualRoughnessToMipmapLevel）
GI GetGI (Surface surface ,float roughness){
	GI gi= (GI)0;
	gi.diffuse = SampleLightProbeOrLightMap(surface.position,surface.normal,surface.lightMapUV);// SampleLightMap(surface.lightMapUV)+SampleLightProbe(surface.position,surface.normal);
	gi.specular = SampleSpecular(surface,roughness);   //环境光反射
	return gi;
}
#endif