Shader "Hidden/Room/FrostedScreenBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 1
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _BlurSize;
                fixed4 color = fixed4(0, 0, 0, 0);

                color += tex2D(_MainTex, i.uv + offset * float2(-1, -1));
                color += tex2D(_MainTex, i.uv + offset * float2( 0, -1));
                color += tex2D(_MainTex, i.uv + offset * float2( 1, -1));
                color += tex2D(_MainTex, i.uv + offset * float2(-1,  0));
                color += tex2D(_MainTex, i.uv);
                color += tex2D(_MainTex, i.uv + offset * float2( 1,  0));
                color += tex2D(_MainTex, i.uv + offset * float2(-1,  1));
                color += tex2D(_MainTex, i.uv + offset * float2( 0,  1));
                color += tex2D(_MainTex, i.uv + offset * float2( 1,  1));

                return color / 9.0;
            }
            ENDCG
        }
    }

    Fallback Off
}
