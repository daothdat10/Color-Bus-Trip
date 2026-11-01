Shader "Bus/Passenger Stencil Mask"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry+6" "RenderType"="Opaque" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            ZTest Always
            Cull [_Cull]
            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp Always
                Pass Replace
            }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            fixed4 frag() : SV_Target { return 0; }
            ENDCG
        }
    }
    Fallback Off
}
