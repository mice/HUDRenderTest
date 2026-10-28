Shader "UIData/DamageText"
{
    Properties { _DamageTextAtlas("Digit atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "DamageTextAnimation.cginc"
            sampler2D _DamageTextAtlas;
            struct appdata
            {
                float2 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 anchor : TEXCOORD1;
                float4 motion : TEXCOORD2;
                float4 animation : TEXCOORD3;
            };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(appdata input)
            {
                v2f output;
                float opacity;
                output.position = DamageTextProject(input.position, input.anchor, input.motion, input.animation, opacity);
                output.uv = input.uv; output.color = input.color; output.color.a *= opacity;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                return fixed4(input.color.rgb, tex2D(_DamageTextAtlas, input.uv).a * input.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
