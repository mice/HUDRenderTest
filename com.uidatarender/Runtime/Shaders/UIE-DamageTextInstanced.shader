Shader "UIData/DamageTextInstanced"
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
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options forcemaxcount:128 assumeuniformscaling
            #include "UnityCG.cginc"
            #include "DamageTextAnimation.cginc"
            UNITY_INSTANCING_BUFFER_START(DamageTextInstances)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DamageLocalRect)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DamageUvRect)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DamageColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DamageAnchor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DamageMotion)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DamageAnimation)
            UNITY_INSTANCING_BUFFER_END(DamageTextInstances)
            sampler2D _DamageTextAtlas;
            struct appdata { float2 position : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(appdata input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 rect = UNITY_ACCESS_INSTANCED_PROP(DamageTextInstances, _DamageLocalRect);
                float4 uv = UNITY_ACCESS_INSTANCED_PROP(DamageTextInstances, _DamageUvRect);
                float4 anchor = UNITY_ACCESS_INSTANCED_PROP(DamageTextInstances, _DamageAnchor);
                float4 motion = UNITY_ACCESS_INSTANCED_PROP(DamageTextInstances, _DamageMotion);
                float4 animation = UNITY_ACCESS_INSTANCED_PROP(DamageTextInstances, _DamageAnimation);
                v2f output;
                float opacity;
                output.position = DamageTextProject(lerp(rect.xy, rect.zw, input.position), anchor, motion, animation, opacity);
                output.uv = lerp(uv.xy, uv.zw, input.position);
                output.color = UNITY_ACCESS_INSTANCED_PROP(DamageTextInstances, _DamageColor);
                output.color.a *= opacity;
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
