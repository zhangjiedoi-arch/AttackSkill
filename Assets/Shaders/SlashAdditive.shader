Shader "AttackSkill/VFX/SlashAdditive"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.82, 0.35, 1)
        _Intensity ("Intensity", Range(0, 16)) = 6
        _CoreBoost ("Core Boost", Range(0, 8)) = 3.2
        _SoftEdge ("Soft Edge", Range(0.01, 0.5)) = 0.22
        _HeadSoft ("Head Soft", Range(0.01, 0.5)) = 0.12
        _TailSoft ("Tail Soft", Range(0.01, 0.5)) = 0.28
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _CoreBoost;
            float _SoftEdge;
            float _HeadSoft;
            float _TailSoft;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // u = 沿刀弧进度, v = 径向(0内缘~1外缘)
                float u = i.uv.x;
                float v = i.uv.y;

                float radial = 1.0 - saturate(abs(v * 2.0 - 1.0) / max(_SoftEdge * 2.0, 0.001));
                radial = smoothstep(0.0, 1.0, radial);

                float head = smoothstep(0.0, _HeadSoft, u);
                float tail = 1.0 - smoothstep(1.0 - _TailSoft, 1.0, u);
                float along = head * tail;

                float core = pow(radial, 1.6) * _CoreBoost;
                float mask = (radial + core) * along * i.color.a;
                fixed3 rgb = _Color.rgb * _Intensity * mask * i.color.rgb;
                return fixed4(rgb, mask);
            }
            ENDCG
        }
    }

    FallBack Off
}
