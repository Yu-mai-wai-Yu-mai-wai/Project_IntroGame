// A solid panel whose left edge is smoke: the colour (Image.color, black for the shop menu) fills the quad,
// and its left border wavers and rolls, with wisps that rise on one layer and sink on another, so the line
// between the panel and what is behind it is never straight or still. Lives in Resources so builds include
// it; loaded by ShopShrineView.
// _Edge = how much of the quad's width (from its left side) the smoky border takes.
// _Aspect = quad width / height, so the smoke is round, not stretched.
Shader "TawanOS/UI/SmokeEdge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Edge ("Edge Width (quad UV)", Range(0.01, 0.6)) = 0.12
        _Aspect ("Quad Aspect", Float) = 1.3
        _Speed ("Drift Speed", Float) = 1
        _Scale ("Smoke Scale", Float) = 4
        _WispTint ("Wisp Colour (alpha = strength)", Color) = (0.34, 0.31, 0.30, 0.85)

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
            float4 _ClipRect;
            float _Edge;
            float _Aspect;
            float _Speed;
            float _Scale;
            fixed4 _WispTint;

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
                for (int i = 0; i < 5; i++)
                {
                    v += a * noise(p);
                    p = p * 2.03 + float2(17.1, 9.3);
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = IN.texcoord;
                float2 p = float2(uv.x * _Aspect, uv.y) * _Scale;

                // Two smoke layers: one rising, one sinking, each warped by the other so the edge rolls
                float rise = fbm(p + float2(0.0, -t * 0.22));
                float sink = fbm(p * 1.3 + float2(7.3, t * 0.15) + rise * 0.8);
                float smoke = (rise + sink) * 0.5;

                // Where the border sits, as a fraction of the edge zone: it bulges and recedes with the smoke
                float x = uv.x / _Edge;                         // 0 at the quad's left side, 1 at the end of the edge zone
                float border = 0.6 + (fbm(float2(uv.y * _Scale * 0.8, t * 0.12)) - 0.5) * 1.0;
                float body = smoothstep(border - 0.15, border + 0.15, x + (smoke - 0.5) * 1.4);

                // Wisps: thin crests of the smoke field, stretched upright so they read as rising and sinking strands,
                // lighter than the panel so they show against the dark scene, drifting out past the border
                float2 sp = float2(uv.x * _Aspect * _Scale * 2.2, uv.y * _Scale * 0.9);
                float up = 1.0 - abs(fbm(sp + float2(rise * 0.6, -t * 0.3)) * 2.0 - 1.0);
                float down = 1.0 - abs(fbm(sp * 1.4 + float2(4.1 + sink * 0.6, t * 0.22)) * 2.0 - 1.0);
                float strands = pow(saturate(up), 6.0) + 0.7 * pow(saturate(down), 6.0);
                float reach = smoothstep(-0.25, border, x) * (1.0 - body);
                float wisps = saturate(strands * reach * 1.2);

                fixed4 color = IN.color;
                color.rgb = lerp(color.rgb, _WispTint.rgb, wisps * (1.0 - body));
                color.a *= saturate(max(body, wisps * _WispTint.a));

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
