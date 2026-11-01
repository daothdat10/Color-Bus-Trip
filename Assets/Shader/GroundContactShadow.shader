Shader "Bus/GroundContactShadow"
{
    Properties
    {
        _OutsideShadowColor ("Outside Color (controlled by road material)", Color) = (0.55, 0.70, 0.29, 1)
        _InsideShadowColor ("Inside Color (controlled by road material)", Color) = (0.18, 0.28, 0.09, 1)
        _OutsideShadowStrength ("Outside Strength (controlled by road material)", Range(0, 1)) = 0
        _InsideShadowStrength ("Inside Strength (controlled by road material)", Range(0, 1)) = 0
        _OutsideShadowWidth ("Outside Width (controlled by road material)", Range(0.1, 1)) = 1
        _InsideShadowWidth ("Inside Width (controlled by road material)", Range(0.005, 0.5)) = 0.138
        _InsideShadowOffset ("Inside Offset (controlled by road material)", Range(0, 0.4)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;
            float4 _OutsideShadowColor;
            float4 _InsideShadowColor;
            float _OutsideShadowStrength;
            float _InsideShadowStrength;
            float _OutsideShadowWidth;
            float _InsideShadowWidth;
            float _InsideShadowOffset;

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fade : TEXCOORD1;
            };

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;
                output.position = mul(unity_MatrixVP,
                    mul(unity_ObjectToWorld, input.position));
                output.uv = input.uv;
                output.fade = 1.0 - saturate(input.color.r);
                return output;
            }

            float4 frag(VertexOutput input) : SV_Target
            {
                bool inside = input.uv.y < 0.5;
                float width = inside ? _InsideShadowWidth : _OutsideShadowWidth;
                float offset = inside ? _InsideShadowOffset : 0.0;
                float distance = abs(input.uv.x - offset);
                float edgeFade = 1.0 - smoothstep(width * 0.35,
                    max(width, 0.001), distance);
                float strength = inside ? _InsideShadowStrength : _OutsideShadowStrength;
                float3 color = inside ? _InsideShadowColor.rgb : _OutsideShadowColor.rgb;
                return float4(color,
                    saturate(strength) * input.fade * edgeFade);
            }
            ENDHLSL
        }
    }
}
