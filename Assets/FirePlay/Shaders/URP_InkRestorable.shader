Shader "DemonViglu/FirePlay/URP Ink Restorable"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [HDR] _BaseColor("Restored Color", Color) = (0.78, 0.67, 0.56, 1)
        [HDR] _InkColor("Ink Color", Color) = (0.24, 0.25, 0.27, 1)
        [HDR] _BloomColor("Bloom Edge Color", Color) = (1, 0.72, 0.82, 1)
        _LitAmount("Lit Amount", Range(0, 1)) = 0
        _NoiseScale("Ink Noise Scale", Range(0.05, 10)) = 1.8
        _EdgeWidth("Bloom Edge Width", Range(0.001, 0.2)) = 0.055
        _BloomIntensity("Bloom Intensity", Range(0, 5)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _InkColor;
                half4 _BloomColor;
                half _LitAmount;
                half _NoiseScale;
                half _EdgeWidth;
                half _BloomIntensity;
                float4 _BaseMap_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float Hash21(float2 samplePosition)
            {
                samplePosition = frac(samplePosition * float2(123.34, 456.21));
                samplePosition += dot(samplePosition, samplePosition + 45.32);
                return frac(samplePosition.x * samplePosition.y);
            }

            float ValueNoise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 local = frac(samplePosition);
                local = local * local * (3.0 - 2.0 * local);

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), local.x);
                float top = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), local.x);
                return lerp(bottom, top, local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float noise = ValueNoise(input.positionWS.xz * _NoiseScale + input.uv * 1.7);
                float restoreMask = smoothstep(noise - _EdgeWidth, noise + _EdgeWidth, _LitAmount);
                float sharpMask = smoothstep(noise - _EdgeWidth * 0.25, noise + _EdgeWidth * 0.25, _LitAmount);
                float edge = saturate(abs(restoreMask - sharpMask) * 3.0);

                half3 detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 restored = detail * _BaseColor.rgb;
                half3 color = lerp(_InkColor.rgb, restored, restoreMask);
                color += _BloomColor.rgb * edge * _BloomIntensity;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
