// Painted 2.5D scenery layer (houses, trees, signs drawn as cut-outs at depth behind the gameplay plane).
// Unlit (the paintings already carry their lighting), alpha-to-coverage edges with MSAA, URP fog for
// atmospheric depth, optional haze tint toward the sky colour for far layers, casts no shadow.
Shader "AdventureKing/Cutout"
{
    Properties
    {
        [MainTexture] _BaseMap ("Painting", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _HazeColor ("Haze Color", Color) = (0.78,0.88,0.96,1)
        _Haze ("Haze Amount", Range(0,1)) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.35
        _FogAmount ("Fog Amount", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "Cutout"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            AlphaToMask On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _HazeColor;
                half _Haze;
                half _Cutoff;
                half _FogAmount;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                // Sharpen the alpha edge for alpha-to-coverage (smooth, no halo).
                c.a = (c.a - _Cutoff) / max(fwidth(c.a), 1e-4) + 0.5;
                clip(c.a);
                c.rgb = lerp(c.rgb, _HazeColor.rgb, _Haze);
                c.rgb = lerp(c.rgb, MixFog(c.rgb, i.fog), _FogAmount); // the far backdrop opts out
                return half4(c.rgb, saturate(c.a));
            }
            ENDHLSL
        }
    }
}
