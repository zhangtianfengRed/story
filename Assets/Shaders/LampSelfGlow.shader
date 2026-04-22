Shader "Custom/LampSelfGlow"
{
    Properties
    {
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _MainTex ("Base Map", 2D) = "white" {}
        [HDR]_EmissionColor ("Emission Color", Color) = (1.6, 1.25, 0.7, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 2
        _SelfLitStrength ("Self Lit Strength", Range(0, 2)) = 0.55
        _Glossiness ("Smoothness", Range(0, 1)) = 0.25
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _EmissionMap;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_EmissionMap;
        };

        fixed4 _Color;
        fixed4 _EmissionColor;
        half _EmissionStrength;
        half _SelfLitStrength;
        half _Glossiness;
        half _Metallic;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedoSample = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            fixed3 emissionSample = tex2D(_EmissionMap, IN.uv_EmissionMap).rgb;

            o.Albedo = albedoSample.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = albedoSample.a;

            // Keep the model readable in dark areas while still supporting bloom through HDR emission.
            o.Emission = (albedoSample.rgb * _SelfLitStrength) +
                (emissionSample * _EmissionColor.rgb * _EmissionStrength);
        }
        ENDCG
    }

    FallBack "Standard"
}
