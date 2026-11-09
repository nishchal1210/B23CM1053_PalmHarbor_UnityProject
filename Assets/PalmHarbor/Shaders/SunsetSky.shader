Shader "PalmHarbor/SunsetSky"
{
    Properties
    {
        _TopColor ("Zenith · coastal blue", Color) = (0.373, 0.498, 0.663, 1)
        _HorizonColor ("Horizon · warm peach", Color) = (0.953, 0.714, 0.647, 1)
        _BottomColor ("Below horizon · coastal haze", Color) = (0.776, 0.655, 0.667, 1)
        _SunDirection ("Sun direction · world XYZ", Vector) = (0.97, 0.12, 0.20, 0)
        [HDR] _SunColor ("Sun colour", Color) = (1.60, 1.10, 0.65, 1)
        _SunSize ("Sun radius · degrees", Range(0.3, 8)) = 2.8
        _CloudAmount ("High cloud veil", Range(0, 1)) = 0.28
        _Exposure ("Exposure", Range(0.1, 2)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "PalmHarborSky"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                float4 _SunDirection;
                half4 _SunColor;
                float _SunSize;
                float _CloudAmount;
                float _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 directionWS : TEXCOORD0; };

            float SkyHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SkyNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(SkyHash(i), SkyHash(i + float2(1, 0)), f.x),
                            lerp(SkyHash(i + float2(0, 1)), SkyHash(i + 1.0), f.x), f.y);
            }

            float CloudNoise(float2 p)
            {
                return SkyNoise(p) * 0.57 + SkyNoise(p * 2.03 + 7.4) * 0.28
                     + SkyNoise(p * 4.11 + 23.7) * 0.15;
            }

            Varyings SkyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionWS = TransformObjectToWorldDir(input.positionOS.xyz);
                return output;
            }

            half4 SkyFragment(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.directionWS);
                float height = direction.y;
                // Keep peach close to the sea horizon; a broad complementary blend reads grey.
                float upper = 1.0 - exp(-saturate(height) * 10.0);
                half3 colour = lerp(_HorizonColor.rgb, _TopColor.rgb, upper);
                colour = lerp(colour, _BottomColor.rgb, 1.0 - smoothstep(-0.40, -0.01, height));

                float3 sunDirection = normalize(_SunDirection.xyz + float3(0, 0.00001, 0));
                float sunAlignment = saturate(dot(direction, sunDirection));
                float sunRadius = radians(_SunSize);
                float disk = smoothstep(cos(sunRadius), cos(sunRadius * 0.84), sunAlignment);
                float halo = pow(sunAlignment, 34.0) * 0.17;
                float innerGlow = pow(sunAlignment, 240.0) * 0.10;
                colour += _SunColor.rgb * (halo + innerGlow);

                // Stretched, slowly drifting noise makes a thin high-altitude cloud veil.
                float2 cloudUV = direction.xz / max(height + 0.22, 0.10);
                cloudUV = cloudUV * float2(1.6, 5.0) + float2(_Time.y * 0.0018, 0);
                float cloud = smoothstep(0.46, 0.77, CloudNoise(cloudUV));
                cloud *= smoothstep(0.02, 0.18, height) * (1.0 - smoothstep(0.55, 0.90, height));
                half3 highCloudColour = lerp(_TopColor.rgb, half3(0.82, 0.84, 0.87), 0.42);
                half3 cloudColour = lerp(_HorizonColor.rgb * 1.03, highCloudColour, upper);
                colour = lerp(colour, cloudColour, cloud * _CloudAmount);
                colour = lerp(colour, _SunColor.rgb, disk * (1.0 - cloud * 0.30));
                return half4(colour * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
