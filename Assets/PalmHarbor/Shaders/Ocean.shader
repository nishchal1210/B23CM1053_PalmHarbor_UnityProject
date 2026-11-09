Shader "PalmHarbor/Ocean"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (0.045, 0.22, 0.30, 1)
        _ShallowColor ("Shallow water", Color) = (0.12, 0.53, 0.53, 1)
        _FoamColor ("Shore foam", Color) = (0.72, 0.87, 0.79, 1)
        _ReflectionColor ("Dusk sky reflection", Color) = (0.48, 0.38, 0.48, 1)
        [HDR] _SunColor ("Sunset reflection", Color) = (1.0, 0.53, 0.29, 1)
        _SunDirection ("Sun direction · world XYZ", Vector) = (0.97, 0.12, 0.20, 0)
        _ShorelineX ("Shoreline · world X", Float) = 74
        _DepthRange ("Shallow to deep distance", Float) = 42
        _WaveHeight ("Wave height · metres", Range(0, 0.4)) = 0.09
        _WaveSpeed ("Wave speed", Range(0, 2)) = 0.65
        _ReflectionStrength ("Sunset reflection strength", Range(0, 1)) = 0.32
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }
        Cull Back
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;
            half4 _ShallowColor;
            half4 _FoamColor;
            half4 _ReflectionColor;
            half4 _SunColor;
            float4 _SunDirection;
            float _ShorelineX;
            float _DepthRange;
            float _WaveHeight;
            float _WaveSpeed;
            float _ReflectionStrength;
        CBUFFER_END

        float OceanHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float OceanNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(OceanHash(i), OceanHash(i + float2(1, 0)), f.x),
                        lerp(OceanHash(i + float2(0, 1)), OceanHash(i + 1.0), f.x), f.y);
        }

        float3 DisplaceWater(float3 positionWS)
        {
            float time = _Time.y * _WaveSpeed;
            float fade = smoothstep(0.0, 6.0, positionWS.x - _ShorelineX);
            float waves = sin(positionWS.x * 0.34 + positionWS.z * 0.09 - time * 0.80)
                        + sin(positionWS.x * 0.17 - positionWS.z * 0.23 + time * 0.63) * 0.48;
            positionWS.y += waves * _WaveHeight * fade;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "OceanForward"
            Tags { "LightMode"="UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OceanVertex
            #pragma fragment OceanFragment
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings OceanVertex(Attributes input)
            {
                Varyings output;
                output.positionWS = DisplaceWater(TransformObjectToWorld(input.positionOS.xyz));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 OceanFragment(Varyings input) : SV_Target
            {
                float2 surface = input.positionWS.xz;
                float time = _Time.y * _WaveSpeed;
                float shoreDistance = surface.x - _ShorelineX;
                float depth = smoothstep(0.0, max(1.0, _DepthRange), shoreDistance);
                float broadNoise = OceanNoise(surface * 0.037 + float2(time * 0.012, 0));
                half3 colour = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depth + (broadNoise - 0.5) * 0.16));

                // Analytic wave slopes keep highlights smooth even on a coarse ocean mesh.
                float phaseA = surface.x * 0.34 + surface.y * 0.09 - time * 0.80;
                float phaseB = surface.x * 0.17 - surface.y * 0.23 + time * 0.63;
                float2 slope = cos(phaseA) * float2(0.34, 0.09) + cos(phaseB) * float2(0.17, -0.23) * 0.48;
                slope *= _WaveHeight;
                float rippleA = sin(dot(surface, float2(0.83, 0.37)) + time * 0.42);
                float rippleB = sin(dot(surface, float2(0.22, -1.13)) - time * 0.37);
                slope += float2(rippleA, rippleB) * 0.027;
                float3 normalWS = normalize(float3(-slope.x, 1.0, -slope.y));
                float3 viewDirection = normalize(GetCameraPositionWS() - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirection)), 3.0);
                colour = lerp(colour, _ReflectionColor.rgb, fresnel * 0.43);

                float3 sunDirection = normalize(_SunDirection.xyz + float3(0, 0.00001, 0));
                float3 reflectedView = reflect(-viewDirection, normalWS);
                float sunAlignment = saturate(dot(reflectedView, sunDirection));
                float reflection = pow(sunAlignment, 45.0);
                float brokenReflection = 0.52 + 0.48 * smoothstep(-0.3, 0.75, sin(phaseA * 2.2 + broadNoise * 3.0));
                colour += _SunColor.rgb * reflection * brokenReflection * _ReflectionStrength;

                float ripple = (rippleA * 0.55 + rippleB * 0.45) * 0.5 + 0.5;
                colour += half3(0.033, 0.052, 0.049) * smoothstep(0.66, 1.0, ripple) * (0.35 + 0.65 * fresnel);

                // Gentle irregular wash within the first few metres of the waterline.
                float shoreNoise = OceanNoise(float2(surface.y * 0.12, time * 0.08));
                float wash = sin(shoreDistance * 1.10 - time * 0.90 + shoreNoise * 2.2) * 0.5 + 0.5;
                float foam = smoothstep(0.80, 0.99, wash) * (1.0 - smoothstep(0.5, 8.5, shoreDistance));
                foam *= 0.34 + OceanNoise(surface * float2(0.9, 0.55)) * 0.30;
                colour = lerp(colour, _FoamColor.rgb, foam);
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = DisplaceWater(TransformObjectToWorld(input.positionOS.xyz));
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }
            half4 DepthFragment(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
