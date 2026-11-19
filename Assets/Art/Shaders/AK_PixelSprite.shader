// Unlit pixel-art sprite shader (original). Plain texture x tint, NO emission, NO lighting, point-sampled by the importer.
// _MaxLuma caps the output brightness just under the world's bloom threshold (AK_WorldLook: 0.86), so white hair / highlights
// never bloom into a glow haze round the sprite; colours below the cap are shown exactly as painted.
Shader "AdventureKing/PixelSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaxLuma ("Max brightness (under the bloom threshold)", Range(0.5,1)) = 0.84
        // SpriteMask support (SpriteRenderer.maskInteraction drives these): used to hide the hero's own hat under a worn hat
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Pass
        {
            Name "PixelSprite"
            Tags { "LightMode"="UniversalForward" }
            Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _MaxLuma;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.col = i.col; return o; }
            half4 frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.col * _Color;
                half peak = max(t.r, max(t.g, t.b));
                t.rgb *= peak > _MaxLuma ? _MaxLuma / peak : 1.0h;   // cap, hue preserved
                return t;
            }
            ENDHLSL
        }
    }
}