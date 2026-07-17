Shader "Story/City Walk/Background Silhouette"
{
    Properties
    {
        _LitColor ("Soft Lit Color", Color) = (0.30, 0.285, 0.32, 1)
        _ShadowColor ("Soft Shadow Color", Color) = (0.18, 0.155, 0.21, 1)
        _LightWrap ("Wrapped Light", Range(0, 1)) = 0.45
        _LightSoftness ("Light Softness", Range(0.05, 1)) = 0.72
        _SceneLightInfluence ("Scene Light Color Influence", Range(0, 1)) = 0.18
        _ShadowInfluence ("Scene Shadow Influence", Range(0, 1)) = 0.20
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry+20"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _LitColor;
            fixed4 _ShadowColor;
            half _LightWrap;
            half _LightSoftness;
            half _SceneLightInfluence;
            half _ShadowInfluence;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                half3 worldNormal : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                SHADOW_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 worldNormal = normalize(i.worldNormal);
                half3 lightDirection = normalize(
                    UnityWorldSpaceLightDir(i.worldPosition));

                half normalLight = dot(worldNormal, lightDirection);
                half wrappedLight = saturate(
                    (normalLight + _LightWrap) / (1.0h + _LightWrap));

                half halfSoftness = max(_LightSoftness * 0.5h, 0.025h);
                half softLight = smoothstep(
                    0.5h - halfSoftness,
                    0.5h + halfSoftness,
                    wrappedLight);

                half shadowAttenuation = SHADOW_ATTENUATION(i);
                softLight *= lerp(
                    1.0h,
                    shadowAttenuation,
                    saturate(_ShadowInfluence));

                fixed3 matteColor = lerp(
                    _ShadowColor.rgb,
                    _LitColor.rgb,
                    softLight);

                fixed3 sceneLightTint = saturate(_LightColor0.rgb);
                matteColor *= lerp(
                    fixed3(1.0, 1.0, 1.0),
                    sceneLightTint,
                    saturate(_SceneLightInfluence));

                UNITY_APPLY_FOG(i.fogCoord, matteColor);
                return fixed4(matteColor, 1.0);
            }
            ENDCG
        }

        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster
            #pragma target 3.0
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            struct shadowV2f
            {
                V2F_SHADOW_CASTER;
            };

            shadowV2f vertShadowCaster(appdata_base v)
            {
                shadowV2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 fragShadowCaster(shadowV2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    FallBack Off
}
