// One cartoon cloud puff on a sprite quad.
// Sphere-like toon shading in 3 bands, noisy bubbling edge, noise dissolve driven by vertex alpha.
// Use a full-rect sprite (e.g. Square). Set _Seed per renderer via MaterialPropertyBlock.
Shader "SushiDojo/ToonPuff"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite (unused)", 2D) = "white" {}

        [Header(Toon Shading)]
        _LightColor("Light Color", Color) = (1, 1, 1, 1)
        _MidColor("Mid Color", Color) = (0.82, 0.84, 0.88, 1)
        _ShadowColor("Shadow Color", Color) = (0.6, 0.63, 0.7, 1)
        _LightDir("Light Direction", Vector) = (-0.4, 0.7, 0.6, 0)
        _LightThreshold("Light Threshold", Range(-1, 1)) = 0.55
        _ShadowThreshold("Shadow Threshold", Range(-1, 1)) = 0.15
        _ShadeNoise("Shade Noise", Range(0, 1)) = 0.25

        [Header(Shape)]
        _Radius("Radius", Range(0.1, 1)) = 0.78
        _EdgeNoise("Edge Noise", Range(0, 0.5)) = 0.12
        _EdgeFrequency("Edge Frequency", Range(0.5, 10)) = 3
        _BubbleSpeed("Bubble Speed", Range(0, 10)) = 2.5

        [Header(Dissolve)]
        _DissolveScale("Dissolve Scale", Range(1, 20)) = 6

        _Seed("Seed", Float) = 0
        // Puff Z rotation in radians, set per renderer so the light stays fixed in world space.
        _Rotation("Rotation", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _MidColor;
                half4 _ShadowColor;
                float4 _LightDir;
                float _LightThreshold;
                float _ShadowThreshold;
                float _ShadeNoise;
                float _Radius;
                float _EdgeNoise;
                float _EdgeFrequency;
                float _BubbleSpeed;
                float _DissolveScale;
                float _Seed;
                float _Rotation;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                return ValueNoise(p) * 0.65 + ValueNoise(p * 2.1 + 17.0) * 0.35;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float dist = length(p);
                float time = _Time.y * _BubbleSpeed;
                float2 seedOffset = float2(_Seed * 7.13, _Seed * 3.71);

                // Bubbling edge: noise sampled around the circle so there is no seam.
                float2 dir = dist > 1e-4 ? p / dist : float2(0, 1);
                float edge = Fbm(dir * _EdgeFrequency + seedOffset + time) - 0.5;
                float radius = _Radius + edge * 2.0 * _EdgeNoise;
                clip(radius - dist);

                // Fake sphere normal for toon bands.
                float r = dist / radius;
                float3 normal = float3(p / radius, sqrt(saturate(1.0 - r * r)));
                // Undo the puff's rotation so the light direction stays fixed on screen.
                float3 light = normalize(_LightDir.xyz);
                float sinR, cosR;
                sincos(-_Rotation, sinR, cosR);
                light.xy = float2(cosR * light.x - sinR * light.y, sinR * light.x + cosR * light.y);
                float ndl = dot(normalize(normal), light);
                ndl += (Fbm(p * 3.0 + seedOffset - time * 0.5) - 0.5) * 2.0 * _ShadeNoise;

                half3 color = ndl > _LightThreshold ? _LightColor.rgb
                            : ndl > _ShadowThreshold ? _MidColor.rgb
                            : _ShadowColor.rgb;

                // Vertex alpha 1 = solid, 0 = fully eaten away.
                float dissolve = 1.0 - input.color.a;
                clip(Fbm(p * _DissolveScale + seedOffset) - dissolve * 1.01);

                return half4(color * input.color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
