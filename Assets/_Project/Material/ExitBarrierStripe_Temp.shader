Shader "Bus/ExitBarrierStripe"
{
    Properties
    {
        _Orange ("Barrier Orange", Color) = (0.95, 0.19, 0.07, 1)
        _White ("Stripe White", Color) = (0.97, 0.98, 0.96, 1)
        _StripeScale ("Stripe Scale", Range(1, 24)) = 7
        _StripeWidth ("White Stripe Width", Range(0.05, 0.95)) = 0.38
        _Glossiness ("Smoothness", Range(0,1)) = 0.42
        _Metallic ("Metallic", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _Orange;
        fixed4 _White;
        half _StripeScale;
        half _StripeWidth;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 localPos = mul(unity_WorldToObject, float4(IN.worldPos, 1)).xyz;
            float phase = frac((localPos.x - localPos.y * 0.70) * _StripeScale);
            half whiteStripe = step(phase, _StripeWidth);
            fixed4 surfaceColor = lerp(_Orange, _White, whiteStripe);

            o.Albedo = surfaceColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = surfaceColor.a;
        }
        ENDCG
    }
    FallBack "Standard"
}