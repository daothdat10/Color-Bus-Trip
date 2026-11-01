Shader "Bus/Winter Road Exact"
{
    Properties
    {
        _MainTex ("Winter Texture / Gradient", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _DarkTint ("Dark Tint", Color) = (0.31,0.54,0.70,1)
        _LightTint ("Light Tint", Color) = (0.62,0.76,0.84,1)
        _Mode ("0: snow, 1: road", Float) = 0
        _Lift ("Road Lift", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Off ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Tint, _DarkTint, _LightTint;
            float _Mode, _Lift;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) {
                v2f o; v.vertex.y += _Lift;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                return o;
            }
            fixed4 frag(v2f i):SV_Target {
                fixed4 tex=tex2D(_MainTex,i.uv);
                fixed lum=dot(tex.rgb,fixed3(.299,.587,.114));
                fixed3 snow=lerp(tex.rgb, fixed3(0.94,0.98,1.0), .35)*_Tint.rgb;
                fixed roadT=saturate((lum-.25)/.65);
                fixed3 road=lerp(_DarkTint.rgb,_LightTint.rgb,roadT);
                fixed3 col=lerp(snow,road,saturate(_Mode));
                return fixed4(col,1);
            }
            ENDCG
        }
    }
}