// Reconstructed Town surface shading; the exported game shader has no original program.
Shader "Custom/TownToonSurface"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _DarkTint ("Texture Dark Tint", Color) = (0.2,0.2,0.2,1)
        _GroundPatternStrength ("Grass Pattern Strength", Range(0,1)) = 0.45
        _RoadGradientStrength ("Road Texture Gradient", Range(0,1)) = 0
        _UseVertexColor ("Use Vertex Color", Range(0,1)) = 0
        [Header(Authored Board Contact Shadow)]
        [NoScaleOffset] _ContactShadowMap ("Grass Distance Map (Fill Plane Only)", 2D) = "white" {}
        _ContactShadowColor ("Contact Shadow Color", Color) = (0.55,0.34,0.16,1)
        _ContactShadowStrength ("Contact Shadow Strength", Range(0,1)) = 0
        _ContactShadowWidth ("Contact Shadow Width (Board Local Units)", Range(0.001,0.08)) = 0.025
        [HideInInspector] _RibbonDistanceScale ("Ribbon Width In Board Units", Float) = 0.1
        [HideInInspector] _ContactJoinRepair ("Continue Bank Shadow At Join", Float) = 0
        [Header(Surface Lighting)]
        _LightDirection ("Fake Light Direction (Object Space)", Vector) = (0,5,-1,0)
        _SurfaceNormal ("Flat Surface Normal (Object Space)", Vector) = (0,1,0,0)
        _ShadowColor ("Shadow Color", Color) = (0.58,0.72,0.29,1)
        _Cutoff ("Shadow Cutoff", Range(0,1)) = 0.37
        _ShadowIntensity ("Shadow Intensity", Range(0,1)) = 0.826
        _ShadowSharpness ("Shadow Sharpness", Range(0.1,10)) = 6
        [Toggle] _EnableAOFresnel ("Enable Bevel AO", Float) = 1
        _AOFresnelIntensity ("AO Intensity", Range(0,1)) = 0.519
        _AOFresnelPower ("AO Power", Range(0.1,10)) = 1
        _AOFresnelShadowBoost ("Shadow Side AO Boost", Range(0,1)) = 0
        _AOFresnelTint ("AO Tint", Color) = (0,0,0,1)
        [Toggle] _EnableRim ("Enable Rim Light", Float) = 0
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.1,10)) = 2
        _RoadRimStrength ("Rim Strength", Range(0,1)) = 0.12
        [Toggle] _EnableSpecular ("Enable Specular", Float) = 0
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularIntensity ("Specular Intensity", Range(0,1)) = 0.5
        _SpecularSharpness ("Specular Sharpness", Range(1,128)) = 1
        [Enum(Add,0,Overlay,1,SoftLight,2)] _SpecBlendMode ("Specular Blend", Float) = 1
        [Toggle] _EnableTextureOutline ("Darken Bevel With Texture", Float) = 1
        _TextureOutlineDarkness ("Bevel Darkness", Range(0,1)) = 0.5
        [Header(Road Ribbon Only)]
        [Enum(AuthoredMesh,0,RoadRibbon,1,GrassRibbon,2)] _RibbonMode ("Spline Ribbon Shading", Float) = 0
        _RoadInsideShadowColor ("Road Edge Shadow Color", Color) = (0.65,0.45,0.23,1)
        _RoadInsideShadowStrength ("UV Edge Shadow Strength", Range(0,1)) = 0
        _RoadEdgeShadeWidth ("UV Edge Shadow Width", Range(0.001,0.5)) = 0.045
        _RoadInsideShadowOffset ("UV Edge Shadow Offset", Range(0,0.4)) = 0
        // Shared with RoadContactShadowLink, which renders the separate grass shadow mesh.
        _RoadEdgeShadeColor ("Outside Grass Shadow Color", Color) = (0.55,0.70,0.29,1)
        _RoadEdgeShadeStrength ("Outside Grass Shadow Strength", Range(0,1)) = 0
        _RoadOutsideShadowWidth ("Outside Grass Shadow Width", Range(0.1,1)) = 0.475
        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("Depth Write", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Float) = 0
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _ContactShadowMap;
            float4 _ContactShadowColor;
            float _ContactShadowStrength, _ContactShadowWidth, _RibbonDistanceScale;
            float _ContactJoinRepair;
            float4x4 _ContactWorldToBoard;
            float4 _MainTex_ST, _BaseColor, _DarkTint, _ShadowColor;
            float4 _LightDirection, _SurfaceNormal, _AOFresnelTint, _RimColor;
            float4 _SpecularColor, _RoadInsideShadowColor;
            float _GroundPatternStrength, _RoadGradientStrength, _UseVertexColor;
            float _Cutoff, _ShadowIntensity, _ShadowSharpness;
            float _EnableAOFresnel, _AOFresnelIntensity, _AOFresnelPower, _AOFresnelShadowBoost;
            float _EnableRim, _RimPower, _RoadRimStrength;
            float _EnableSpecular, _SpecularIntensity, _SpecularSharpness, _SpecBlendMode;
            float _EnableTextureOutline, _TextureOutlineDarkness;
            float _RoadInsideShadowStrength, _RoadEdgeShadeWidth, _RoadInsideShadowOffset, _RibbonMode;

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                float2 rawUV : TEXCOORD4;
                float4 color : COLOR;
            };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.normalOS = v.normal;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rawUV = v.uv;
                o.color = v.color;
                return o;
            }
            float4 frag(Varyings i) : SV_Target
            {
                float4 texel = tex2D(_MainTex, i.uv);
                float luminance = dot(texel.rgb, float3(0.299,0.587,0.114));
                float3 color = _BaseColor.rgb * (1 + (luminance - 0.9) * _GroundPatternStrength);
                // Preserve the authored UV gradient without turning the whole road dark.
                color = lerp(color, lerp(_DarkTint.rgb, _BaseColor.rgb, luminance),
                    saturate(_RoadGradientStrength));
                color *= lerp(float3(1,1,1), i.color.rgb, _UseVertexColor);

                float3 nOS = normalize(i.normalOS);
                float3 n = normalize(i.normalWS);
                float3 surface = normalize(_SurfaceNormal.xyz);
                float3 lightOS = normalize(_LightDirection.xyz + float3(0,0.0001,0));
                // A flat top retains the saved base colour. Rolled edges receive shading.
                float bevel = 1 - saturate(abs(dot(nOS, surface)));
                float lightFacing = dot(nOS, lightOS);
                float halfWidth = 0.5 / max(_ShadowSharpness, 0.1);
                float shadow = 1 - smoothstep(_Cutoff-halfWidth, _Cutoff+halfWidth, lightFacing);
                color = lerp(color, color * _ShadowColor.rgb, shadow * _ShadowIntensity * bevel);
                color *= 1 - bevel * _EnableTextureOutline * _TextureOutlineDarkness * 0.28;

                float3 view = normalize(_WorldSpaceCameraPos.xyz-i.positionWS);
                float fresnel = 1-saturate(abs(dot(n,view)));
                float ao = pow(saturate(bevel), _AOFresnelPower) * lerp(0.5,1,fresnel);
                ao *= _EnableAOFresnel * _AOFresnelIntensity * (1+shadow*_AOFresnelShadowBoost);
                color = lerp(color, _AOFresnelTint.rgb, saturate(ao)*0.6);

                float3 lightWS = normalize(UnityObjectToWorldDir(lightOS));
                float3 halfVector = normalize(lightWS+view);
                float specular = pow(saturate(dot(n,halfVector)), max(1,_SpecularSharpness));
                specular *= _EnableSpecular * _SpecularIntensity * bevel * 0.18;
                float3 overlay = lerp(2*color*_SpecularColor.rgb,
                    1-2*(1-color)*(1-_SpecularColor.rgb), step(0.5,color));
                float3 softLight = (1-2*_SpecularColor.rgb)*color*color+2*_SpecularColor.rgb*color;
                if (_SpecBlendMode < 0.5) color += _SpecularColor.rgb * specular;
                else color = lerp(color, _SpecBlendMode < 1.5 ? overlay : softLight, specular);
                color += _RimColor.rgb * pow(fresnel,_RimPower) * bevel * _EnableRim * _RoadRimStrength;

                // Ribbon U is normalized by Dreamteck before the material tiling is applied.
                float across = saturate(i.rawUV.x * abs(_MainTex_ST.x));
                float edge = min(across,1-across);
                float contact = 1-smoothstep(0,max(0.001,_RoadEdgeShadeWidth),abs(edge-_RoadInsideShadowOffset));
                float alpha = 1;
                if (_RibbonMode > 1.5)
                {
                    // Feather the enlarged grass ribbon into the flat background.
                    alpha = smoothstep(0,0.16,edge);
                }
                else if (_RibbonMode > 0.5)
                {
                    color = lerp(color,_RoadInsideShadowColor.rgb,contact*_RoadInsideShadowStrength);
                }
                // The map stores distance from the authored grass silhouette,
                // independently of the plane's tiling or camera angle.
                float2 contactUV = i.rawUV;
                float grassDistance = tex2D(_ContactShadowMap,contactUV).r * 0.1;
                if (_RibbonMode < 0.5 && _ContactJoinRepair > 0.5)
                {
                    float3 boardPoint = mul(_ContactWorldToBoard,float4(i.positionWS,1)).xyz;
                    // The fixed board's two open joins have floor edges at
                    // abs(x)=0.16/0.22. Extend their lateral shadow through the
                    // map's artificial closing edge, preserving the bank shadow.
                    float side = abs(boardPoint.x);
                    float lateralDistance = min(abs(side-0.16),abs(side-0.22));
                    float atJoin = smoothstep(0.29,0.3,boardPoint.z);
                    atJoin *= step(0.16,side)*step(side,0.22);
                    grassDistance = lerp(grassDistance,lateralDistance,atJoin);
                }
                // A spline ribbon has different UVs from the authored board.
                // Reuse the same colour/strength/width, measuring from its two banks.
                if (_RibbonMode > 0.5)
                    grassDistance = edge * _RibbonDistanceScale;
                float contactShadow = 1-smoothstep(0,max(0.001,_ContactShadowWidth),grassDistance);
                color = lerp(color,_ContactShadowColor.rgb,contactShadow*_ContactShadowStrength);
                return float4(color,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
