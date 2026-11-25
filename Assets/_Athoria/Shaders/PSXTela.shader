// Efeito de tela de jogo antigo: reduz as cores (15 bits, como no PS1) com
// pontilhado ordenado (dithering) e uma névoa de cor nas bordas, como na TV.
// Usado pelo Full Screen Pass do renderer (configurado em Athoria → PS1).
Shader "Athoria/PSX Tela"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "PSXTela"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Níveis por canal (32 = 15 bits do PS1; 0 = desligado) e intensidade do pontilhado.
            float _PSX_ColorLevels;
            float _PSX_Dither;

            static const float Bayer[16] =
            {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uv);
                if (_PSX_ColorLevels > 1)
                {
                    uint2 p = (uint2)(uv * _ScreenParams.xy) & 3u;
                    float threshold = Bayer[p.y * 4u + p.x] / 16.0 - 0.5;
                    float levels = _PSX_ColorLevels - 1;
                    color.rgb = floor(saturate(color.rgb + threshold * _PSX_Dither / levels) * levels + 0.5) / levels;
                }
                return color;
            }
            ENDHLSL
        }
    }
}
