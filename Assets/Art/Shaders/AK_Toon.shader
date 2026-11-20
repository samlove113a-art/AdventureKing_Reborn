// Adventure King house style: stepped cel lighting + soft band, warm rim, inverted-hull outline.
// Shared by characters, weapons, NPCs, monsters and props so the whole cast reads as one style.
// URP (Forward / Forward+), mobile friendly: one main light with shadows + SH ambient, no extra lights.
Shader "AdventureKing/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.62,0.58,0.72,1)
        _ShadeThreshold ("Shade Threshold", Range(0,1)) = 0.48
        _ShadeSoftness ("Shade Softness", Range(0.001,0.3)) = 0.045
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.35
        _RimColor ("Rim Color", Color) = (1,0.94,0.82,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim Strength", Range(0,1)) = 0.28
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        _OutlineColor ("Outline Color", Color) = (0.13,0.09,0.12,1)
        _OutlineWidth ("Outline Width (world)", Range(0,0.05)) = 0.006
        _FlashColor ("Hit Flash Color", Color) = (1,1,1,0)
        [Toggle(_VERTEX_COLOR)] _UseVertexColor ("Multiply Vertex Color", Float) = 0
        // ---- hero face layer (HeroFaceRig measures, HeroFace animates). Face units = face plane / hips-to-head.
        [Toggle(_FACE)] _FaceLayer ("Face Layer", Float) = 0
        _EyeA ("Eye A (cx cy rx ry)", Vector) = (0,0,0.1,0.1)
        _EyeB ("Eye B (cx cy rx ry)", Vector) = (0,0,0.1,0.1)
        _MouthRect ("Mouth (cx cy hw hh)", Vector) = (0,0,0.1,0.02)
        _FaceSkin ("Face Skin", Color) = (1,0.82,0.72,1)
        _LidColorA ("Lid A", Color) = (1,0.82,0.72,1)
        _LidColorB ("Lid B", Color) = (1,0.82,0.72,1)
        _LashColor ("Lash", Color) = (0.12,0.06,0.06,1)
        _MouthColor ("Mouth Line", Color) = (0.6,0.3,0.3,1)
        _FaceEyes ("Eyes (open bend tilt squint)", Vector) = (1,0,0,0)
        _FaceMouth ("Mouth (curve open wide blush)", Vector) = (0.35,0,0.5,0)
        _FaceFx ("Fx (hurt tear - -)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadeColor;
            half _ShadeThreshold;
            half _ShadeSoftness;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _EmissionColor;
            half4 _OutlineColor;
            float _OutlineWidth;
            half4 _FlashColor;
            float4 _EyeA, _EyeB, _MouthRect;
            half4 _FaceSkin, _LidColorA, _LidColorB, _LashColor, _MouthColor;
            float4 _FaceEyes, _FaceMouth, _FaceFx;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _VERTEX_COLOR
            #pragma shader_feature_local _FACE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 face : TEXCOORD3; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
                float4 face : TEXCOORD4;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

        #if _FACE
            // Rounded-rectangle "superellipse" distance (1 = edge) - covers painted lash flicks in the corners.
            float SuperEllipse(float2 d) { float2 a = abs(d); a *= a; a *= a; return sqrt(sqrt(a.x + a.y)); }

            float SegmentDist(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // One eye: lids close over the painted eye (identity kept), lash line rides the upper lid edge.
            void EyeLayer(float2 p, float4 E, half3 lid, float faceCx, inout half3 col)
            {
                float open = _FaceEyes.x, bend = _FaceEyes.y, tilt = _FaceEyes.z, squint = _FaceEyes.w, hurt = _FaceFx.x;
                float2 d = (p - E.xy) / E.zw;
                float innerSign = faceCx > E.x ? 1.0 : -1.0;
                float xi = d.x * innerSign;                  // +1 = inner corner (towards the nose)
                float aa = max(fwidth(d.y), 1e-4) * 1.25;
                float reg = SuperEllipse(float2(d.x / 1.14, d.y / 1.22));
                float inReg = 1.0 - smoothstep(1.0 - aa * 2.0, 1.0, reg);
                float c = 1.0 - open;
                float x2 = saturate(d.x * d.x);
                float closedY = -0.12 + bend * 0.55 * (1.0 - x2);
                // Upper lid edge: parked above the eye when open, sweeps down as an arc, tilts for anger/sadness.
                float L = lerp(1.4, closedY, c) - c * (1.0 - c) * 0.7 * x2 - tilt * 0.55 * (xi + 0.25);
                float meet = max(squint, smoothstep(0.55, 1.0, c));
                float Lb = lerp(-1.4, closedY - 0.04, meet) + meet * (1.0 - meet) * 0.5 * x2;
                float cover = saturate(smoothstep(-aa, aa, d.y - L) + smoothstep(-aa, aa, Lb - d.y)) * inReg;
                float th = 0.085 + 0.055 * c;
                float ends = saturate((1.12 - abs(d.x)) * 7.0);
                float lash = (1.0 - smoothstep(th - aa, th + aa, abs(d.y - L))) * saturate((1.05 - L) * 4.0) * ends;
                // Pain: eye squeezed shut into a chevron pointing at the nose.
                float2 q = float2(xi, d.y);
                float chev = min(SegmentDist(q, float2(0.5, 0.0), float2(-0.75, 0.55)), SegmentDist(q, float2(0.5, 0.0), float2(-0.75, -0.55)));
                float chevMask = 1.0 - smoothstep(0.11 - aa, 0.11 + aa, chev);
                cover = lerp(cover, inReg, hurt);
                lash = lerp(lash, chevMask, hurt);
                col = lerp(col, lid, cover);
                col = lerp(col, _LashColor.rgb, saturate(lash));
                // Tear (sadness) under the outer corner.
                float2 t = (p - (E.xy + float2(-innerSign * E.z * 0.7, -E.w * 1.25))) / E.z;
                float drop = length(float2(t.x * 1.6, max(t.y, 0.0) * 0.8 + min(t.y, 0.0) * 1.6));
                col = lerp(col, half3(0.62, 0.86, 1.0), _FaceFx.y * (1.0 - smoothstep(0.16, 0.2, drop)));
                // Extra blush on the cheek.
                float2 bc = (p - (E.xy + float2(-innerSign * E.z * 0.15, -E.w * 1.55))) / (E.z * 0.95);
                col = lerp(col, half3(1.0, 0.56, 0.56), _FaceMouth.w * 0.4 * saturate(1.0 - dot(bc, bc)));
            }

            // Mouth: the painted one is covered with skin and redrawn so it can smile, frown, open and tremble.
            void MouthLayer(float2 p, inout half3 col)
            {
                float curve = _FaceMouth.x, open = _FaceMouth.y, wide = _FaceMouth.z, hurt = _FaceFx.x;
                float hw = _MouthRect.z;
                float2 m = (p - _MouthRect.xy) / hw;
                float aa = max(fwidth(m.y), 1e-4) * 1.25;
                float ry = max(_MouthRect.w / hw * 1.7, 0.42);
                float coverReg = SuperEllipse(float2(m.x / 1.22, m.y / ry));
                // Feathered edge: a hard-edged skin patch showed as a pale rounded box against shaded cheeks.
                col = lerp(col, _FaceSkin.rgb, 1.0 - smoothstep(0.6, 1.05, coverReg));
                float W = lerp(0.55, 1.0, wide);
                float xn = m.x / W;
                float inX = saturate((1.0 - abs(xn)) * 9.0);
                float yc = curve * 0.32 * (xn * xn) - curve * 0.12 + hurt * 0.06 * sin(m.x * 16.0);
                float yl = yc - open * 0.6 * sqrt(saturate(1.0 - xn * xn));
                float inside = step(abs(xn), 1.0) * smoothstep(-aa, aa, yc - m.y) * smoothstep(-aa, aa, m.y - yl) * step(0.02, open);
                half3 mouthIn = lerp(half3(0.42, 0.1, 0.12), half3(0.95, 0.45, 0.48), smoothstep(yl + open * 0.25, yl, m.y));
                col = lerp(col, mouthIn, inside);
                float th = 0.06 + 0.02 * open;
                float upperLip = (1.0 - smoothstep(th - aa, th + aa, abs(m.y - yc))) * inX;
                float lowerLip = (1.0 - smoothstep(th - aa, th + aa, abs(m.y - yl))) * inX * step(0.02, open);
                col = lerp(col, _MouthColor.rgb, saturate(max(upperLip, lowerLip)));
            }

            half3 ApplyFace(float4 face, half3 albedo)
            {
                float mask = saturate((face.z - 0.5) * 4.0);
                if (mask <= 0.0) return albedo;
                half3 col = albedo;
                float faceCx = (_EyeA.x + _EyeB.x) * 0.5;
                EyeLayer(face.xy, _EyeA, _LidColorA.rgb, faceCx, col);
                EyeLayer(face.xy, _EyeB, _LidColorB.rgb, faceCx, col);
                MouthLayer(face.xy, col);
                return lerp(albedo, col, mask);
            }
        #endif

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.face = v.face;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
            #if _VERTEX_COLOR
                albedo *= i.color;
            #endif
            #if _FACE
                albedo.rgb = ApplyFace(i.face, albedo.rgb);
            #endif
                half3 n = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = dot(n, mainLight.direction) * 0.5h + 0.5h;
                half lit = ndl * lerp(0.35h, 1.0h, mainLight.shadowAttenuation);
                half band = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, lit);
                half3 lightCol = min(mainLight.color, 1.25h);
                half3 diffuse = albedo.rgb * lerp(_ShadeColor.rgb, half3(1, 1, 1), band) * lightCol;
                half3 ambient = albedo.rgb * SampleSH(n) * _AmbientStrength;
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower) * _RimStrength * (0.35h + 0.65h * band);
                half3 col = diffuse + ambient + rim * _RimColor.rgb + _EmissionColor.rgb;
                col = lerp(col, _FlashColor.rgb, _FlashColor.a);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 face : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half fog : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                // Hero face (UV3.z > 0.5): the hull's back faces showed through the shallow eye/mouth dips as black
                // slivers. Thin the outline there and push it slightly away from the camera so the face surface
                // hides it; the cheek/chin silhouette keeps a thin line. Meshes without UV3 read 0 = unchanged.
                float faceMask = saturate((v.face.z - 0.5) * 4.0);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                posWS += nWS * _OutlineWidth * lerp(1.0, 0.25, faceMask);
                o.positionCS = TransformWorldToHClip(posWS);
            #if UNITY_REVERSED_Z
                o.positionCS.z -= faceMask * 0.0015 * o.positionCS.w;
            #else
                o.positionCS.z += faceMask * 0.0015 * o.positionCS.w;
            #endif
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 c = lerp(_OutlineColor.rgb, _FlashColor.rgb, _FlashColor.a * 0.5h);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = cs;
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
