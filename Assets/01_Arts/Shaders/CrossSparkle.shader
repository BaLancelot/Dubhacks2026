// Cartoon "+" twinkle: tapered cross rays with optional short diagonal rays.
// Vertex alpha fades it. Use a full-rect sprite (e.g. Square).
Shader "SushiDojo/CrossSparkle"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite (unused)", 2D) = "white" {}

        _Color("Color", Color) = (1, 0.9, 0.25, 1)
        // Ray half-width at the center, in quad space (quad half-size = 1).
        _Thickness("Thickness", Range(0.01, 0.3)) = 0.08
        // Diagonal ray length relative to the main rays. 0 hides them.
        _DiagonalLength("Diagonal Length", Range(0, 1)) = 0.45
        _DiagonalThickness("Diagonal Thickness", Range(0.01, 0.3)) = 0.05
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
                float _Thickness;
                float _DiagonalLength;
                float _DiagonalThickness;
            CBUFFER_END

            // Signed coverage of a ray along +-X that tapers to a point at |x| = length.
            float Ray(float2 p, float length, float thickness)
            {
                float along = abs(p.x) / max(length, 1e-4);
                float halfWidth = thickness * saturate(1.0 - along);
                return halfWidth - abs(p.y);
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

                float shape = max(Ray(p, 1.0, _Thickness), Ray(p.yx, 1.0, _Thickness));

                if (_DiagonalLength > 0.0)
                {
                    const float invSqrt2 = 0.70710678;
                    float2 d = float2(p.x + p.y, p.x - p.y) * invSqrt2;
                    shape = max(shape, Ray(d, _DiagonalLength, _DiagonalThickness));
                    shape = max(shape, Ray(d.yx, _DiagonalLength, _DiagonalThickness));
                }

                float edge = max(fwidth(shape), 1e-4);
                float coverage = saturate(shape / edge + 0.5);

                return half4(_Color.rgb * input.color.rgb, coverage * _Color.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
