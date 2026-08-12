Shader "Hidden/DemonViglu/FirePlay/Ice Path Stamp"
{
    Properties
    {
        _MainTex("Previous Mask", 2D) = "black" {}
        _BrushUV("Brush UV", Vector) = (0.5, 0.5, 0, 0)
        _BrushRadiusUV("Brush Radius UV", Vector) = (0.02, 0.02, 0, 0)
        _BrushStrength("Brush Strength", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _BrushUV;
            float4 _BrushRadiusUV;
            float _BrushStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half previous = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                float2 normalizedDelta = (input.uv - _BrushUV.xy) / max(_BrushRadiusUV.xy, float2(0.0001, 0.0001));
                half brush = saturate(1.0 - length(normalizedDelta));
                brush = brush * brush * (3.0 - 2.0 * brush);
                half accumulated = saturate(previous + brush * _BrushStrength);
                return accumulated.xxxx;
            }
            ENDHLSL
        }
    }
}
