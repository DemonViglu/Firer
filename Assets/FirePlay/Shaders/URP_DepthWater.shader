Shader "DemonViglu/FirePlay/URP Depth Water"
{
    Properties
    {
        [HDR] _ShallowColor("Shallow Water", Color) = (0.20, 0.62, 0.72, 0.32)
        [HDR] _DeepColor("Deep Water", Color) = (0.025, 0.12, 0.24, 0.92)
        [HDR] _FresnelColor("Fresnel", Color) = (0.72, 0.92, 1.0, 0.72)
        _DepthDistance("Full Depth Distance", Range(0.5, 20)) = 7.5
        _WaveScale("Wave Scale", Range(0.02, 1)) = 0.14
        _WaveSpeed("Wave Speed", Range(0, 2)) = 0.22
        _WaveStrength("Wave Strength", Range(0, 0.3)) = 0.075
        _Smoothness("Smoothness", Range(0, 1)) = 0.88
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
        }

        Pass
        {
            Name "DepthWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FresnelColor;
                float _DepthDistance;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
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
                float4 screenPosition : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.screenPosition = ComputeScreenPos(positions.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPosition.xy / max(input.screenPosition.w, 0.0001);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float surfaceEyeDepth = -TransformWorldToView(input.positionWS).z;
                half depth01 = saturate((sceneEyeDepth - surfaceEyeDepth) / max(_DepthDistance, 0.01));

                float phase = _Time.y * _WaveSpeed;
                float2 wavePosition = input.positionWS.xz * _WaveScale;
                float2 waveSlope = float2(
                    sin(wavePosition.x + phase) + sin(wavePosition.y * 1.31 - phase * 0.73),
                    cos(wavePosition.y - phase * 0.81) + cos(wavePosition.x * 1.17 + phase * 0.62));
                half3 normalWS = normalize(input.normalWS + half3(waveSlope.x, 0.0, waveSlope.y) * _WaveStrength);
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirection)), 4.0h);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(24.0h, 160.0h, _Smoothness));

                half4 water = lerp(_ShallowColor, _DeepColor, smoothstep(0.0h, 1.0h, depth01));
                half3 lighting = SampleSH(normalWS) + mainLight.color * (0.24h + diffuse * 0.45h * mainLight.shadowAttenuation);
                half3 color = water.rgb * lighting;
                color += mainLight.color * specular * 0.65h;
                color = lerp(color, _FresnelColor.rgb, fresnel * _FresnelColor.a);
                half alpha = saturate(water.a + fresnel * 0.18h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
