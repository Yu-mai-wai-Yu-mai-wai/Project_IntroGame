// Foil over an upgraded card (CardFoil): a faint sheen that drifts with time and shifts as the card turns
// toward or away from the camera (white, or rainbow with _Rainbow up), a bright gloss band sweeping across it,
// and twinkling sparkles. Drawn additively over the card's face, inside its rounded corners. Works on a 3D quad
// and on a UI Image (its rect's UVs; vertex colour alpha scales it). _Aspect = face width / height, so the
// corners stay round.
Shader "TawanOS/CardFoil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture (unused, for UI)", 2D) = "white" {}
        _Strength ("Strength", Range(0, 1)) = 0.32
        _Aspect ("Width / Height", Float) = 0.716
        _Corner ("Corner Radius", Range(0, 0.2)) = 0.045
        _Rainbow ("Rainbow (0 = white sheen)", Range(0, 1)) = 0
        _SheenStrength ("Sheen Strength", Range(0, 1)) = 0.35
        _HueScale ("Rainbow Size", Float) = 1.1
        _HueSpeed ("Rainbow Drift", Float) = 0.08
        _ViewShift ("Shift With Angle", Float) = 1.6
        _GlossSpeed ("Gloss Speed", Float) = 0.22
        _GlossStrength ("Gloss Strength", Range(0, 2)) = 0.9
        _SparkleDensity ("Sparkle Density", Float) = 26
        _SparkleStrength ("Sparkle Strength", Range(0, 3)) = 1.4

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
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float view : TEXCOORD1;
            };

            float _Rainbow, _SheenStrength;
            float _Strength, _Aspect, _Corner, _HueScale, _HueSpeed, _ViewShift;
            float _GlossSpeed, _GlossStrength, _SparkleDensity, _SparkleStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                // How the face is turned against the line of sight: changes as the card tilts, flies or the camera moves
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 toCam = normalize(_WorldSpaceCameraPos - world);
                float3 right = normalize(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
                float3 up = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
                o.view = dot(toCam, right) + dot(toCam, up);
                return o;
            }

            float3 Rainbow(float h)
            {
                return saturate(abs(frac(h + float3(0.0, 0.333, 0.667)) * 6.0 - 3.0) - 1.0);
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Inside the face's rounded rectangle
                float2 size = float2(_Aspect, 1.0);
                float2 p = (i.uv - 0.5) * size;
                float2 q = abs(p) - (size * 0.5 - _Corner);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Corner;
                float inside = 1.0 - smoothstep(-0.004, 0.0, d);

                // Bands running diagonally, drifting, and sliding along as the viewing angle changes
                float diag = i.uv.x * _Aspect + i.uv.y;
                float hue = diag * _HueScale + _Time.y * _HueSpeed + i.view * _ViewShift;
                float3 rainbow = Rainbow(hue);
                float bands = 0.55 + 0.45 * sin(diag * 18.0 + i.view * 6.0);

                // A gloss band sweeping across now and then
                float sweep = frac(diag * 0.5 - _Time.y * _GlossSpeed);
                float gloss = smoothstep(0.0, 0.06, sweep) * (1.0 - smoothstep(0.06, 0.14, sweep)) * _GlossStrength;

                // Sparkles: a few cells of a grid twinkle, each at its own pace
                float2 grid = i.uv * float2(_SparkleDensity * _Aspect, _SparkleDensity);
                float2 cell = floor(grid);
                float rnd = Hash(cell);
                float2 inCell = frac(grid) - 0.5;
                float twinkle = pow(saturate(sin(_Time.y * (2.0 + rnd * 3.0) + rnd * 40.0)), 16.0);
                float star = step(0.88, rnd) * twinkle * saturate(1.0 - length(inCell) * 3.0) * _SparkleStrength;

                float3 sheen = lerp(float3(1.0, 1.0, 1.0) * _SheenStrength, rainbow, _Rainbow) * bands;
                float3 col = sheen + gloss + star;
                float a = inside * _Strength * i.color.a;
                return float4(col, a);
            }
            ENDCG
        }
    }
}
