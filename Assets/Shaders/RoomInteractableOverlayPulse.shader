Shader "Room/Interaction/Overlay Pulse"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1, 0.92, 0.25, 1)
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.18
        _PulseAmplitude ("Pulse Amplitude", Range(0, 1)) = 0.28
        _PulseSpeed ("Pulse Speed", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "OverlayPulse"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TintColor;
            half _BaseAlpha;
            half _PulseAmplitude;
            float _PulseSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half pulse = 0.5h + 0.5h * sin(_Time.y * _PulseSpeed);
                half alpha = saturate(_BaseAlpha + pulse * _PulseAmplitude);
                return fixed4(_TintColor.rgb * alpha, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
