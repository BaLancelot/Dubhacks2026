// Sprite shader that paints a color over the sprite instead of only multiplying it.
// Tint Amount blends toward the tint color; Keep Shading keeps the sprite's own light and dark under the new color.
// The SpriteRenderer's Color still multiplies on top (and drives alpha). Per object values: use the SpriteTint component.
Shader "SushiDojo/SpriteColorTint"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _TintColor("Tint Color", Color) = (1, 0.5, 0.5, 1)
        // 0 = original sprite, 1 = fully painted with the tint color.
        _TintAmount("Tint Amount", Range(0, 1)) = 1
        // 0 = flat color, 1 = the sprite's original light and dark show through the tint.
        _KeepShading("Keep Shading", Range(0, 1)) = 1
        _Brightness("Brightness", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _TintAmount;
                half _KeepShading;
                half _Brightness;
            CBUFFER_END

            // Mid gray maps to 1, so Keep Shading keeps the tint's own brightness on average.
            static const half ShadingCenter = 0.5h;
            static const half3 LuminanceWeights = half3(0.299h, 0.587h, 0.114h);

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
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(sprite.rgb, LuminanceWeights);
                half shading = lerp(1.0h, luminance / ShadingCenter, _KeepShading);
                half3 painted = _TintColor.rgb * shading;

                half3 rgb = lerp(sprite.rgb, painted, _TintAmount * _TintColor.a) * _Brightness;
                return half4(rgb * input.color.rgb, sprite.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
