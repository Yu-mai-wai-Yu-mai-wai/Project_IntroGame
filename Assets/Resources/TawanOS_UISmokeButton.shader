// A button drawn as a ring of drifting smoke: wispy strands trace a pill shape around the button, trail off
// to the sides and slowly flow, with a faint haze inside. Drawn on a quad bigger than the button so the
// wisps can spill past its edge. Colour comes from the vertex colour (Image.color), so hover / disabled is
// just a colour change. Lives in Resources so builds include it; loaded by UiSmokeButton.
// _Inner = the button's rectangle inside the quad, in quad UV (xMin, yMin, xMax, yMax).
// _Aspect = quad width / height. _Seed keeps every button's smoke different.
Shader "TawanOS/UI/SmokeButton"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Inner ("Button Rect In Quad (xMin, yMin, xMax, yMax)", Vector) = (0.2, 0.25, 0.8, 0.75)
        _Aspect ("Quad Aspect", Float) = 4
        _Seed ("Seed", Float) = 0
        _Speed ("Drift Speed", Float) = 1
        _RingWidth ("Ring Width", Range(0.01, 0.3)) = 0.06
        _Fill ("Inner Haze", Range(0, 1)) = 0.04

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _Inner;
            float _Aspect;
            float _Seed;
            float _Speed;
            float _RingWidth;
            float _Fill;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
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
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
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

            // Thin bright lines where the noise crosses its middle: smoke strands
            float strands(float2 p)
            {
                float r = 1.0 - abs(fbm(p) * 2.0 - 1.0);
                r = r * r * r;
                return r * r * r; // ^9: only the thinnest crests survive
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = IN.texcoord;

                // Quad space, height = 1, centred on the button
                float2 centre = (_Inner.xy + _Inner.zw) * 0.5;
                float2 p = (uv - centre) * float2(_Aspect, 1.0);
                float hx = (_Inner.z - _Inner.x) * 0.5 * _Aspect;
                float hy = (_Inner.w - _Inner.y) * 0.5;

                // Slow domain warp: the whole shape wavers like smoke
                float2 s = float2(_Seed * 7.13, _Seed * 3.71);
                float2 warp = float2(fbm(p * 2.2 + s + float2(t * 0.25, -t * 0.08)),
                                     fbm(p * 2.2 + s + float2(5.2 - t * 0.18, 1.3 + t * 0.12)));
                float2 pw = p + (warp - 0.5) * float2(0.45, 0.2);

                // Pill outline (capsule as tall as the button), a little narrower than the button so wisps reach its ends
                float r = hy * 0.95;
                float d = length(max(abs(pw) - float2(max(hx * 0.92 - r, 0.0), 0.0), 0.0)) - r;

                // Strands stretched along the pill, flowing sideways
                float2 sp = pw * float2(3.0, 10.0) + s + float2(-t * 0.35, t * 0.05);
                float lines = strands(sp) + 0.6 * strands(sp * 1.7 + float2(t * 0.2, 3.1));

                float ring = exp(-(d * d) / (_RingWidth * _RingWidth)); // gaussian: no long glow tails
                // Trails drifting off the two ends of the pill
                float trail = exp(-max(d, 0.0) / 0.35) * exp(-abs(pw.y + (warp.x - 0.5) * 0.4) / (hy * 0.6))
                            * smoothstep(hx * 0.55, hx * 1.05, abs(pw.x));
                float haze = saturate(-d / r) * _Fill * (0.6 + 0.8 * fbm(pw * 3.0 + s - t * 0.1));

                float inside = saturate(-d / r);
                float a = ring * (0.03 + lines * 1.3)                 // the rim
                        + trail * lines * 0.35 * saturate(d / 0.06)    // wisps off the ends, outside the rim only
                        + inside * lines * 0.06 + haze;                // a few faint strands across the dark middle

                // Fade out before the quad's edges
                float2 e = smoothstep(0.0, 0.12, uv) * smoothstep(0.0, 0.12, 1.0 - uv);
                a *= e.x * e.y;

                fixed4 color = IN.color;
                color.rgb *= 0.55 + 0.7 * saturate(ring * lines + 0.3);
                color.a *= saturate(a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
