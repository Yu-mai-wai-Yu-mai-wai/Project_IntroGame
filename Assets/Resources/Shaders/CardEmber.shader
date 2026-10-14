// An upgraded card smoulders (CardEmber): the fire of a dying card (CardBurnDissolve, same glow and char colours)
// held at its edge instead of eating it. Right along the card's own rounded outline the fire stands as a ragged
// line, mostly hugging the edge with bites reaching in here and there; the edge side of it is charred, the line
// glows and flickers, the char fades just ahead of it, and embers drift up from it. Everything
// stays inside the card's rounded shape; nothing is cut away. Works on a 3D quad and on a UI Image (its rect's
// UVs; vertex colour alpha scales it). Premultiplied: the char covers the card, the glow and embers add light.
Shader "TawanOS/CardEmber"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture (unused, for UI)", 2D) = "white" {}
        _Aspect ("Card Width / Height", Float) = 0.716
        _Pad ("Sheet Size / Card Size", Float) = 1
        _Depth ("Line Inset From The Edge", Range(0, 0.1)) = 0.008
        _Corner ("Corner Radius", Range(0, 0.2)) = 0.045

                _Ragged ("Ragged Bites (how far in)", Range(0, 0.15)) = 0.04
        _NoiseScale ("Noise Scale", Float) = 7
        _Seed ("Seed (another ragged pattern, same place)", Float) = 0
        _Surge ("Surge (extra reach in, for the upgrade flare; 0 = at rest)", Range(0, 0.4)) = 0
        _EdgeWidth ("Glow Width", Range(0.002, 0.06)) = 0.009
        [HDR] _GlowColor ("Glow Colour", Color) = (1.6, 0.55, 0.12, 1)
        _GlowBoost ("Glow Brightness (above 1 lights up Bloom)", Range(0, 6)) = 2.2
        _HaloWidth ("Soft Halo Width", Range(0, 0.1)) = 0.035
        _HaloStrength ("Soft Halo Strength", Range(0, 2)) = 0.7
        _CharColor ("Char Colour", Color) = (0.12, 0.05, 0.03, 1)
        _CharStrength ("Char Strength", Range(0, 1)) = 0.9
        _Flicker ("Flicker", Range(0, 1)) = 0.35
        _Creep ("Fire Creep Speed", Float) = 0.04

        _EmberDensity ("Ember Density", Float) = 22
        _EmberSpeed ("Ember Rise Speed", Float) = 0.12
        _EmberStrength ("Ember Strength", Range(0, 3)) = 1.2

        // UI masking (so a scroll view can clip it)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            float _Pad, _Depth, _Seed, _Surge, _GlowBoost, _HaloWidth, _HaloStrength;
            float _Aspect, _Corner, _Ragged, _NoiseScale, _EdgeWidth, _CharStrength, _Flicker, _Creep;
            float _EmberDensity, _EmberSpeed, _EmberStrength;
            float4 _GlowColor, _CharColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * Noise(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // The card's own rounded outline; inward = distance in from it
                float2 size = float2(_Aspect, 1.0);
                float2 p = (i.uv - 0.5) * size * _Pad;
                float2 q = abs(p) - (size * 0.5 - _Corner);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Corner;
                float inside = 1.0 - smoothstep(-0.003, 0.0, d);
                float inward = -d;

                // Where the fire stands: at the edge, bitten in raggedly here and there (never out past the edge),
                // creeping and flickering a little
                // The seed only shifts where in the noise the pattern is read from: same reach, different shape
                float2 np = p * _NoiseScale + float2(_Seed * 17.31, _Seed * -9.73);
                float n = Fbm(np * 1.8 + float2(0.0, _Time.y * _Creep));
                float bite = pow(saturate(n * 1.8 - 0.5), 1.3) + 0.35 * Fbm(np * 5.0 + 11.3);
                // _Surge pushes the whole ragged line further in (the upgrade flare), keeping its shape
                float front = _Depth + bite * (_Ragged + _Surge * 0.5) + _Surge;
                front *= 1.0 + (Noise(np * 0.5 + _Time.y * 0.8) - 0.5) * _Flicker * 0.4;
                float value = inward - front; // < 0 burnt (toward the outline), > 0 not reached yet

                // Charred behind the line, the char fading just ahead of it (as the dying card's char band)
                float behind = 1.0 - smoothstep(-0.004, 0.0, value);
                float ahead = (1.0 - smoothstep(0.0, _EdgeWidth * 3.0, value)) * step(0.0, value);
                float charA = saturate(behind * _CharStrength + ahead * 0.55) * inside;
                float3 col = _CharColor.rgb * charA * (0.75 + 0.5 * Noise(np * 3.0));

                // The glowing line, flickering
                float flicker = 1.0 - _Flicker + _Flicker * Noise(np * 1.5 + _Time.y * 2.3);
                float glow = (1.0 - smoothstep(0.0, _EdgeWidth, abs(value))) * flicker * inside;
                // ...with light spreading softly off it on both sides
                float halo = exp(-abs(value) / max(_HaloWidth * 0.35, 0.0001)) * _HaloStrength * flicker * inside;
                col += _GlowColor.rgb * (glow + halo) * _GlowBoost;

                // Embers drifting up from the burning band, inside the card
                float2 grid = float2(p.x, p.y - _Time.y * _EmberSpeed) * _EmberDensity;
                float2 cell = floor(grid);
                float rnd = Hash(cell);
                float2 inCell = frac(grid) - 0.5 - (float2(Hash(cell + 3.7), Hash(cell + 9.1)) - 0.5) * 0.6;
                float near = 1.0 - smoothstep(front, front + 0.12, inward);
                float twinkle = 0.6 + 0.4 * sin(_Time.y * (4.0 + rnd * 4.0) + rnd * 30.0);
                float ember = step(0.86, rnd) * saturate(1.0 - length(inCell) * 4.0) * near * twinkle * _EmberStrength * inside;
                col += _GlowColor.rgb * ember * _GlowBoost;

                float a = i.color.a;
                return float4(col * a, charA * a);
            }
            ENDCG
        }
    }
}
