Shader "Custom/Room/FogPlane"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0.86, 0.86, 0.9, 1)
        _AccentColor ("Accent Color", Color) = (0.76, 0.71, 0.88, 1)
        _AccentStrength ("Accent Strength", Range(0, 1)) = 0.32
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.35
        _FogDensity ("Fog Density", Range(0, 4)) = 1
        _AlphaMultiplier ("Alpha Multiplier", Range(0, 1)) = 1
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseTiling ("Noise Tiling", Float) = 1.6
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.25
        _NoiseScroll ("Noise Scroll", Vector) = (0.02, 0.01, 0, 0)
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.49)) = 0.18
        _FloatAmplitude ("Planar Warp Amount", Range(0, 0.15)) = 0.04
        _FloatFrequency ("Planar Flow Scale", Float) = 1
        _FloatSpeed ("Planar Flow Speed", Float) = 0.55
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.35
        _FlowEnabled ("Enable Flow", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 edgeUV : TEXCOORD0;
                float2 noiseUV : TEXCOORD1;
                float2 noiseUV2 : TEXCOORD2;
            };

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            fixed4 _Color;
            fixed4 _AccentColor;
            float _AccentStrength;
            float _BaseAlpha;
            float _FogDensity;
            float _AlphaMultiplier;
            float _NoiseTiling;
            float _NoiseStrength;
            float4 _NoiseScroll;
            float _EdgeSoftness;
            float _FloatAmplitude;
            float _FloatFrequency;
            float _FloatSpeed;
            float _FlowStrength;
            float _FlowEnabled;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.edgeUV = saturate(v.uv);

                float2 baseUV = TRANSFORM_TEX(v.uv, _NoiseTex);
                float flowScale = max(_FloatFrequency, 0.01);
                o.noiseUV = baseUV * (_NoiseTiling * flowScale);
                o.noiseUV2 = baseUV * (_NoiseTiling * (flowScale * 1.73));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float edgeDistance = min(
                    min(i.edgeUV.x, 1.0 - i.edgeUV.x),
                    min(i.edgeUV.y, 1.0 - i.edgeUV.y));
                float edgeFade = smoothstep(0.0, max(_EdgeSoftness, 0.0001), edgeDistance);

                float flowEnabled = step(0.5, _FlowEnabled);
                float flowAmount = saturate(_FlowStrength) * flowEnabled;
                float timeA = _Time.y * (_FloatSpeed * 0.95 + 0.12) * flowEnabled;
                float timeB = _Time.y * (_FloatSpeed * 1.35 + 0.18) * flowEnabled;
                float2 dirA = normalize(float2(0.85, 0.52));
                float2 dirB = normalize(float2(-0.58, 0.81));
                float2 baseScrollA = _NoiseScroll.xy * (_Time.y * 1.15) + dirA * (timeA * 0.42);
                float2 baseScrollB = float2(-_NoiseScroll.y, _NoiseScroll.x) * (_Time.y * 0.95) + dirB * (timeB * 0.34);
                float2 warpSeedUV = i.noiseUV2 * 0.82 + baseScrollB * 0.75;
                float warpA = tex2D(_NoiseTex, warpSeedUV + float2(0.23, 0.61)).r * 2.0 - 1.0;
                float warpB = tex2D(_NoiseTex, warpSeedUV * 1.17 + float2(-0.41, 0.17)).r * 2.0 - 1.0;
                float2 drift = float2(warpA, warpB) * (_FloatAmplitude * (1.2 + flowAmount * 2.2) * flowEnabled);

                float2 sampleUV1 = i.noiseUV + baseScrollA + drift;
                float2 sampleUV2 = i.noiseUV2 - baseScrollB - drift * 1.35 + float2(0.17, 0.63);
                float noiseSampleA = tex2D(_NoiseTex, sampleUV1).r;
                float noiseSampleB = tex2D(_NoiseTex, sampleUV2).r;
                float combined = noiseSampleA * 0.58 + noiseSampleB * 0.42;
                float collision = 1.0 - abs(noiseSampleA - noiseSampleB);
                float plume = saturate(combined * 0.72 + collision * 0.28);
                float densityAmount = saturate(_NoiseStrength);

                float stepped = floor(plume * 4.0 + 0.001) / 3.0;
                float ridge = 1.0 - smoothstep(0.0, 0.12, abs(plume - 0.58));
                float billow = smoothstep(0.3, 0.78, plume);
                float pattern = saturate(
                    lerp(plume, stepped, (0.45 + densityAmount * 0.4) * flowAmount) * (0.4 + densityAmount * 0.25)
                    + ridge * (0.22 + densityAmount * 0.22)
                    + billow * 0.22);
                float visibleMask = smoothstep(0.28 - densityAmount * 0.12, 0.9 - densityAmount * 0.08, pattern);
                float colorNoise = tex2D(_NoiseTex, sampleUV1 * 0.72 + sampleUV2 * 0.28).r;
                float colorMask = smoothstep(0.3, 0.82, colorNoise * 0.45 + ridge * 0.55);

                float alpha = saturate(_BaseAlpha * _FogDensity * _AlphaMultiplier * edgeFade * lerp(0.28, 1.55 + densityAmount * 0.18, visibleMask));
                float accentBlend = colorMask * _AccentStrength * flowEnabled * (0.45 + flowAmount * 0.55);
                fixed3 gasColor = lerp(_Color.rgb, _AccentColor.rgb, saturate(accentBlend));
                fixed3 color = gasColor * lerp(0.88, 1.12, visibleMask);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
