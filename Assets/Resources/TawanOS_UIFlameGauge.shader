// A กุศล / มลทิน gauge drawn entirely here (Game Designer, 13 Oct, after the drawn flame ring she sent): a thick ring
// round a solid black plate (the number sits on it), with live fire burning out from behind the ring, tallest at the
// top and low down the sides. The fire's edges are kept crisp so it never turns into a haze. Lives in Resources so
// builds include it; loaded by FlameGaugeHUD.
Shader "TawanOS/UI/FlameGauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlameColor ("Flame", Color) = (0.85, 0.2, 0.15, 1)
        _TipColor ("Flame Tip", Color) = (1, 0.55, 0.38, 1)
        _PlateColor ("Plate", Color) = (0, 0, 0, 1)
        _InnerRadius ("Ring Inner Radius", Float) = 0.245
        _OuterRadius ("Ring Outer Radius", Float) = 0.3
        _FlameScale ("Flame Height Scale", Float) = 1.25
        _Motion ("Motion (0 = still)", Range(0, 1)) = 1

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

            #define TONGUES 13

            // How far the fire reaches round the ring: angle in degrees clockwise from the top, height (quad units),
            // half width in degrees. Tall at the top, middling up the sides, low at the bottom.
            static const float3 Tongue[TONGUES] =
            {
                float3(-14.0, 0.150, 13.0),
                float3(  6.0, 0.185, 14.0),
                float3( 26.0, 0.120, 11.0),
                float3( 52.0, 0.095, 12.0),
                float3( 84.0, 0.085, 13.0),
                float3(118.0, 0.065, 12.0),
                float3(150.0, 0.045, 11.0),
                float3(-40.0, 0.105, 11.0),
                float3(-70.0, 0.090, 12.0),
                float3(-104.0, 0.070, 12.0),
                float3(-138.0, 0.050, 11.0),
                float3(-168.0, 0.035, 10.0),
                float3(178.0, 0.030, 9.0),
            };

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
            fixed4 _FlameColor;
            fixed4 _TipColor;
            fixed4 _PlateColor;
            float _InnerRadius;
            float _OuterRadius;
            float _FlameScale;
            float _Motion;
            float4 _ClipRect;

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

            // Signed angle difference in degrees, -180..180
            float AngleDiff(float a, float b)
            {
                float d = a - b;
                return d - 360.0 * floor((d + 180.0) / 360.0);
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

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y * _Motion;
                float2 p = IN.texcoord - 0.5;
                float r = length(p);
                float aa = max(fwidth(r), 0.0015);

                // Burning fire behind the ring. The tongue table only sets how far the fire may reach at each angle
                // (tall at the top, low down the sides); inside that, rising noise breaks it into live, licking flames.
                // A sideways wobble makes the flames lean and waver as they rise.
                float2 q = p * 7.0;
                float2 pw = p + float2(fbm(q + float2(0.0, -t * 1.4)) - 0.5, 0.0) * 0.05;
                float rw = length(pw);
                float ang = degrees(atan2(pw.x, pw.y));                // 0 at the top, clockwise positive
                float reach = 0.02;
                for (int j = 0; j < TONGUES; j++)
                {
                    float3 tg = Tongue[j];
                    float k = AngleDiff(ang, tg.x) / (tg.z * 1.6);
                    reach = max(reach, tg.y * _FlameScale * exp(-k * k));
                }
                float d = (rw - _OuterRadius + 0.015) / reach;          // 0 at the ring, 1 where the fire may reach
                float f1 = fbm(pw * float2(16.0, 6.0) + float2(0.0, -t * 2.3));
                float f2 = fbm(pw * float2(30.0, 11.0) + float2(4.3, -t * 3.5));
                float f = f1 * 0.7 + f2 * 0.3;
                float heat = (1.0 - min(d, 3.0)) * 1.4 - 0.35 + (f - 0.5) * 1.6;   // beyond the reach it only cools, so no stray sparks
                heat *= step(_InnerRadius, rw);
                // A sharp threshold keeps the flame edges crisp instead of a soft haze
                float fire = smoothstep(0.0, 0.05, heat);
                fixed3 col = lerp(_FlameColor.rgb, _TipColor.rgb, smoothstep(0.1, 0.7, heat));
                float alpha = fire;

                // Ring band: one solid colour, the bright colour of the flames (Game Designer, 13 Oct), so ring and fire
                // read as one burning hoop. Its outer edge melts into the flames; its inner edge stays crisp against
                // the black plate.
                float band = smoothstep(_InnerRadius - aa, _InnerRadius + aa, r) * (1.0 - smoothstep(_OuterRadius - 0.012, _OuterRadius + 0.004, r));
                fixed3 ringCol = lerp(_FlameColor.rgb, _TipColor.rgb, 0.35);
                col = lerp(col, ringCol, band);
                alpha = max(alpha, band);

                // Solid black plate for the number
                float plate = 1.0 - smoothstep(_InnerRadius - aa, _InnerRadius + aa, r);
                col = lerp(col, _PlateColor.rgb, plate);
                alpha = max(alpha, plate * _PlateColor.a);

                fixed4 color = fixed4(col, alpha) * IN.color;

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
