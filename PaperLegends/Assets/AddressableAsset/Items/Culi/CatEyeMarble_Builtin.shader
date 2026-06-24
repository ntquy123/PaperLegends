Shader "Custom/OilGem_Builtin_Mobile_Opaque"
{
    Properties
    {
        _BaseColor ("Base Color (Tint)", Color) = (0.95,0.65,0.30,1.0)
        _Smoothness ("Specular Shininess", Range(8,256)) = 96

        _InnerColor ("Inner Glow Color", Color) = (1.0,0.75,0.45,1)
        _InnerIntensity ("Inner Glow Intensity", Range(0,3)) = 1.0
        _InnerRadius ("Inner Radius", Range(0,1)) = 0.55
        _InnerSoftness ("Inner Softness", Range(0.01,1)) = 0.25

        _FleckColor ("Fleck Color", Color) = (1,0.9,0.7,1)
        _FleckIntensity ("Fleck Intensity", Range(0,2)) = 0.35
        _FleckScale ("Fleck Scale", Range(5,200)) = 60
        _FleckDensity ("Fleck Density", Range(0.9,1.0)) = 0.985
        _FleckTwinkle ("Twinkle Speed", Range(0,10)) = 1.5

        _FresnelPower ("Fresnel Power", Range(0.1,8)) = 2.8
        _FresnelStrength ("Fresnel Strength", Range(0,1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 150
        ZWrite On
        Cull Back

        Pass
        {
            // Quan trọng: ForwardBase để có _LightColor0, _WorldSpaceLightPos0
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"   // <-- thêm dòng này để có _LightColor0

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDir  : TEXCOORD1;
                float3 objPos   : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            fixed4 _BaseColor;
            half   _Smoothness;

            fixed4 _InnerColor;
            half   _InnerIntensity, _InnerRadius, _InnerSoftness;

            fixed4 _FleckColor;
            half   _FleckIntensity, _FleckScale, _FleckDensity, _FleckTwinkle;

            half   _FresnelPower, _FresnelStrength;

            float hash21(float2 p){
                p = frac(p*float2(123.34,456.21));
                p += dot(p, p+34.345);
                return frac(p.x*p.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.normalWS = normalize(UnityObjectToWorldNormal(v.normal));
                o.viewDir  = normalize(WorldSpaceViewDir(v.vertex));
                o.objPos   = v.vertex.xyz;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDir);

                // Main directional light (Built‑in)
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float  NdotL = saturate(dot(N, L));
                float3 H = normalize(L + V);

                // Specular Blinn‑Phong
                float spec = pow(saturate(dot(N,H)), _Smoothness) * NdotL;
                float3 specCol = spec * _LightColor0.rgb;   // <-- giờ đã có

                // Inner spherical glow
                float3 p = normalize(i.objPos); // mesh sphere là đẹp nhất
                float inner = 1.0 - smoothstep(_InnerRadius, _InnerRadius + _InnerSoftness, length(p));
                float3 innerCol = _InnerColor.rgb * inner * _InnerIntensity;

                // Oil flecks (sparkles)
                float2 fleckUV = i.worldPos.xz * _FleckScale;
                float  rnd = hash21(fleckUV);
                float  tw  = frac(rnd + _Time.y * _FleckTwinkle);
                float  fleck = step(_FleckDensity, rnd) * step(0.95, tw);
                fleck *= (1.0 - saturate(dot(N,V))) * 1.2;
                float3 fleckCol = _FleckColor.rgb * fleck * _FleckIntensity;

                // Fresnel rim
                float fres = pow(1.0 - saturate(dot(N,V)), _FresnelPower) * _FresnelStrength;

                float3 baseCol = _BaseColor.rgb * (0.2 + 0.1 * NdotL);
                float3 color = baseCol + specCol + innerCol + fleckCol + fres*(0.7 + 0.3*_BaseColor.rgb);

                // nhân màu đèn chính
                color *= _LightColor0.rgb;

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
