// Material "de PS1/N64" para o Athoria:
//  - vértices presos numa grade de baixa resolução (o "tremor" do PS1);
//  - textura com mapeamento afim (a distorção das texturas do PS1);
//  - luz calculada por vértice (Gouraud), inclusive das tochas;
//  - névoa do URP (a mesma da cena), para o cenário sumir na neblina.
// Os valores globais (_PSX_SnapResolution, _PSX_Affine) vêm de GameSettings
// (opção "Estilo PS1" do menu de configurações).
Shader "Athoria/PSX Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura", 2D) = "white" {}
        [MainColor] _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _Cutoff ("Corte de transparência", Range(0, 1)) = 0.45
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Recortar transparência", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 2
        _EmissionMap ("Emissão (textura)", 2D) = "white" {}
        [HDR] _EmissionColor ("Emissão", Color) = (0, 0, 0, 1)
        _Smoothness ("(não usado)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half4 _EmissionColor;
                half _Smoothness;
            CBUFFER_END

            // Globais: (largura, altura) da grade de vértices; 0 = desligado. Quanto da textura é afim (0..1).
            float4 _PSX_SnapResolution;
            float _PSX_Affine;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 uvAffine : TEXCOORD1;   // uv * w, w
                half3 light : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                // Tremor do PS1: prende o vértice numa grade de tela de baixa resolução.
                if (_PSX_SnapResolution.x > 0)
                {
                    float2 grid = _PSX_SnapResolution.xy * 0.5;
                    float w = max(positionCS.w, 1e-4);
                    float2 ndc = positionCS.xy / w;
                    ndc = floor(ndc * grid + 0.5) / grid;
                    positionCS.xy = ndc * w;
                }
                o.positionCS = positionCS;

                float2 uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uv = uv;
                o.uvAffine = float3(uv * positionCS.w, positionCS.w);

                // Luz por vértice (Gouraud): sol/lua + ambiente + luzes pontuais (tochas).
                Light mainLight = GetMainLight();
                half3 light = SampleSH(normalWS);
                light += mainLight.color * saturate(dot(normalWS, mainLight.direction)) * mainLight.distanceAttenuation;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, positionWS);
                    light += l.color * (l.distanceAttenuation * saturate(dot(normalWS, l.direction) * 0.75 + 0.25));
                }
                #endif
                o.light = light;
                o.fog = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 affineUv = i.uvAffine.xy / max(i.uvAffine.z, 1e-4);
                float2 uv = lerp(i.uv, affineUv, saturate(_PSX_Affine));
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                clip(tex.a - _Cutoff);
                #endif
                half3 color = tex.rgb * i.light;
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Sombras e profundidade reaproveitam os passes do Lit do URP.
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
