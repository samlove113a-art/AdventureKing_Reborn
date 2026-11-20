// Layered skill-VFX shader for Adventure King (original). One material per blend mode; per-effect values come from a
// MaterialPropertyBlock (SkillVfxKit):
//   shape mask (_MainTex, R*A) x scrolling noise (_NoiseTex) -> intensity; core/edge HDR gradient by intensity;
//   _Dissolve eats the shape through the noise with a glowing rim (impact -> dissipate); _UVScroll slides the shape
//   (trails / ribbons); vertex colour (particles) multiplies everything. Bloom in URP post makes the core glow.
Shader "AdventureKing/VFXFlow"
{
    Properties
    {
        _MainTex ("Shape Mask", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        [HDR] _ColorCore ("Core", Color) = (2,2,2,1)
        [HDR] _ColorEdge ("Edge", Color) = (0.5,0.8,1,1)
        _CoreSharp ("Core Sharpness", Range(0.2,6)) = 2
        _NoiseScale ("Noise Scale (xy) Scroll (zw)", Vector) = (1,1,0.6,0)
        _NoiseAmount ("Noise Amount", Range(0,1)) = 0.5
        _UVScroll ("Shape UV Scroll (xy)", Vector) = (0,0,0,0)
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _DissolveSoft ("Dissolve Softness", Range(0.01,0.5)) = 0.12
        [HDR] _RimColor ("Dissolve Rim", Color) = (3,2,1,1)
        _Alpha ("Alpha", Range(0,2)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "VFXFlow"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _ColorCore, _ColorEdge, _RimColor;
                half _CoreSharp, _NoiseAmount, _Dissolve, _DissolveSoft, _Alpha;
                float4 _NoiseScale, _UVScroll;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 nuv : TEXCOORD1; half4 col : COLOR; };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex) + _UVScroll.xy * _Time.y;
                o.nuv = i.uv * _NoiseScale.xy + _NoiseScale.zw * _Time.y;
                o.col = i.col;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half mask = s.r * s.a;
                half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.nuv).r;
                half shaped = mask * lerp(1.0h, n * 1.6h, _NoiseAmount);
                // dissolve: the shape is eaten through the noise, leaving a hot rim
                half d = shaped - _Dissolve;
                // the shape's own soft falloff is the alpha; the dissolve only cuts it (a hard cut made every soft
                // gradient an opaque block)
                half alpha = saturate(d / _DissolveSoft) * saturate(shaped * 1.25h) * i.col.a * _Alpha;
                half rim = _Dissolve > 0.001h ? saturate(1.0h - abs(d) / (_DissolveSoft * 1.5h)) * step(0.001h, mask) : 0.0h;
                half k = pow(saturate(shaped), _CoreSharp);
                half3 c = lerp(_ColorEdge.rgb, _ColorCore.rgb, k) * i.col.rgb + _RimColor.rgb * rim;
                return half4(c * alpha, alpha * saturate(_ColorEdge.a));
            }
            ENDHLSL
        }
    }
}
