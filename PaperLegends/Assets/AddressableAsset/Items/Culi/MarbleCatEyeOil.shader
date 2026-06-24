Shader "Custom/MarbleCatEyeOil"
{
    Properties
    {
        _Color ("Màu dầu (Base Color)", Color) = (1.0, 0.8, 0.1, 0.8)
        _CoreColor ("Màu lõi mắt mèo (Core Color)", Color) = (0.9, 0.4, 0.0, 1.0)
        _CoreWidth ("Độ rộng mắt mèo", Range(1, 10)) = 4.0

        _Glossiness ("Độ bóng bề mặt (Smoothness)", Range(0,1)) = 0.95
        _Metallic ("Độ kim loại (Reflectivity)", Range(0,1)) = 0.3

        _SparkleIntensity ("Độ lấp lánh (Sparkle)", Range(0, 5)) = 2.0
        _SparkleDensity ("Mật độ hạt lấp lánh", Range(10, 200)) = 100.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half  _CoreWidth;
                half  _Glossiness;
                half  _Metallic;
                half  _SparkleIntensity;
                half  _SparkleDensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir     : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            // Hàm tạo nhiễu ngẫu nhiên (tạo hạt lấp lánh)
            float random(float3 pos)
            {
                return frac(sin(dot(pos, float3(12.9898, 78.233, 45.164))) * 43758.5453123);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.worldPos    = posInputs.positionWS;
                OUT.worldNormal = normInputs.normalWS;
                OUT.viewDir     = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.worldNormal);
                float3 V = normalize(IN.viewDir);

                // 1. MÀU CƠ BẢN
                half4 baseColor = _Color;

                // 2. LÕI MẮT MÈO: tâm (NdotV cao = nhìn thẳng) → màu lõi
                //    rìa (NdotV thấp = nhìn nghiêng) → màu dầu cơ sở
                float NdotV = saturate(dot(N, V));
                float coreEffect = pow(NdotV, _CoreWidth); // cao ở tâm, thấp ở rìa

                // 3. HIỆU ỨNG LẤP LÁNH: hạt sáng li ti ngẫu nhiên
                float noise   = random(IN.worldPos * _SparkleDensity);
                float sparkle = step(0.97, noise) * _SparkleIntensity;
                sparkle      *= NdotV;

                // 4. PHẢN CHIẾU ĐƠN GIẢN (Fresnel rim)
                float rim = pow(1.0 - NdotV, 3.0) * _Metallic;

                // 5. ÁNH SÁNG — ambient tối thiểu 0.55 để màu luôn hiện dù không có đèn
                Light mainLight = GetMainLight();
                float NdotL     = saturate(dot(N, normalize(mainLight.direction)));
                float3 lighting = mainLight.color * (NdotL * 0.7 + 0.3);
                lighting        = max(lighting, 0.55); // đảm bảo không tối đen

                // TỔNG HỢP: rìa = baseColor (dầu), tâm = CoreColor (lõi mắt mèo)
                half3 color = lerp(baseColor.rgb, _CoreColor.rgb, coreEffect);
                color       = color * lighting;
                color      += sparkle;
                color      += rim * baseColor.rgb;

                half alpha = baseColor.a;

                color = MixFog(color, IN.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }

    }

    FallBack "Universal Render Pipeline/Lit"
}