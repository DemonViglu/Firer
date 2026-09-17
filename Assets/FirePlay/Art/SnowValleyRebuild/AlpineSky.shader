Shader "FirePlay/Environment/AlpineSky"
{
    Properties
    {
        _TopColor("Zenith", Color) = (0.18,0.38,0.61,1)
        _HorizonColor("Horizon", Color) = (0.68,0.81,0.88,1)
        _GroundColor("Ground haze", Color) = (0.49,0.62,0.72,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _TopColor, _HorizonColor, _GroundColor;
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(positionOS.xyz);
                o.direction=positionOS.xyz;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float h=normalize(i.direction).y;
                float3 upper=lerp(_HorizonColor.rgb,_TopColor.rgb,smoothstep(0,0.85,h));
                return half4(lerp(upper,_GroundColor.rgb,smoothstep(0,0.4,-h)),1);
            }
            ENDHLSL
        }
    }
}
