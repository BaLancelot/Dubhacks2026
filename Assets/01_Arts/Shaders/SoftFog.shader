// Soft, blurry fog blob on a sprite quad. Noisy radial falloff that slowly flows.
// Vertex alpha fades it in/out. Use a full-rect sprite (e.g. Square).
Shader "SushiDojo/SoftFog"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite (unused)", 2D) = "white" {}

        _Color("Color", Color) = (0.86, 0.88, 0.93, 0.6)
        // Where the falloff starts (0 = center, 1 = edge). Lower is blurrier.
        _InnerRadius("Inner Radius", Range(0, 1)) = 0.15
        _NoiseScale("Noise Scale", Range(0.5, 10)) = 2.5
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.5
        // Brightness variation from the noise.
        _NoiseShade("Noise Shade", Range(0, 0.5)) = 0.08
        _FlowSpeed("Flow Speed", Range(0, 3)) = 0.4

        _Seed("Seed", Float) = 0
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
                half4 _Color;
                float _InnerRadius;
                float _NoiseScale;
                float _NoiseStrength;
                float _NoiseShade;
                float _FlowSpeed;
                float _Seed;
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
                return ValueNoise(p) * 0.5 + ValueNoise(p * 2.03 + 11.0) * 0.3 + ValueNoise(p * 4.01 + 23.0) * 0.2;
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
                float time = _Time.y * _FlowSpeed;
                float2 seedOffset = float2(_Seed * 5.17, _Seed * 2.93);

                float noise = Fbm(p * _NoiseScale + seedOffset + float2(time, -time * 0.7));
                float dist = length(p) + (noise - 0.5) * _NoiseStrength;
                float falloff = 1.0 - smoothstep(_InnerRadius, 1.0, dist);

                half3 color = _Color.rgb * (1.0 + (noise - 0.5) * 2.0 * _NoiseShade) * input.color.rgb;
                half alpha = falloff * _Color.a * input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
