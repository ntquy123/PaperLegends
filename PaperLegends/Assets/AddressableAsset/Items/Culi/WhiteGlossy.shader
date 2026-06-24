Shader "Custom/WhiteGlossyURP"
{
    Properties
    {
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.9
        _MarbleTexture ("Marble Pattern", 2D) = "white" { }
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            // 1. Khai báo các hàm xử lý - Đây là chỗ bạn bị thiếu gây ra lỗi "Both vertex and fragment..."
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 2. Cấu trúc dữ liệu đầu vào từ Mesh
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            // 3. Cấu trúc dữ liệu truyền từ Vertex sang Fragment
            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            // Định nghĩa các biến thuộc tính
            TEXTURE2D(_MarbleTexture);
            SAMPLER(sampler_MarbleTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MarbleTexture_ST;
                float4 _Color;
                half _Glossiness;
            CBUFFER_END

            // 4. Vertex Shader
            Varyings vert(Attributes input)
            {
                Varyings output;
                // Chuyển đổi vị trí từ không gian Object sang Clip Space
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MarbleTexture);
                return output;
            }

            // 5. Fragment Shader
            half4 frag(Varyings input) : SV_Target
            {
                // Lấy màu từ Texture
                half4 texColor = SAMPLE_TEXTURE2D(_MarbleTexture, sampler_MarbleTexture, input.uv);
                
                // Kết hợp màu sắc và độ bóng
                half3 finalColor = texColor.rgb * _Color.rgb;
                
                // Trả về màu cuối cùng (Alpha cố định là 1.0 cho vật thể Opaque)
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}