Shader "DemonViglu/FirePlay/URP Ice Path Crack"
{
    Properties
    {
        [HDR] _BaseColor("Ice Surface", Color) = (0.68, 0.88, 0.96, 0.86)
        [HDR] _DeepColor("Pressed Ice", Color) = (0.18, 0.48, 0.68, 0.88)
        [HDR] _CrackColor("Crack Color", Color) = (0.88, 0.98, 1.0, 1.0)
        _CrackMask("Recorded Path Mask", 2D) = "black" {}
        _IceWorldRect("World Rect (Min XZ, Size XZ)", Vector) = (0, 0, 1, 1)
        _CrackScale("Crack Scale", Range(0.2, 4)) = 1.35
        _CrackThreshold("Crack Threshold", Range(0, 1)) = 0.12
        _BreakThreshold("Break Threshold", Range(0, 1)) = 0.78
        _EdgeSoftness("Broken Edge Softness", Range(0.001, 0.25)) = 0.08
        _Smoothness("Smoothness", Range(0, 1)) = 0.82
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CrackMask);
            SAMPLER(sampler_CrackMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half4 _CrackColor;
                float4 _IceWorldRect;
                half _CrackScale;
                half _CrackThreshold;
                half _BreakThreshold;
                half _EdgeSoftness;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float CrackPattern(float2 worldXZ)
            {
                float2 p = worldXZ * _CrackScale;
                float warpA = sin(p.y * 0.71 + sin(p.x * 0.23) * 2.4);
                float warpB = sin(p.x * 0.83 - sin(p.y * 0.31) * 2.1);
                float branchA = 1.0 - smoothstep(0.035, 0.095, abs(sin(p.x + warpA * 2.2)));
                float branchB = 1.0 - smoothstep(0.025, 0.075, abs(sin(p.y * 1.17 + warpB * 2.7)));
                float breakup = smoothstep(0.28, 0.82, Hash21(floor(p * 0.42)));
                return saturate(branchA + branchB * breakup);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 maskUV = (input.positionWS.xz - _IceWorldRect.xy) / max(_IceWorldRect.zw, float2(0.001, 0.001));
                float inside = step(0.0, maskUV.x) * step(0.0, maskUV.y) * step(maskUV.x, 1.0) * step(maskUV.y, 1.0);
                half pressure = SAMPLE_TEXTURE2D(_CrackMask, sampler_CrackMask, saturate(maskUV)).r * inside;

                half crackPresence = smoothstep(_CrackThreshold, _CrackThreshold + 0.18, pressure);
                half cracks = CrackPattern(input.positionWS.xz) * crackPresence;
                half hole = smoothstep(_BreakThreshold - _EdgeSoftness, _BreakThreshold + _EdgeSoftness, pressure);
                half brokenEdge = smoothstep(_BreakThreshold - 0.2, _BreakThreshold, pressure) * (1.0 - hole);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(16.0, 128.0, _Smoothness));

                half3 iceColor = lerp(_BaseColor.rgb, _DeepColor.rgb, pressure * 0.55);
                half3 lighting = SampleSH(normalWS) + mainLight.color * (0.3 + diffuse * mainLight.shadowAttenuation * 0.7);
                half3 color = iceColor * lighting;
                color += mainLight.color * specular * 0.5;
                color = lerp(color, _CrackColor.rgb, cracks * 0.92);
                color += _CrackColor.rgb * brokenEdge * 0.35;

                half alpha = lerp(_BaseColor.a, _DeepColor.a, pressure * 0.4) * (1.0 - hole);
                clip(alpha - 0.015);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
