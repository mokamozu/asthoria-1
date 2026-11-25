// Água das Terras Alagadas. A mesma superfície serve para o pântano turvo e para a
// água negra da peste (mais escura, com brilho de óleo furta-cor que corre com o tempo).
Shader "Athoria/Agua"
{
    Properties
    {
        _BaseMap ("Turbidez (textura)", 2D) = "white" {}
        _BaseColor ("Cor", Color) = (0.16, 0.2, 0.12, 0.85)
        [Normal] _BumpMap ("Ondulação (normais)", 2D) = "bump" {}
        _BumpScale ("Força da ondulação", Range(0, 2)) = 0.6
        _Tiling ("Tamanho do padrão (m)", Float) = 9
        _Flow ("Velocidade da correnteza", Float) = 0.02
        _Specular ("Reflexo da luz", Range(0, 3)) = 1
        _Fresnel ("Reflexo nas bordas", Range(0, 1)) = 0.35
        _ReflectColor ("Cor do reflexo", Color) = (0.55, 0.58, 0.48, 1)
        _Iridescence ("Óleo furta-cor", Range(0, 1)) = 0
        _IridescenceScale ("Escala do óleo", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                float _Tiling;
                float _Flow;
                half _Specular;
                half _Fresnel;
                half4 _ReflectColor;
                half _Iridescence;
                float _IridescenceScale;
            CBUFFER_END

            float4 _PSX_SnapResolution;

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fog : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                if (_PSX_SnapResolution.x > 0)
                {
                    float2 grid = _PSX_SnapResolution.xy * 0.5;
                    float w = max(positionCS.w, 1e-4);
                    positionCS.xy = floor(positionCS.xy / w * grid + 0.5) / grid * w;
                }
                o.positionCS = positionCS;
                o.positionWS = positionWS;
                o.fog = ComputeFogFactor(positionCS.z);
                return o;
            }

            half3 ThinFilm(float t)
            {
                // Arco-íris de película de óleo (cores que giram com o ângulo e o tempo).
                return 0.5 + 0.5 * cos(6.28318 * (t + float3(0.0, 0.33, 0.67)));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.positionWS.xz / max(_Tiling, 0.01);
                float t = _Time.y * _Flow;
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv + float2(t, t * 0.6)), _BumpScale);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv * 1.7 - float2(t * 0.8, -t * 0.4)), _BumpScale);
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                float3 normalWS = normalize(float3(nTS.x, nTS.z, nTS.y));   // plano horizontal: tangente = X, binormal = Z

                half4 murk = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * 0.5 + nTS.xy * 0.05 + t * 0.3);
                half3 color = _BaseColor.rgb * murk.rgb;

                Light main = GetMainLight();
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half fres = pow(1.0 - saturate(dot(normalWS, viewDir)), 4.0) * _Fresnel;
                half3 ambient = SampleSH(float3(0, 1, 0));
                color = color * (ambient + main.color * 0.35) ;
                color = lerp(color, _ReflectColor.rgb * (ambient + main.color * 0.4), fres);
                half spec = pow(saturate(dot(normalWS, normalize(main.direction + viewDir))), 90.0) * _Specular;
                color += main.color * spec;

                // Óleo: faixas furta-cor que dependem do ângulo de visão, da ondulação e do tempo.
                float film = dot(normalWS, viewDir) * _IridescenceScale + murk.r * 2.0 + _Time.y * 0.05;
                half3 oil = ThinFilm(film) * (0.25 + fres * 2.0 + spec * 0.5);
                color = lerp(color, color + oil * 0.35, _Iridescence);

                color = MixFog(color, i.fog);
                return half4(color, saturate(_BaseColor.a + fres * 0.5));
            }
            ENDHLSL
        }
    }
}
