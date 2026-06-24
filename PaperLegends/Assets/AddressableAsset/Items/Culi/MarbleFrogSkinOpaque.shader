Shader "Custom/MarbleFrogSkinOpaque"
{
    Properties
    {
        _BaseColor ("Mau nen duc (Base)", Color) = (0.88, 0.90, 0.82, 1.0)

        _SpotColorA ("Mau dom A", Color) = (0.20, 0.30, 0.85, 1.0)
        _SpotColorB ("Mau dom B", Color) = (0.48, 0.22, 0.70, 1.0)
        _SpotColorC ("Mau dom C", Color) = (0.10, 0.65, 0.50, 1.0)

        _SpotScale ("Ti le dom", Range(2, 80)) = 24
        _SpotDensity ("Mat do dom", Range(0.2, 2.0)) = 1.0
        _SpotThreshold ("Kich thuoc dom", Range(0.3, 0.9)) = 0.62
        _SpotSoftness ("Do mem vien dom", Range(0.001, 0.2)) = 0.05
        _BumpStrength ("Do go dom", Range(0, 1.5)) = 0.45

        _Smoothness ("Do bong", Range(0, 1)) = 0.18
        _SpecularStrength ("Do sang phan xa", Range(0, 2)) = 0.35
        _AmbientStrength ("Do sang nen toi thieu (luon thay dom)", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 250

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SpotColorA;
                half4 _SpotColorB;
                half4 _SpotColorC;

                half _SpotScale;
                half _SpotDensity;
                half _SpotThreshold;
                half _SpotSoftness;
                half _BumpStrength;

                half _Smoothness;
                half _SpecularStrength;
                half _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 objectPos   : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir     : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
            };

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float valueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0, 0, 0));
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);

                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);
                return lerp(nxy0, nxy1, u.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                v += a * valueNoise3D(p); p *= 2.02; a *= 0.5;
                v += a * valueNoise3D(p); p *= 2.03; a *= 0.5;
                v += a * valueNoise3D(p); p *= 2.01; a *= 0.5;
                v += a * valueNoise3D(p);
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.objectPos   = IN.positionOS.xyz;
                OUT.worldPos    = posInputs.positionWS;
                OUT.worldNormal = normalInputs.normalWS;
                OUT.viewDir = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.worldNormal);
                float3 V = normalize(IN.viewDir);

                // Dung object space de noise luon khop voi kich thuoc object
                // worldPos se bi qua nho voi vien bi (chenh lech ~0.02 unit) nen dot bien mat
                float3 p = IN.objectPos * (_SpotScale * _SpotDensity);

                float n1 = fbm(p);
                float n2 = fbm(p * 1.91 + float3(17.0, 5.0, 11.0));
                float n3 = fbm(p * 1.37 + float3(4.0, 29.0, 8.0));

                // Noise tan so thap (~0.2x) de tao kich thuoc cham ngau nhien x2–x5
                // nSize cao -> sizeOffset lon -> nguong giam -> cham to hon
                float nSize = fbm(p * 0.22 + float3(53.0, 17.0, 31.0));
                float sizeOffset = nSize * nSize * 0.38; // phi tuyen: phan lon cham vua, mot so rat to

                float rawMask = n1 * 0.75 + n2 * 0.25;
                float localThreshold = _SpotThreshold - sizeOffset;
                float spotMask = smoothstep(localThreshold - _SpotSoftness, localThreshold + _SpotSoftness, rawMask);

                float selectorAB = saturate(n2 * 1.3 - 0.15);
                float3 spotAB = lerp(_SpotColorA.rgb, _SpotColorB.rgb, selectorAB);
                float selectorC = smoothstep(0.62, 0.86, n3);
                float3 spotColor = lerp(spotAB, _SpotColorC.rgb, selectorC * 0.7);

                float dhdx = ddx(spotMask);
                float dhdy = ddy(spotMask);
                float3 dpdx = ddx(IN.objectPos);
                float3 dpdy = ddy(IN.objectPos);
                float3 grad = dhdx * cross(N, dpdy) + dhdy * cross(dpdx, N);
                float3 bumpedN = normalize(N - grad * _BumpStrength);

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float NdotL = saturate(dot(bumpedN, L));

                float3 albedo = lerp(_BaseColor.rgb, spotColor, spotMask);

                float3 diffuse = albedo * mainLight.color * (0.25 + 0.75 * NdotL);

                float3 H = normalize(L + V);
                float NdotH = saturate(dot(bumpedN, H));
                float specPow = lerp(8.0, 96.0, _Smoothness);
                float spec = pow(NdotH, specPow) * _SpecularStrength;
                float3 specular = spec * mainLight.color;

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, IN.worldPos);
                    float3 L2 = normalize(light.direction);
                    float NdotL2 = saturate(dot(bumpedN, L2));
                    diffuse += albedo * light.color * (NdotL2 * 0.35);
                }
                #endif

                // Ambient = SH (Spherical Harmonics) cua URP + floor toi thieu _AmbientStrength
                // Dam bao dom mau luon hien du trong scene toi hay sang
                half3 sh = SampleSH(bumpedN);
                half3 shAmbient = max(sh, _AmbientStrength);
                float3 ambient = albedo * shAmbient;
                float3 color = diffuse + specular + ambient;
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}