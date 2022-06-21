Shader "Unlit/CustomLit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _Color("Color",Color) = (1,1,1,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR]_EmissionColor("Emission Color",Color) = (0,0,0,0)
        
        _Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Fresnel ("Fresnel", Range(0, 1)) = 1
        [NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
		_NormalScale("Normal Scale", Range(0, 1)) = 1
        
        _DetailMap("Details", 2D) = "linearGrey" {}
        [NoScaleOffset] _DetailNormalMap("Detail Normals", 2D) = "bump" {}
        _DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"}
        Pass
        {
            Tags{ "RenderType"="Opaque" "LightMode"="CustomLit"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _  DEBUG_CASCADE
            #pragma shader_feature _ SHADOW_OFF
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ _SHADOW_MASK_DISTANCE _SHADOW_MASK_ALWAYS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _NORMAL
			#pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
			#include "LitPass.hlsl"
            
            #pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
            ENDHLSL
        }
                Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    	 // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags{"LightMode" = "Meta"}

            Cull Off

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMeta

            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            #pragma shader_feature_local_fragment _SPECGLOSSMAP

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            ENDHLSL
        }
    }
}
