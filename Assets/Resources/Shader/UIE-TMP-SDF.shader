Shader "Hidden/UIE-TMP-SDF"
{
    Properties
    {
        _MainTex("Font Atlas", 2D) = "white" {}
        [HDR]_FaceColor("Face Color", Color) = (1,1,1,1)
        [HDR]_OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Range(0, 0.5)) = 0
        _FaceDilate("Face Dilate", Range(-0.5, 0.5)) = 0
        _Softness("Softness", Range(0.001, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Pass
        {
            ZTest Always Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _FaceColor;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _FaceDilate;
            float _Softness;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float distance = tex2D(_MainTex, i.uv).a;
                float faceEdge = 0.5 - _FaceDilate;
                float faceAlpha = smoothstep(faceEdge - _Softness, faceEdge + _Softness, distance);
                float outlineAlpha = smoothstep(faceEdge - _OutlineWidth - _Softness, faceEdge - _OutlineWidth + _Softness, distance);

                fixed4 face = _FaceColor * i.color;
                fixed4 outline = _OutlineColor;
                fixed4 color = lerp(outline, face, faceAlpha);
                color.a *= max(faceAlpha, outlineAlpha) * i.color.a;
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
