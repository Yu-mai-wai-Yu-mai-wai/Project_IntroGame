// Black smoke for particles (DrawPitSmoke). The texture's alpha is how thick the smoke is and its red channel
// how thin it is (1 at faint wisp edges, 0 in dense smoke); the particle colour is the smoke's colour and
// _EdgeLight is added where it thins, so the curls read as lighter grey edges on black, like ink in water.
// _Tint multiplies the result and may go above 1: particle colours are clamped to 0..1, so this is how a
// layer (the red strands) gets bright enough for the scene's Bloom to make it glow.
Shader "TawanOS/DarkSmoke"
{
    Properties
    {
        _MainTex ("Smoke", 2D) = "white" {}
        _EdgeLight ("Edge Light", Color) = (0.11, 0.1, 0.11, 1)
        [HDR] _Tint ("Tint (above 1 glows)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

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
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _EdgeLight;
            half4 _Tint;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.color = v.color;
                OUT.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.uv);
                half3 rgb = (IN.color.rgb + _EdgeLight.rgb * tex.r) * _Tint.rgb;
                return half4(rgb, tex.a * IN.color.a);
            }
            ENDCG
        }
    }
}
