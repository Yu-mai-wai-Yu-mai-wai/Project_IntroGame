// A burning arrow lying on the combat table (Game Designer, 13 Oct): it points at whoever's turn it is. The shape is
// an arrowhead on a short shaft; its edges waver like flame, bright chevrons run along it toward the tip, and it glows
// (additive) against the dark wood. The quad's +V points where the arrow points. Lives in Resources so builds include
// it; loaded by TableTurnMarker.
Shader "TawanOS/FireArrow"
{
    Properties
    {
        _FlameColor ("Flame", Color) = (1, 0.55, 0.15, 1)
        _TipColor ("Hot", Color) = (1, 0.92, 0.6, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Speed ("Speed", Float) = 1
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
            float _Speed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += a * noise(p);
                    p = p * 2.03 + float2(17.1, 9.3);
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = i.uv;
                float x = abs(uv.x - 0.5) * 2.0;                 // 0 on the centre line, 1 at the quad's side
                float y = uv.y;                                  // 0 at the tail, 1 at the tip

                // Flames lick backward from the shape: the edge wavers with noise running from tip to tail
                float n = fbm(float2(uv.x * 6.0, uv.y * 4.0 + t * 2.2));
                float waver = (n - 0.5) * 0.22;

                // Arrowhead: full width at y = 0.5, narrowing to the tip at y = 0.95
                float headW = saturate((0.95 - y) / 0.45) * 0.95;
                // (outside its own stretch each part is -1, i.e. far outside the shape)
                float head = y >= 0.5 ? headW + waver - x : -1.0;
                // Shaft: a bar down the middle from the tail to the head, its tail burning ragged
                float shaftW = 0.32 + waver * 0.8;
                float shaft = y <= 0.55 ? (shaftW - x) - smoothstep(0.25, 0.0, y) * (0.5 - n) * 0.8 : -1.0;
                float d = max(head, shaft);
                float body = smoothstep(0.0, 0.06, d);

                // Bright chevrons running toward the tip
                float chev = frac((y - x * 0.45) * 3.0 - t * 1.4);
                float stripe = smoothstep(0.0, 0.12, chev) * (1.0 - smoothstep(0.25, 0.45, chev));
                float heat = saturate(d * 4.0) * 0.6 + stripe * 0.5 + (n - 0.5) * 0.3;
                fixed3 col = lerp(_FlameColor.rgb, _TipColor.rgb, saturate(heat));

                // Soft glow round the shape
                float glow = saturate(1.0 - (-d) / 0.25) * 0.35 * step(d, 0.0);
                float a = saturate(body + glow) * _Alpha;
                return fixed4(col * (1.0 + stripe * 0.4), a);
            }
        ENDCG
        }
    }
}
