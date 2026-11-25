// PS1 com relevo: mesmo tremor de vértices e textura afim do "PSX Lit", mas a luz principal
// é calculada por pixel com um mapa de normais (tijolo, pedra, madeira e lama ganham volume).
// Luzes pontuais (lanternas, tochas) continuam por vértice, como no PS1.
Shader "Athoria/PSX Lit Relevo"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura", 2D) = "white" {}
        [MainColor] _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normais", 2D) = "bump" {}
        _BumpScale ("Força do relevo", Range(0, 3)) = 1
        _Wetness ("Umidade (brilho)", Range(0, 1)) = 0
        _Cutoff ("Corte de transparência", Range(0, 1)) = 0.45
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Recortar transparência", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 2
        _EmissionMap ("Emissão (textura)", 2D) = "white" {}
        [HDR] _EmissionColor ("Emissão", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Wetness;
                half _Cutoff;
                half4 _EmissionColor;
            CBUFFER_END

            float4 _PSX_SnapResolution;
            float _PSX_Affine;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 uvAffine : TEXCOORD1;
                half3 vertexLight : TEXCOORD2;
                half fog : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
                float4 tangentWS : TEXCOORD5;
                float3 positionWS : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                if (_PSX_SnapResolution.x > 0)
                {
                    float2 grid = _PSX_SnapResolution.xy * 0.5;
                    float w = max(positionCS.w, 1e-4);
                    float2 ndc = floor(positionCS.xy / w * grid + 0.5) / grid;
                    positionCS.xy = ndc * w;
                }
                o.positionCS = positionCS;
                o.positionWS = positionWS;
                float2 uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uv = uv;
                o.uvAffine = float3(uv * positionCS.w, positionCS.w);
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());

                half3 light = 0;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, positionWS);
                    light += l.color * (l.distanceAttenuation * saturate(dot(n.normalWS, l.direction) * 0.75 + 0.25));
                }
                #endif
                o.vertexLight = light;
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

                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                float3 nWS = normalize(i.normalWS);
                float3 tWS = normalize(i.tangentWS.xyz);
                float3 bWS = cross(nWS, tWS) * i.tangentWS.w;
                float3 normalWS = normalize(mul(nTS, float3x3(tWS, bWS, nWS)));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light main = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, main.direction));
                half3 light = SampleSH(normalWS) + main.color * ndl * main.distanceAttenuation * main.shadowAttenuation + i.vertexLight;

                // Umidade: um brilho pequeno e duro (pedra molhada, lama), sem PBR.
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half spec = pow(saturate(dot(normalWS, normalize(main.direction + viewDir))), 48.0) * _Wetness * 1.5;

                half3 color = tex.rgb * light + main.color * spec * main.shadowAttenuation;
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
}
