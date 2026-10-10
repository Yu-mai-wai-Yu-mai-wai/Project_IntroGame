// UI image that burns away from the card's bottom-right corner to its top-left corner.
// _CardRect is the card's rectangle in root-canvas space (xMin, yMin, xMax, yMax), so every image on
// the card (frame, artwork, card back) shares one fire front. _Burn goes 0 (whole) -> 1 (gone).
// Based on Unity's UI/Default so masks, RectMask2D and canvas tinting keep working.
Shader "TawanOS/UI/BurnDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Burn ("Burn Progress", Range(0, 1)) = 0
        _CardRect ("Card Rect (canvas space)", Vector) = (0, 0, 1, 1)
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 3
        _NoiseAmount ("Ragged Edge", Range(0, 1)) = 0.25
        _EdgeWidth ("Glow Width", Range(0.001, 0.5)) = 0.08
        [HDR] _GlowColor ("Glow Color", Color) = (1.6, 0.55, 0.12, 1)
        _CharColor ("Char Color", Color) = (0.12, 0.05, 0.03, 1)

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
            #pragma target 2.0

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
                float4 canvasPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Burn;
            float4 _CardRect;
            sampler2D _NoiseTex;
            float _NoiseScale;
            float _NoiseAmount;
            float _EdgeWidth;
            float4 _GlowColor;
            fixed4 _CharColor;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.canvasPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Position on the card: u 0 = left .. 1 = right, v 0 = bottom .. 1 = top
                float2 cardUV = (IN.canvasPosition.xy - _CardRect.xy) / max(_CardRect.zw - _CardRect.xy, 0.0001);

                // 0 at the bottom-right corner, 1 at the top-left corner: the order the fire reaches each point
                float along = ((1.0 - cardUV.x) + cardUV.y) * 0.5;
                float noise = tex2D(_NoiseTex, cardUV * _NoiseScale).r;
                float value = along + (noise - 0.5) * _NoiseAmount;

                // The fire front sweeps from before the first point to past the last one
                float front = lerp(-_NoiseAmount * 0.5 - _EdgeWidth, 1.0 + _NoiseAmount * 0.5 + _EdgeWidth, _Burn);

                // Behind the front: gone. Just ahead of it: glowing, then charred, then the untouched card
                float edge = saturate((value - front) / _EdgeWidth);
                float charred = lerp(0.25, 1.0, smoothstep(0.35, 1.0, edge));
                half3 burnt = lerp(_CharColor.rgb, color.rgb, charred);
                color.rgb = lerp(_GlowColor.rgb, burnt, smoothstep(0.0, 0.35, edge));
                color.a *= smoothstep(0.0, 0.08, value - front + 0.02);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.canvasPosition.xy, _ClipRect);
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
