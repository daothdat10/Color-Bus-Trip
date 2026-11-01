Shader "Bus/Winter Road Visual"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Light Color", Color) = (1,1,1,1)
        _DarkTint ("Dark Color", Color) = (0.2,0.2,0.2,1)
        [Toggle] _GradientMode ("Use grayscale gradient", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _BaseColor;
            fixed4 _DarkTint;
            float _GradientMode;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed luminance = dot(tex.rgb, fixed3(0.299, 0.587, 0.114));
                fixed3 tintedGradient = lerp(_DarkTint.rgb, _BaseColor.rgb, luminance);
                fixed3 coloredTexture = tex.rgb * _BaseColor.rgb;
                fixed3 color = lerp(coloredTexture, tintedGradient, saturate(_GradientMode));
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}