Shader "AttackSkill/Enemy/DeathGold"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Gold Tint", Color) = (0.92, 0.72, 0.28, 1)
        _RimColor ("Rim Color", Color) = (1.0, 0.88, 0.5, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.2
        _RimIntensity ("Rim Intensity", Range(0, 5)) = 0.85
        _Emission ("Emission", Range(0, 4)) = 0.28
        _Alpha ("Alpha", Range(0, 1)) = 0.26
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float _Emission;
            float _Alpha;

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
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = mul((float3x3)unity_ObjectToWorld, v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float fresnel = pow(1.0 - saturate(dot(n, v)), _RimPower);

                float lum = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                // 整体压暗：残影偏透、不抢画面
                fixed3 baseCol = lerp(_Color.rgb * 0.35, _Color.rgb * 0.7, saturate(lum + 0.15));
                baseCol *= tex.rgb * 0.25 + 0.45;

                fixed3 rim = _RimColor.rgb * fresnel * _RimIntensity * 0.65;
                fixed3 emit = _Color.rgb * _Emission * (0.12 + fresnel * 0.4);
                fixed3 rgb = baseCol + rim + emit;

                float a = saturate(_Alpha) * _Color.a * max(tex.a, 0.35);
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}
