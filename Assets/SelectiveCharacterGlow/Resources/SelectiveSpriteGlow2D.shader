Shader "TimeEcho/Selective Sprite Glow 2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TargetColor ("Target Color", Color) = (1, 1, 1, 1)
        _ColorTolerance ("Color Tolerance", Range(0.01, 1)) = 0.22
        _SelectionSoftness ("Selection Softness", Range(0.001, 0.5)) = 0.06
        _BrightnessThreshold ("Brightness Threshold", Range(0, 1)) = 0.72
        [HDR] _GlowColor ("Glow Color", Color) = (0.25, 0.9, 1, 1)
        _GlowIntensity ("Glow Intensity", Float) = 5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One One

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _TargetColor;
            half4 _GlowColor;
            half _GlowIntensity;
            half _ColorTolerance;
            half _SelectionSoftness;
            half _BrightnessThreshold;
        CBUFFER_END

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);

            half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            half softness = max(_SelectionSoftness, 0.001h);

            half targetDistance = distance(source.rgb, _TargetColor.rgb);
            half colorSelection = 1.0h - smoothstep(
                _ColorTolerance,
                _ColorTolerance + softness,
                targetDistance);

            half brightness = max(source.r, max(source.g, source.b));
            half brightnessSelection = smoothstep(
                _BrightnessThreshold - softness,
                _BrightnessThreshold + softness,
                brightness);

            half mask = source.a * colorSelection * brightnessSelection;
            half3 emission = _GlowColor.rgb * (_GlowIntensity * mask);
            return half4(emission, mask);
        }
        ENDHLSL

        Pass
        {
            Name "SelectiveGlow2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "SelectiveGlowForward"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    FallBack Off
}
