// Full screen pixelation. Use with URP's Full Screen Pass Renderer Feature.
Shader "SushiDojo/PixelatePost"
{
    Properties
    {
        // Pixel rows across the screen height. Lower is chunkier.
        _VerticalResolution("Vertical Resolution", Range(16, 1080)) = 270
        // Color levels per channel. 0 disables posterize.
        _ColorSteps("Color Steps (0 = off)", Range(0, 64)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Pixelate"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _VerticalResolution;
            float _ColorSteps;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Square pixels: same block size on both axes.
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 blocks = float2(_VerticalResolution * aspect, _VerticalResolution);
                float2 uv = (floor(input.texcoord * blocks) + 0.5) / blocks;

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                if (_ColorSteps > 0.5)
                    color.rgb = floor(color.rgb * _ColorSteps + 0.5) / _ColorSteps;

                return color;
            }
            ENDHLSL
        }
    }
}
