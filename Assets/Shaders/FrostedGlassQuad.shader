Shader "Custom/Quad/FrostedTransparent"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.88, 0.95, 1.0, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.72
        _BlurRadius ("Blur Radius", Range(0, 16)) = 6
        _FrostAmount ("Frost Amount", Range(0, 1)) = 0.75
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.25
        _Distortion ("Surface Distortion", Range(0, 0.04)) = 0.006
        _GrainScale ("Frost Grain Scale", Range(4, 160)) = 64
        _GrainStrength ("Frost Grain Strength", Range(0, 0.15)) = 0.035
        _EdgeSoftness ("Edge Softness", Range(0, 0.49)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        LOD 100

        GrabPass { "_FrostedGlassBackground" }

        Pass
        {
            Cull Off
            Lighting Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _FrostedGlassBackground;
            float4 _FrostedGlassBackground_TexelSize;

            fixed4 _TintColor;
            float _Opacity;
            float _BlurRadius;
            float _FrostAmount;
            float _TintStrength;
            float _Distortion;
            float _GrainScale;
            float _GrainStrength;
            float _EdgeSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grabPos : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            fixed4 SampleGrab(float4 grabPos, float2 uvOffset)
            {
                float4 samplePos = grabPos;
                samplePos.xy += uvOffset * samplePos.w;
                return tex2Dproj(_FrostedGlassBackground, UNITY_PROJ_COORD(samplePos));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = saturate(v.uv);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float frost = saturate(_FrostAmount);
                float blur = max(_BlurRadius, 0.0) * frost;
                float2 texel = _FrostedGlassBackground_TexelSize.xy * blur;

                float coarseNoiseA = ValueNoise(i.uv * _GrainScale + _Time.y * 0.11);
                float coarseNoiseB = ValueNoise(i.uv * (_GrainScale * 0.83) + float2(19.7, 43.1) - _Time.y * 0.07);
                float2 distortion = (float2(coarseNoiseA, coarseNoiseB) - 0.5) * (_Distortion * frost);

                fixed4 blurred = 0;
                blurred += SampleGrab(i.grabPos, distortion) * 0.16;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2( 1.0,  0.0)) * 0.08;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2(-1.0,  0.0)) * 0.08;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2( 0.0,  1.0)) * 0.08;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2( 0.0, -1.0)) * 0.08;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2( 0.72,  0.72)) * 0.07;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2(-0.72,  0.72)) * 0.07;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2( 0.72, -0.72)) * 0.07;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2(-0.72, -0.72)) * 0.07;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2( 1.45,  0.55)) * 0.06;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2(-1.45, -0.55)) * 0.06;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2( 0.55, -1.45)) * 0.06;
                blurred += SampleGrab(i.grabPos, distortion + texel * float2(-0.55,  1.45)) * 0.06;

                float luma = dot(blurred.rgb, float3(0.299, 0.587, 0.114));
                float3 frosted = lerp(blurred.rgb, luma.xxx, frost * 0.28);
                frosted = lerp(frosted, _TintColor.rgb, saturate(_TintStrength) * frost);

                float fineGrain = Hash21(i.uv * (_GrainScale * 9.7) + _Time.y) - 0.5;
                frosted += fineGrain * _GrainStrength * frost;
                frosted += frost * 0.035;

                float edgeDistance = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));
                float edgeFade = smoothstep(0.0, max(_EdgeSoftness, 0.0001), edgeDistance);
                float alpha = saturate(_Opacity * _TintColor.a * edgeFade);

                return fixed4(saturate(frosted), alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
