Shader "Unlit/MVP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 vertexPS : TEXCOORD2;
            };

            sampler2D _MainTex;
            //float4 _MainTex_ST;
            float4x4 MVP;
            float4x4 VP;
            int IsGame;
            v2f vert (appdata v)
            {
                v2f o;
                float4x4 vp = mul(unity_MatrixVP, unity_ObjectToWorld);
                float4x4 vp2 = mul(VP, unity_ObjectToWorld);
                // if(IsGame==1)
                // {
                //     o.vertex = mul(vp2,v.vertex);// UnityObjectToClipPos(v.vertex);
                // }else
                // {
                //      o.vertex = mul(vp,v.vertex);// UnityObjectToClipPos(v.vertex);
                // }
                o.vertex = mul(vp,v.vertex);
             // o.vertex = mul(vp,v.vertex);
               // o.vertex = mul(MVP,v.vertex);
                o.uv = v.uv;//, _MainTex);
                o.vertexPS = mul(vp,float4(0,0,0,1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                
                //float2 dither = (i.vertex.xy % 10) / 10;
                //clip(0.5-dither);
                //float b = i.vertexPS.z/i.vertexPS.w;
                float b =  i.vertex.z;///i.vertex.w;
                 // if(b<=0.1)
                 // {
                 //     return fixed4(0,1,0,1);
                 // }else if(b<1)
                 // {
                 //     return fixed4(1,0,0,1);
                 //     
                 // }
                 return b;
            }
            ENDCG
        }
    }
}
