Shader "Bus/TownSplineMask"
{
    SubShader
    {
        Tags { "Queue"="Geometry-130" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0
            Stencil { Ref 120 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 position : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(position);
            }
            float4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
