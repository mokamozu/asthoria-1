// Grama, juncos e ervas instanciados (Graphics.DrawMeshInstanced): recorte alfa, as duas
// faces, balanço do vento pelo topo da folha (cor do vértice A = 0 na base, 1 na ponta)
// e tom que varia de touceira para touceira. Tremor PS1 como o resto do cenário.
Shader "Athoria/Folhagem Vento"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura", 2D) = "white" {}
        [MainColor] _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _DryColor ("Cor seca (variação)", Color) = (0.75, 0.68, 0.45, 1)
        _Cutoff ("Corte", Range(0, 1)) = 0.5
        _WindStrength ("Força do vento", Range(0, 1)) = 0.18
        _WindSpeed ("Velocidade do vento", Float) = 1.6
        _WindScale ("Tamanho das rajadas (m)", Float) = 9
        _WindDirection ("Direção do vento", Vector) = (1, 0, 0.35, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _DryColor;
                half _Cutoff;
                half _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float4 _WindDirection;
            CBUFFER_END
            float4 _PSX_SnapResolution;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 color : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);

                // Vento: onda que atravessa o campo + tremor rápido por touceira.
                float bend = v.color.a * v.color.a;
                float2 dir = normalize(_WindDirection.xz + 1e-4);
                float phase = dot(root.xz, dir) / max(_WindScale, 0.1) - _Time.y * _WindSpeed;
                float gust = sin(phase) * 0.7 + sin(phase * 2.3 + 1.7) * 0.3;
                float flutter = sin(_Time.y * 7.0 + Hash(root.xz) * 6.28) * 0.15;
                positionWS.xz += dir * (gust + flutter) * _WindStrength * bend;
                positionWS.y -= abs(gust) * _WindStrength * bend * 0.25;

                float4 positionCS = TransformWorldToHClip(positionWS);
                if (_PSX_SnapResolution.x > 0)
                {
                    float2 grid = _PSX_SnapResolution.xy * 0.5;
                    float w = max(positionCS.w, 1e-4);
                    positionCS.xy = floor(positionCS.xy / w * grid + 0.5) / grid * w;
                }
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                // Folhas finas: luz como se a normal apontasse para cima, base mais escura.
                Light main = GetMainLight();
                half3 light = SampleSH(float3(0, 1, 0)) + main.color * (0.45 + 0.4 * saturate(main.direction.y));
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, positionWS);
                    light += l.color * l.distanceAttenuation * 0.6;
                }
                #endif
                half tint = Hash(floor(root.xz * 0.35));
                half3 hue = lerp(_BaseColor.rgb, _DryColor.rgb, tint * 0.6);
                o.color = hue * light * lerp(0.45, 1.0, v.color.a);
                o.fog = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                half3 color = MixFog(tex.rgb * i.color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
