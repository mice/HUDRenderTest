Shader "Hidden/UIE-AtlasBlit"
{
    Properties
    {
        _MainTex0("Texture", any) = "" {}
        _MainTex1("Texture", any) = "" {}
        _MainTex2("Texture", any) = "" {}
        _MainTex3("Texture", any) = "" {}
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            //Blend Off
            Blend SrcAlpha OneMinusSrcAlpha
            //Blend One OneMinusSrcAlpha // Ô¤³ËÍ¸Ã÷¶È

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            uniform sampler2D _MainTex0;
            uniform float4 _MainTex0_ST;

            uniform sampler2D _MainTex1;
            uniform float4 _MainTex1_ST;

            uniform sampler2D _MainTex2;
            uniform float4 _MainTex2_ST;

            uniform sampler2D _MainTex3;
            uniform float4 _MainTex3_ST;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 texcoord : TEXCOORD0;
                float4 tint : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                float4 tint : COLOR;
                float is_text:TEXCOORD1;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                switch (v.texcoord.z)
                {
                case 0:
                    o.texcoord.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex0);
                    break;
                case 1:
                    o.texcoord.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex1);
                    break;
                case 2:
                    o.texcoord.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex2);
                    break;
                case 3:
                    o.texcoord.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex3);
                    break;
                default:
                    o.texcoord.xy = float2(0, 0);
                    break;
                }
                o.texcoord.z = v.texcoord.z;
                o.is_text = v.texcoord.w;
                o.tint = v.tint;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = fixed4(1, 1, 1, 1);
                switch (i.texcoord.z)
                {
                case 0:
                    color = tex2D(_MainTex0, i.texcoord.xy);
                    break;
                case 1:
                    color = tex2D(_MainTex1, i.texcoord.xy);
                    break;
                case 2:
                    color = tex2D(_MainTex2, i.texcoord.xy);
                    break;
                case 3:
                    color = tex2D(_MainTex3, i.texcoord.xy);
                    break;
                }
                if(i.is_text==1){
                    return fixed4(i.tint.rgb,color.a* i.tint.a);
                }
                //font is Alpha8,hold no color;
                return color * i.tint;
            }
            ENDCG
        }
    }
    Fallback Off
}
