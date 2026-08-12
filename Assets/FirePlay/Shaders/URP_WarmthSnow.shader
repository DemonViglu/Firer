Shader "DemonViglu/FirePlay/URP Warmth Snow"
{
    Properties
    {
        _BaseColor("Cold Snow", Color) = (0.90, 0.95, 1.0, 1)
        _WarmColor("Thawed Ground", Color) = (0.28, 0.24, 0.16, 1)
        _EdgeColor("Wet Edge", Color) = (0.55, 0.45, 0.27, 1)
        _Smoothness("Snow Smoothness", Range(0, 1)) = 0.38
        _WarmSmoothness("Wet Smoothness", Range(0, 1)) = 0.62
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _WarmColor;
                half4 _EdgeColor;
                half _Smoothness;
                half _WarmSmoothness;
                int _FirePlayWarmthSourceCount;
                float4 _FirePlayWarmthSources[8];
                float _FirePlayWarmthStrengths[8];
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

            float SampleWarmth(float3 positionWS)
            {
                float warmth = 0.0;
                [unroll]
                for (int index = 0; index < 8; index++)
                {
                    if (index >= _FirePlayWarmthSourceCount) break;
                    float4 source = _FirePlayWarmthSources[index];
                    float falloff = 1.0 - saturate(distance(positionWS, source.xyz) / max(source.w, 0.01));
                    float smoothFalloff = falloff * falloff * (3.0 - 2.0 * falloff);
                    warmth = max(warmth, smoothFalloff * _FirePlayWarmthStrengths[index]);
                }

                return saturate(warmth);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half warmth = SampleWarmth(input.positionWS);
                half thaw = smoothstep(0.10, 0.68, warmth);
                half wetEdge = smoothstep(0.06, 0.25, warmth) * (1.0 - smoothstep(0.30, 0.58, warmth));
                half3 surfaceColor = lerp(_BaseColor.rgb, _WarmColor.rgb, thaw);
                surfaceColor = lerp(surfaceColor, _EdgeColor.rgb, wetEdge * 0.62);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half smoothness = lerp(_Smoothness, _WarmSmoothness, thaw + wetEdge * 0.5);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(16.0, 128.0, smoothness));
                half3 lighting = SampleSH(normalWS) + mainLight.color * (0.28 + diffuse * mainLight.shadowAttenuation * 0.72);
                return half4(surfaceColor * lighting + mainLight.color * specular * 0.18, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
