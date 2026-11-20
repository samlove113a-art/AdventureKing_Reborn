// Soft contact shadow under painted scenery (multiplies the ground, no lighting cost).
Shader "AdventureKing/BlobShadow"
{
    Properties
    {
        [MainTexture] _BaseMap ("Mask", 2D) = "white" {}
        _Strength ("Strength", Range(0,1)) = 0.45
        _ShadowColor ("Shadow Color", Color) = (0.18,0.2,0.32,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-50" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Strength;
                half4 _ShadowColor;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                half m = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _Strength;
                return half4(lerp(half3(1,1,1), _ShadowColor.rgb, m), 1);
            }
            ENDHLSL
        }
    }
}
