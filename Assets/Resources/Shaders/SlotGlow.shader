// A board slot the held card can go to, as light lying on the table (CardPlayController3D). By default a plain
// glowing rectangle; the frame, rounded corners, sweeping light band and breathing can be turned up.
// Drawn additively on a quad; everything comes from the quad's UVs, no texture.
// _Aspect (width / length of the quad, set from script) keeps the frame equally thick on all four sides.
Shader "TawanOS/SlotGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.6, 1.1, 0.4, 1)
        _Aspect ("Width / Length", Float) = 0.6
        _FrameStrength ("Frame Strength", Range(0, 2)) = 0
        _EdgeWidth ("Frame Width", Range(0.005, 0.2)) = 0.04
        _Softness ("Edge Softness", Range(0.002, 0.3)) = 0.004
        _Corner ("Corner Radius", Range(0, 0.3)) = 0
        _Fill ("Fill", Range(0, 1)) = 1
        _SweepSpeed ("Sweep Speed", Float) = 0.6
        _SweepStrength ("Sweep Strength", Range(0, 2)) = 0
        _Pulse ("Breathing", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _Color;
            float _FrameStrength, _Aspect, _EdgeWidth, _Softness, _Corner, _Fill, _SweepSpeed, _SweepStrength, _Pulse;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // In length units: x across (0.._Aspect), y along (0..1)
                float2 size = float2(_Aspect, 1.0);
                float2 p = (i.uv - 0.5) * size;
                float2 halfSize = size * 0.5 - _Softness;
                // Distance to the rounded rectangle's outline (negative inside)
                float2 q = abs(p) - (halfSize - _Corner);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Corner;

                float frame = 1.0 - smoothstep(_EdgeWidth * 0.5, _EdgeWidth * 0.5 + _Softness, abs(d));
                float inside = 1.0 - smoothstep(-_Softness, 0.0, d);

                // A band of light running along the slot, over and over
                float sweep = frac(i.uv.y - _Time.y * _SweepSpeed);
                float band = smoothstep(0.0, 0.25, sweep) * (1.0 - smoothstep(0.25, 0.35, sweep));

                float breathe = 1.0 - _Pulse + _Pulse * (0.5 + 0.5 * sin(_Time.y * 4.0));
                float a = (frame * _FrameStrength + inside * (_Fill + band * _SweepStrength * 0.35) + frame * band * _SweepStrength) * breathe;
                return float4(_Color.rgb, saturate(a * _Color.a));
            }
            ENDCG
        }
    }
}
