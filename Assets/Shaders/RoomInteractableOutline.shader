Shader "Room/Interaction/Outline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 0.84, 0.12, 1)
        _OutlineWidth ("Outline Width", Float) = 2
        _OutlineDepthOffset ("Outline Depth Offset", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry+10"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineDepthOffset;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float4 clipPosition = UnityObjectToClipPos(v.vertex);
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 clipNormal = mul((float3x3) UNITY_MATRIX_VP, worldNormal);
                float2 outlineDirection = clipNormal.xy;
                float directionLength = max(length(outlineDirection), 1e-5);
                float2 offset = outlineDirection / directionLength;

                clipPosition.xy += offset / _ScreenParams.xy * _OutlineWidth * clipPosition.w * 2.0;

                #if defined(UNITY_REVERSED_Z)
                clipPosition.z -= _OutlineDepthOffset;
                #else
                clipPosition.z += _OutlineDepthOffset;
                #endif

                o.position = clipPosition;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }

    FallBack Off
}
