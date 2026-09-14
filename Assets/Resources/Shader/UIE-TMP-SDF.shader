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
        _MainTex1("Texture 1", any) = "" {}
        _MainTex2("Texture 2", any) = "" {}
        _MainTex3("Texture 3", any) = "" {}
        _MainTex4("Texture 4", any) = "" {}
        _MainTex5("Texture 5", any) = "" {}
        _MainTex6("Texture 6", any) = "" {}
        _MainTex7("Texture 7", any) = "" {}
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
            #pragma multi_compile _ HUD_8_TEX_SLOTS
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MainTex1;
            float4 _MainTex1_ST;
            sampler2D _MainTex2;
            float4 _MainTex2_ST;
            sampler2D _MainTex3;
            float4 _MainTex3_ST;

            #ifdef HUD_8_TEX_SLOTS
            sampler2D _MainTex4;
            float4 _MainTex4_ST;
            sampler2D _MainTex5;
            float4 _MainTex5_ST;
            sampler2D _MainTex6;
            float4 _MainTex6_ST;
            sampler2D _MainTex7;
            float4 _MainTex7_ST;
            #endif

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
                float3 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float isText : TEXCOORD1;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.z = v.texcoord.z;
                o.isText = v.texcoord.w;
                o.color = v.color;

                if (v.texcoord.w == 1)
                {
                    o.uv.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                    return o;
                }

                switch (v.texcoord.z)
                {
                case 1:
                    o.uv.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex1);
                    break;
                case 2:
                    o.uv.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex2);
                    break;
                case 3:
                    o.uv.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex3);
                    break;
                #ifdef HUD_8_TEX_SLOTS
                case 4:
                    o.uv.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex4);
                    break;
                case 5:
                    o.uv.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex5);
                    break;
                case 6:
                    o.uv.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex6);
                    break;
                case 7:
                    o.uv.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex7);
                    break;
                #endif
                default:
                    o.uv.xy = float2(0, 0);
                    break;
                }
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (i.isText != 1)
                {
                    fixed4 image = fixed4(1, 1, 1, 1);
                    switch (i.uv.z)
                    {
                    case 1:
                        image = tex2D(_MainTex1, i.uv.xy);
                        break;
                    case 2:
                        image = tex2D(_MainTex2, i.uv.xy);
                        break;
                    case 3:
                        image = tex2D(_MainTex3, i.uv.xy);
                        break;
                    #ifdef HUD_8_TEX_SLOTS
                    case 4:
                        image = tex2D(_MainTex4, i.uv.xy);
                        break;
                    case 5:
                        image = tex2D(_MainTex5, i.uv.xy);
                        break;
                    case 6:
                        image = tex2D(_MainTex6, i.uv.xy);
                        break;
                    case 7:
                        image = tex2D(_MainTex7, i.uv.xy);
                        break;
                    #endif
                    }
                    return image * i.color;
                }

                float distance = tex2D(_MainTex, i.uv.xy).a;
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
