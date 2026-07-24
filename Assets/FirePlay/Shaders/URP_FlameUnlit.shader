Shader "DemonViglu/FirePlay/URP Flame Unlit"
{
    Properties
    {
        [HDR] _FlameColor("Flame Color", Color) = (1, 0.62, 0.22, 1)
        _FlameIntensity("Flame Intensity", Range(0, 5)) = 1
        _FlickerSpeed("Flicker Speed", Range(0, 10)) = 2
        _FlickerAmount("Flicker Amount", Range(0, 0.5)) = 0.08
        _CoreColor("Core Color", Color) = (1, 0.92, 0.65, 1)
        _EdgeSoftness("Edge Softness", Range(0.01, 0.5)) = 0.16
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FlameColor;
                half4 _CoreColor;
                half _FlameIntensity;
                half _FlickerSpeed;
                half _FlickerAmount;
                half _EdgeSoftness;
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
                float2 uv = input.uv;
                float time = _Time.y * _FlickerSpeed;

                // 火焰底部较宽、顶部收尖；摇摆幅度向顶端逐渐增加。
                float horizontal = uv.x * 2.0 - 1.0;
                float width = lerp(0.48, 0.035, saturate(uv.y));
                float sway = (sin(time + uv.y * 7.0) + sin(time * 1.73 + uv.y * 13.0) * 0.35)
                    * _FlickerAmount * (0.2 + uv.y);
                float flameBody = abs(horizontal - sway) / max(width, 0.001);
                float sideMask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, flameBody);

                // 略微缩短两侧顶部，形成水滴状火焰轮廓。
                float tipMask = 1.0 - smoothstep(0.92, 1.0, uv.y + abs(horizontal - sway) * 0.28);
                float alpha = sideMask * tipMask;

                // 内焰比外焰更亮，并随高度过渡到主色。
                float coreWidth = width * 0.44;
                float core = 1.0 - smoothstep(coreWidth * 0.65, coreWidth, abs(horizontal - sway));
                core *= 1.0 - smoothstep(0.52, 0.92, uv.y);
                half3 color = lerp(_FlameColor.rgb, _CoreColor.rgb, core);

                // 不透明度与发光强度分开：视觉亮度能增加而轮廓不会变硬。
                half intensity = max(_FlameIntensity, 0.0);
                color *= intensity;
                alpha *= saturate(0.72 + intensity * 0.12) * _FlameColor.a;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
