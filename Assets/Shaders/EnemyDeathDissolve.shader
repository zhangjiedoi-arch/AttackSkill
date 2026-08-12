Shader "AttackSkill/Enemy/DeathDissolve"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.82, 0.35, 1)
        _EdgeColor ("Edge Color", Color) = (1.0, 0.85, 0.35, 1)
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0.01, 0.4)) = 0.08
        _NoiseScale ("Noise Scale", Range(0.1, 20)) = 3.5
        _HeightBias ("Height Bias", Range(0, 1)) = 0.35
        _Emission ("Edge Emission", Range(0, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Cull Off
            ZWrite On
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EdgeColor;
            float _Dissolve;
            float _EdgeWidth;
            float _NoiseScale;
            float _HeightBias;
            float _Emission;

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
                float3 localPos : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 pos = v.vertex.xyz + v.normal * (_Dissolve * 0.04);
                o.pos = UnityObjectToClipPos(float4(pos, 1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.localPos = v.vertex.xyz;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float n = Noise(i.worldPos.xz * _NoiseScale + i.uv * _NoiseScale);
                n = lerp(n, Noise(i.worldPos.xy * _NoiseScale), 0.35);

                float h = saturate(i.localPos.y * 0.5 + 0.5);
                float threshold = saturate(_Dissolve + (1.0 - h) * _HeightBias * _Dissolve);

                float d = n - threshold;
                clip(d);

                float edge = 1.0 - saturate(d / max(_EdgeWidth, 0.001));
                fixed3 baseCol = tex.rgb * _Color.rgb;
                fixed3 rgb = baseCol + _EdgeColor.rgb * edge * _Emission;
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Transparent Cutout"
}
