Shader "Custom/CatEyeMarble"
{
    Properties
    {
        _GlassColor ("Glass Tint", Color) = (1,1,1,0.2)
        _EyeColor ("Eye Color (Mắt Mèo)", Color) = (0, 0.8, 0.2, 1)
        _Thickness ("Eye Thickness", Range(0.01, 0.5)) = 0.1
        _Twist ("Twist Intensity", Range(0, 10)) = 3.0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassColor;
                float4 _EyeColor;
                float _Thickness;
                float _Twist;
                float _Glossiness;
                float _Metallic;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz; // Giữ tọa độ Local để tính toán lõi
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Tính toán độ xoắn của dải màu (Eye Vane)
                // Ma trận xoay dựa trên trục Y
                float angle = input.positionOS.y * _Twist;
                float s = sin(angle);
                float c = cos(angle);
                
                // Tọa độ đã xoay
                float rotatedX = input.positionOS.x * c - input.positionOS.z * s;
                
                // Khoảng cách từ điểm hiện tại đến mặt phẳng trung tâm (đã xoay)
                float dist = abs(rotatedX);
                
                // Tạo dải màu sắc nét nhưng có độ mềm ở biên
                float eyeMask = smoothstep(_Thickness, _Thickness - 0.05, dist);
                
                // Giới hạn dải màu chỉ nằm bên trong khối cầu (tránh tràn ra rìa quá mức)
                float sphereMask = smoothstep(0.5, 0.45, length(input.positionOS));
                eyeMask *= sphereMask;

                // 2. Kết hợp màu sắc
                half4 finalColor = lerp(_GlassColor, _EyeColor, eyeMask);
                
                // 3. Tính toán Lighting cơ bản (Specular)
                Light mainLight = GetMainLight();
                half3 viewDir = normalize(GetCameraPositionWS() - input.positionCS.xyz); // Đơn giản hóa
                half3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(max(dot(input.normalWS, halfDir), 0.0), 128.0) * _Glossiness;

                finalColor.rgb += spec * mainLight.color;
                finalColor.a = max(_GlassColor.a, eyeMask);

                return finalColor;
            }
            ENDHLSL
        }
    }
}