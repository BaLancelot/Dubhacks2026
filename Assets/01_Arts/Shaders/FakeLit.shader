// Unlit shader with a fixed fake directional light, for 3D props under the 2D Renderer
// (2D lights don't shade 3D meshes). Use flat normals for a low-poly look.
Shader "SushiDojo/FakeLit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        // World-space direction the light comes from.
        _LightDir("Light Direction", Vector) = (-0.35, 0.85, -0.4, 0)
        _Ambient("Ambient", Range(0, 1)) = 0.55
        _Diffuse("Diffuse", Range(0, 1)) = 0.5
        // Toon bands for the light. 0 = smooth.
        _ShadeSteps("Shade Steps (0 = smooth)", Range(0, 8)) = 0
        // Overall opacity, usually driven per renderer for fades.
        _Alpha("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _LightDir;
                float _Ambient;
                float _Diffuse;
                float _ShadeSteps;
                float _Alpha;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float ndl = saturate(dot(normalize(input.normalWS), normalize(_LightDir.xyz)));
                if (_ShadeSteps > 0.5)
                    ndl = floor(ndl * _ShadeSteps + 0.5) / _ShadeSteps;

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb * input.color.rgb;
                return half4(albedo * (_Ambient + _Diffuse * ndl), _Alpha);
            }
            ENDHLSL
        }
    }
}
