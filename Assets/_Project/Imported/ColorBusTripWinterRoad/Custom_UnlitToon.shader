Shader "Custom/UnlitToon" {
	Properties {
		[Header(Texture and Color Settings)] _MainTex ("Texture", 2D) = "white" {}
		_BaseColor ("Base Color (Light Areas)", Vector) = (1,1,1,1)
		_DarkTint ("Dark Tint (Dark Areas)", Vector) = (0.2,0.2,0.2,1)
		[Header(Shadow Settings)] _LightDirection ("Fake Light Direction", Vector) = (0,1,0,0)
		_ShadowColor ("Shadow Color", Vector) = (0,0,0,1)
		_Cutoff ("Shadow Cutoff", Range(0, 1)) = 0.5
		_ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 1
		_ShadowSharpness ("Shadow Sharpness", Range(0.1, 10)) = 1
		[Header(Rim Light Settings)] [Toggle] _EnableRim ("Enable Rim Light", Float) = 0
		_RimColor ("Rim Color", Vector) = (1,1,1,1)
		_RimPower ("Rim Power", Range(0.1, 10)) = 2
		[Header(Specular Settings)] [Toggle] _EnableSpecular ("Enable Specular Effect", Float) = 0
		_SpecularColor ("Specular Color", Vector) = (1,1,1,1)
		_SpecularIntensity ("Specular Intensity", Range(0, 1)) = 0.5
		_SpecularSharpness ("Specular Sharpness", Range(1, 128)) = 16
		[KeywordEnum(Add,Overlay,SoftLight)] _SpecBlendMode ("Spec Blend Mode", Float) = 1
		[Header(Outline Settings)] [Toggle] _EnableOutline ("Enable Outline", Float) = 1
		_OutlineThickness ("Outline Thickness", Float) = 0.03
		[Toggle] _EnableTextureOutline ("Texture Color Enable", Float) = 1
		_TextureOutlineDarkness ("Texture Color Darkness", Range(0, 1)) = 0.5
		_ManualOutlineColor ("Manual Outline Color", Vector) = (0,0,0,1)
		[Header(Fake AO (Fresnel UnderRim))] [Toggle] _EnableAOFresnel ("Enable AO Fresnel", Float) = 1
		_AOFresnelPower ("AO Fresnel Power", Range(0.1, 10)) = 3
		_AOFresnelIntensity ("AO Intensity", Range(0, 1)) = 0.45
		_AOFresnelShadowBoost ("Shadow-Side Boost", Range(0, 1)) = 0.7
		_AOFresnelTint ("AO Tint (Darken To)", Vector) = (0,0,0,1)
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		[Header(Render Face)] [Enum(Front,2, Back,1, Double Sided,0)] _Cull ("Render Face", Float) = 2
		[Header(Depth Settings)] [Toggle] _ZWrite ("Depth Write", Float) = 1
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		sampler2D _MainTex;
		struct Input
		{
			float2 uv_MainTex;
		};

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}
}