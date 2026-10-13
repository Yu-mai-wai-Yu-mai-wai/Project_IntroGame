// A glowing arrow lying on the combat table (Game Designer, 13 Oct): it points at whoever's turn it is. A plain
// arrowhead on a short shaft with clean, still edges (no waver or running stripes, Game Designer, 13 Oct), lighter
// toward its tip, with a soft glow round it (additive) against the dark wood. The quad's +V points where the arrow
// points. Lives in Resources so builds include it; loaded by TableTurnMarker.
Shader "TawanOS/FireArrow"
{
    Properties
    {
        _FlameColor ("Colour", Color) = (1, 0.55, 0.15, 1)
        _TipColor ("Tip Colour", Color) = (1, 0.92, 0.6, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _FlameColor;
            fixed4 _TipColor;
            float _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float x = abs(i.uv.x - 0.5) * 2.0;               // 0 on the centre line, 1 at the quad's side
                float y = i.uv.y;                                // 0 at the tail, 1 at the tip

                // Signed distance-like value: > 0 inside the arrow. Outside its own stretch each part is -1.
                // Arrowhead: full width at y = 0.5, narrowing to the tip at y = 0.95
                float head = y >= 0.5 ? saturate((0.95 - y) / 0.45) * 0.95 - x : -1.0;
                // Shaft: a bar down the middle from the tail (y = 0.08) into the head
                float shaft = (y >= 0.08 && y <= 0.55) ? 0.32 - x : -1.0;
                float d = max(head, shaft);

                float aa = max(fwidth(d), 0.002);
                float body = smoothstep(-aa, aa, d);
                fixed3 col = lerp(_FlameColor.rgb, _TipColor.rgb, saturate((y - 0.1) / 0.85));

                // Soft glow just round the shape
                float glow = (1.0 - smoothstep(0.0, 0.18, -d)) * 0.3 * (1.0 - body);
                return fixed4(col, saturate(body + glow) * _Alpha);
            }
        ENDCG
        }
    }
}
