Shader "Custom/Mirror/PlanarReflection"
{
    Properties
    {
        _MainTex ("Reflection Texture", 2D) = "black" {}
        _ReflectionTex ("Reflection Texture", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.92, 0.97, 1, 1)
        _Brightness ("Brightness", Range(0.1, 2)) = 1
        _UseProjectedUV ("Use Projected UV", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ReflectionTex;
            sampler2D _MainTex;
            float4x4 _MirrorVP;
            fixed4 _Tint;
            float _Brightness;
            float _UseProjectedUV;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 mirrorClipPosition : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float4 worldPosition = mul(unity_ObjectToWorld, input.vertex);

                output.position = UnityObjectToClipPos(input.vertex);
                output.mirrorClipPosition = mul(_MirrorVP, worldPosition);
                output.uv = input.uv;

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.mirrorClipPosition.xy / input.mirrorClipPosition.w;
                uv = uv * 0.5 + 0.5;
                uv.y = 1.0 - uv.y;

                float2 directUv = float2(1.0 - input.uv.x, input.uv.y);
                float2 sampleUv = lerp(directUv, uv, step(0.5, _UseProjectedUV));

                fixed4 reflectedColor = tex2D(_ReflectionTex, sampleUv);
                return reflectedColor * _Tint * _Brightness;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Texture"
}
