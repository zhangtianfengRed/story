Shader "Unlit/UnlitColorScroll"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _ScrollX ("Scroll X Speed", Float) = 0.2
        _ScrollY ("Scroll Y Speed", Float) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ScrollX;
            float _ScrollY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 1. 先应用面板上的 Tiling 和 Offset (确保 4, 5 这样的缩放生效)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // 2. 再叠加时间滚动
                float2 uvOffset = float2(_ScrollX, _ScrollY) * _Time.y;
                o.uv += uvOffset;

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 采样纹理并乘以面板上的 Main Color
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
