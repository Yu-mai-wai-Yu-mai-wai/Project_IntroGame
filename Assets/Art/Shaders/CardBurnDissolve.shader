// Burns a 3D board card away (sprites and the card block): from its centre out to its edges
// (_FromCentre = 1), or from its bottom-right corner to its top-left corner (_FromCentre = 0), or from its top
// edge down (_TopDown = 1, which played backwards makes a card appear in fire from the bottom up).
// The 3D counterpart of UIBurnDissolve.shader, driven by CardBurnEffect.
// _CardWorldToLocal (set from script) takes a world position into the card's local space, and _CardRect is
// the card face's rectangle there (xMin, yMin, xMax, yMax), so every part of the card shares one fire front.
// _Burn goes 0 (whole) -> 1 (gone).
Shader "TawanOS/CardBurnDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Burn ("Burn Progress", Range(0, 1)) = 0
        _CardRect ("Card Rect (card space)", Vector) = (-0.5, -0.5, 0.5, 0.5)
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 3
        _NoiseAmount ("Ragged Edge", Range(0, 1)) = 0.25
        _EdgeWidth ("Glow Width", Range(0.001, 0.5)) = 0.08
        [HDR] _GlowColor ("Glow Color", Color) = (1.6, 0.55, 0.12, 1)
        _CharColor ("Char Color", Color) = (0.12, 0.05, 0.03, 1)
        [Toggle] _FromCentre ("Burn From Centre", Float) = 1
        _FaceSize ("Face Size (world, set from script)", Vector) = (1, 1, 0, 0)
        [Toggle] _TopDown ("Burn From Top Edge", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 cardPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Burn;
            float4 _CardRect;
            float4x4 _CardWorldToLocal;
            sampler2D _NoiseTex;
            float _NoiseScale;
            float _NoiseAmount;
            float _EdgeWidth;
            float4 _GlowColor;
            fixed4 _CharColor;
            float _FromCentre;
            float4 _FaceSize;
            float _TopDown;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                OUT.cardPosition = mul(_CardWorldToLocal, world).xy;
                OUT.vertex = UnityWorldToClipPos(world.xyz);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                // Position on the card: u 0 = left .. 1 = right, v 0 = bottom .. 1 = top
                float2 cardUV = (IN.cardPosition - _CardRect.xy) / max(_CardRect.zw - _CardRect.xy, 0.0001);

                // The order the fire reaches each point, 0 first .. 1 last: from the centre, round in world units
                // (0 centre, 1 corners), or from the bottom-right corner (0) to the top-left corner (1)
                float2 fromCentre = (cardUV - 0.5) * _FaceSize.xy;
                float radial = length(fromCentre) / max(length(0.5 * _FaceSize.xy), 0.0001);
                float diagonal = ((1.0 - cardUV.x) + cardUV.y) * 0.5;
                float along = lerp(lerp(diagonal, radial, _FromCentre), 1.0 - cardUV.y, _TopDown);
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

                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
