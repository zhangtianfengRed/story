Shader "Custom/MemoryBarrier_Combined"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.8,0.9,1,1)
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 0.2

        _NoiseTex ("Noise Tex", 2D) = "gray" {}
        _NormalTex ("Normal Tex", 2D) = "bump" {}

        _Distortion ("Distortion", Range(0,0.1)) = 0.02

        _EdgePower ("Edge Power", Range(0.5,5)) = 2
        _EdgeIntensity ("Edge Intensity", Range(0,3)) = 1
        _EdgeLightBoost ("Edge Light Boost", Range(0,1)) = 0.2

        _MaskStrength ("Mask Strength", Range(0,1)) = 0.5

        _RippleCenter ("Ripple Center", Vector) = (0.5,0.5,0,0)
        _RippleRadius ("Ripple Radius", Range(0,2)) = 0
        _RippleWidth ("Ripple Width", Range(0.01,0.5)) = 0.15
        _RippleStrength ("Ripple Strength", Range(0,0.1)) = 0.04
        _RippleBrightness ("Ripple Brightness", Range(0,1)) = 0.3

        _NoiseSpeed1 ("Noise Speed1", Vector) = (0.1,0.1,0,0)
        _NoiseSpeed2 ("Noise Speed2", Vector) = (-0.2,0.15,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        GrabPass { "_GrabTex" }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _GrabTex;
            sampler2D _NoiseTex;
            sampler2D _NormalTex;

            float4 _TintColor;
            float _GlobalAlpha;
            float _Distortion;

            float _EdgePower;
            float _EdgeIntensity;
            float _EdgeLightBoost;

            float _MaskStrength;

            float4 _RippleCenter;
            float _RippleRadius;
            float _RippleWidth;
            float _RippleStrength;
            float _RippleBrightness;

            float4 _NoiseSpeed1;
            float4 _NoiseSpeed2;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grabPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                o.grabPos = ComputeGrabScreenPos(o.pos);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ===== Noise 双层 =====
                float2 uv1 = i.uv + _Time.y * _NoiseSpeed1.xy;
                float2 uv2 = i.uv + _Time.y * _NoiseSpeed2.xy;

                float2 noise1 = tex2D(_NoiseTex, uv1).rg * 2 - 1;
                float2 noise2 = tex2D(_NoiseTex, uv2).rg * 2 - 1;

                float2 noise = noise1 * 0.6 + noise2 * 0.4;

                float mask = saturate(lerp(0.6, 1.0, tex2D(_NoiseTex, uv1).r * _MaskStrength));

                // ===== Ripple =====
                float2 delta = i.uv - _RippleCenter.xy;
                float dist = length(delta);

                float ripple = smoothstep(_RippleRadius + _RippleWidth, _RippleRadius, dist) *
                               smoothstep(_RippleRadius - _RippleWidth, _RippleRadius, dist);

                float2 dir = dist > 0.0001 ? normalize(delta) : float2(0,0);

                // ===== Distortion =====
                float2 offset = noise * _Distortion * mask;
                offset += dir * ripple * _RippleStrength;

                float4 grabPos = i.grabPos;
                grabPos.xy += offset * grabPos.w;

                fixed4 col = tex2Dproj(_GrabTex, UNITY_PROJ_COORD(grabPos));

                // ===== Fresnel Edge =====
                float fresnel = pow(1 - saturate(dot(normalize(i.worldNormal), normalize(i.viewDir))), _EdgePower);
                float edge = fresnel * _EdgeIntensity;

                // ===== 亮度增强 =====
                col.rgb += edge * _EdgeLightBoost;
                col.rgb += ripple * _RippleBrightness;

                // ===== Alpha =====
                float alpha = saturate(
                    0.1 +
                    mask * 0.3 +
                    edge * 0.5 +
                    ripple * 0.6
                );

                alpha *= _GlobalAlpha;

                // ===== 颜色微染 =====
                col.rgb = lerp(col.rgb, col.rgb + _TintColor.rgb * 0.08, edge);

                return fixed4(col.rgb, alpha);
            }
            ENDCG
        }
    }
}