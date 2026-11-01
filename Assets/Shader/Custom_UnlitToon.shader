Shader "Custom/UnlitToon" {
	Properties {
		[Header(Texture and Color Settings)] _MainTex ("Texture", 2D) = "white" {}
		_BaseColor ("Base Color (Light Areas)", Color) = (1,1,1,1)
		_DarkTint ("Dark Tint (Dark Areas)", Color) = (0.2,0.2,0.2,1)
		_RoadGradientStrength ("Texture Gradient Strength", Range(0, 1)) = 1
		_GroundPatternStrength ("Ground Pattern Strength", Range(0, 1)) = 0
		_UseVertexColor ("Use Mesh Vertex Color", Range(0, 1)) = 0
		[Header(Shadow Settings)] _LightDirection ("Fake Light Direction", Vector) = (0,1,0,0)
		_ShadowColor ("Shadow Color", Color) = (0,0,0,1)
		_Cutoff ("Shadow Cutoff", Range(0, 1)) = 0.5
		_ShadowIntensity ("Legacy Lighting Shadow (unused on road)", Range(0, 1)) = 1
		_ShadowSharpness ("Shadow Sharpness", Range(0.1, 10)) = 1
		[Header(Rim Light Settings)] [Toggle] _EnableRim ("Enable Rim Light", Float) = 0
		_RimColor ("Rim Color", Color) = (1,1,1,1)
		_RimPower ("Rim Power", Range(0.1, 10)) = 2
		_RoadBankShade ("Road Bank Shade", Range(0, 1)) = 0.35
		_RoadRimStrength ("Road Rim Brightness", Range(0, 1)) = 0.55
		_RoadRimWidth ("Road Rim Width", Range(0.005, 0.1)) = 0.035
		[Header(Road Contact Shadow)] _RoadEdgeShadeColor ("Outside Grass Shadow Color", Color) = (0.55,0.68,0.32,1)
		_RoadInsideShadowColor ("Inside Road Shadow Color", Color) = (0.18,0.28,0.09,1)
		_RoadEdgeShadeStrength ("Outside Grass Shadow Strength", Range(0, 1)) = 0
		_RoadInsideShadowStrength ("Inside Road Shadow Strength", Range(0, 1)) = 0
		_RoadInsideShadowOffset ("Inside Road Shadow Offset", Range(0, 0.4)) = 0
		[HideInInspector] _RoadShadowDirection ("Legacy Shadow Direction", Range(-1, 1)) = 1
		_RoadEdgeShadeWidth ("Inside Road Shadow Width", Range(0.005, 0.5)) = 0.018
		_RoadOutsideShadowWidth ("Outside Grass Shadow Width", Range(0.1, 1)) = 1
		[Header(Specular Settings)] [Toggle] _EnableSpecular ("Enable Specular Effect", Float) = 0
		_SpecularColor ("Specular Color", Color) = (1,1,1,1)
		_SpecularIntensity ("Specular Intensity", Range(0, 1)) = 0.5
		_SpecularSharpness ("Specular Sharpness", Range(1, 128)) = 16
		[KeywordEnum(Add,Overlay,SoftLight)] _SpecBlendMode ("Spec Blend Mode", Float) = 1
		[Header(Outline Settings)] [Toggle] _EnableOutline ("Enable Outline", Float) = 1
		_OutlineThickness ("Outline Thickness", Float) = 0.03
		[Toggle] _EnableTextureOutline ("Texture Color Enable", Float) = 1
		_TextureOutlineDarkness ("Texture Color Darkness", Range(0, 1)) = 0.5
		_ManualOutlineColor ("Manual Outline Color", Color) = (0,0,0,1)
		[Header(Fake AO (Fresnel UnderRim))] [Toggle] _EnableAOFresnel ("Enable AO Fresnel", Float) = 1
		_AOFresnelPower ("AO Fresnel Power", Range(0.1, 10)) = 3
		_AOFresnelIntensity ("AO Intensity", Range(0, 1)) = 0.45
		_AOFresnelShadowBoost ("Shadow-Side Boost", Range(0, 1)) = 0.7
		_AOFresnelTint ("AO Tint (Darken To)", Color) = (0,0,0,1)
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		[Header(Render Face)] [Enum(Front,2, Back,1, Double Sided,0)] _Cull ("Render Face", Float) = 2
		[Header(Depth Settings)] [Toggle] _ZWrite ("Depth Write", Float) = 1
	}
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			Cull [_Cull]
			ZWrite [_ZWrite]
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;
			float4 _BaseColor;
			float4 _DarkTint;
			float4 _RimColor;
			float4 _RoadInsideShadowColor;
			float _RoadGradientStrength;
			float _GroundPatternStrength;
			float _UseVertexColor;
			float _EnableRim;
			float _RoadBankShade;
			float _RoadRimStrength;
			float _RoadRimWidth;
			float _RoadInsideShadowStrength;
			float _RoadInsideShadowOffset;
			float _RoadEdgeShadeWidth;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.color = input.color;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				float4 texel = _MainTex.Sample(sampler_MainTex, input.uv.xy);
				float3 color = lerp(float3(1.0, 1.0, 1.0), texel.rgb,
					saturate(_RoadGradientStrength)) * _BaseColor.rgb;
				float pattern = dot(texel.rgb, float3(0.299, 0.587, 0.114));
				color *= 1.0 + (pattern - 0.9) * _GroundPatternStrength;
				color *= lerp(float3(1.0, 1.0, 1.0), input.color.rgb,
					saturate(_UseVertexColor));
				float across = saturate(input.uv.x);
				float edgeDistance = min(across, 1.0 - across);
				if (_EnableRim > 0.5)
				{
					// The road gradient runs across U. Shade the curved banks and
					// brighten the narrow lips on both sides of the channel.
					float bank = smoothstep(0.06, 0.11, edgeDistance)
						* (1.0 - smoothstep(0.14, 0.20, edgeDistance));
						color = lerp(color, _DarkTint.rgb,
							bank * _RoadBankShade);
					float rim = 1.0 - smoothstep(0.0, _RoadRimWidth,
						abs(edgeDistance - 0.06));
						color = lerp(color, _RimColor.rgb,
							rim * _RoadRimStrength);
				}
				// Move the contact band inward across the tan road surface.
				float contactDistance = abs(edgeDistance
					- _RoadInsideShadowOffset);
				float contact = 1.0 - smoothstep(0.0,
					max(_RoadEdgeShadeWidth, 0.001), contactDistance);
				color = lerp(color, _RoadInsideShadowColor.rgb,
					contact * saturate(_RoadInsideShadowStrength));
				return float4(color, texel.a * _BaseColor.a);
			}

			ENDHLSL
		}
	}
}
