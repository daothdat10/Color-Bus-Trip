Shader "Stylized Water 2/Standard" {
	Properties {
		[Toggle] _ZWrite ("Depth writing", Float) = 0
		[Toggle] _ZClip ("Camera frustum clipping", Float) = 1
		[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull faces", Float) = 2
		[MaterialEnum(Simple, 0,Advanced, 1)] _ShadingMode ("Shading mode", Float) = 1
		[MaterialEnum(Mesh UV,0,World XZ projected ,1)] _WorldSpaceUV ("UV Coordinates", Float) = 1
		_Direction ("Animation direction", Vector) = (0,-1,0,0)
		_Speed ("Animation Speed", Float) = 1
		_SlopeAngleThreshold ("Angle° threshold", Range(0, 90)) = 15
		_SlopeAngleFalloff ("Angle° falloff", Range(15, 90)) = 25
		_SlopeStretching ("Slope UV stretch", Range(0, 1)) = 0.5
		_SlopeSpeed ("Slope speed multiplier", Float) = 2
		_SlopeFoam ("River slope foam", Range(0, 3)) = 1
		[HDR] _BaseColor ("Deep", Vector) = (0,0.44,0.62,1)
		[HDR] _ShallowColor ("Shallow", Vector) = (0.1,0.9,0.89,0.02)
		[PowerSlider(3)] _ColorAbsorption ("Color Absorption", Range(0, 1)) = 0
		_WaveTint ("Wave tint", Range(-0.1, 0.1)) = 0
		[HDR] _HorizonColor ("Horizon", Vector) = (0.84,1,1,0.15)
		_HorizonDistance ("Horizon Distance", Range(0.01, 32)) = 8
		[Toggle] _VertexColorDepth ("Vertex color (G) depth", Float) = 0
		_DepthVertical ("View Depth", Range(0.01, 16)) = 4
		_DepthHorizontal ("Vertical Height Depth", Range(0.01, 8)) = 1
		[Toggle] _DepthExp ("Exponential Blend", Float) = 1
		_EdgeFade ("Edge Fade", Float) = 0.1
		_ShadowStrength ("Shadow Strength", Range(0, 1)) = 1
		_TranslucencyStrength ("Translucency Strength", Range(0, 3)) = 1
		_TranslucencyStrengthDirect ("Translucency Strength (Direct)", Range(0, 0.5)) = 0.05
		_TranslucencyExp ("Translucency Exponent", Range(1, 32)) = 4
		_TranslucencyCurvatureMask ("Translucency Curvature mask", Range(0, 1)) = 0.75
		_TranslucencyReflectionMask ("Translucency Reflection mask", Range(0, 1)) = 1
		_CausticsBrightness ("Brightness", Float) = 2
		_CausticsChromance ("Chromance", Range(0, 1)) = 1
		_CausticsTiling ("Tiling", Float) = 0.5
		_CausticsSpeed ("Speed multiplier", Float) = 0.1
		_CausticsDistortion ("Distortion", Range(0, 1)) = 0.15
		[SingleLineTexture] [NoScaleOffset] _CausticsTex ("Texture", 2D) = "black" {}
		_UnderwaterSurfaceSmoothness ("Underwater Surface Smoothness", Range(0, 1)) = 0.8
		_UnderwaterRefractionOffset ("Underwater Refraction Offset", Range(0, 1)) = 0.2
		_RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.1
		_RefractionChromaticAberration ("Refraction Chromatic Aberration)", Range(0, 1)) = 1
		[MaterialEnum(Depth Texture,0,Vertex Color (R),1,Depth Texture and Vertex Color,2)] _IntersectionSource ("Intersection source", Float) = 0
		[MaterialEnum(None,0,Sharp,1,Smooth,2)] _IntersectionStyle ("Intersection style", Float) = 1
		[SingleLineTexture] [NoScaleOffset] _IntersectionNoise ("Intersection noise", 2D) = "white" {}
		_IntersectionColor ("Color", Vector) = (1,1,1,1)
		_IntersectionLength ("Distance", Range(0.01, 5)) = 2
		_IntersectionClipping ("Cutoff", Range(0.01, 1)) = 0.5
		_IntersectionFalloff ("Falloff", Range(0.01, 1)) = 0.5
		_IntersectionTiling ("Noise Tiling", Float) = 0.2
		_IntersectionDistortion ("Distortion", Range(0, 1)) = 0.2
		_IntersectionSpeed ("Speed multiplier", Float) = 0.1
		_IntersectionRippleDist ("Ripple distance", Float) = 32
		_IntersectionRippleStrength ("Ripple Strength", Range(0, 1)) = 0.5
		[SingleLineTexture] [NoScaleOffset] _FoamTex ("Foam Mask", 2D) = "black" {}
		_FoamColor ("Color", Vector) = (1,1,1,1)
		_FoamSpeed ("Speed multiplier", Float) = 0.1
		_FoamSubSpeed ("Speed multiplier (sub-layer)", Float) = -0.25
		_FoamBaseAmount ("Base amount", Range(0, 1)) = 0
		_FoamClipping ("Clipping", Range(0, 0.999)) = 0
		_FoamWaveAmount ("Wave crest amount", Range(0, 2)) = 0
		_FoamTiling ("Tiling", Vector) = (0.1,0.1,0,0)
		_FoamSubTiling ("Tiling (sub-layer)", Float) = 0.5
		_FoamDistortion ("Distortion", Range(0, 3)) = 0
		[Toggle] _VertexColorFoam ("Vertex color (A) foam", Float) = 0
		[SingleLineTexture] [NoScaleOffset] _FoamTexDynamic ("Foam (Dynamic)", 2D) = "black" {}
		_FoamTilingDynamic ("Tiling (Dynamic)", Float) = 0.1
		_FoamSubTilingDynamic ("Tiling (sub-layer)", Float) = 2
		_FoamSpeedDynamic ("Speed multiplier", Float) = 0.1
		_FoamSubSpeedDynamic ("Speed multiplier (sub-layer)", Float) = -0.1
		[SingleLineTexture] [NoScaleOffset] [Normal] _BumpMap ("Normals", 2D) = "bump" {}
		[SingleLineTexture] [NoScaleOffset] [Normal] _BumpMapSlope ("Normals (River slopes)", 2D) = "bump" {}
		_NormalTiling ("Tiling", Vector) = (0.5,0.5,0,0)
		_NormalSubTiling ("Tiling (sub-layer)", Float) = 0.5
		_NormalStrength ("Strength", Range(0, 1)) = 0.135
		_NormalSpeed ("Speed multiplier", Float) = 1
		_NormalSubSpeed ("Speed multiplier (sub-layer)", Float) = -0.5
		[SingleLineTexture] [NoScaleOffset] [Normal] _BumpMapLarge ("Normals (Distance)", 2D) = "bump" {}
		_DistanceNormalsFadeDist ("Distance normals blend (Start/End)", Vector) = (100,300,0,0)
		_DistanceNormalsTiling ("Distance normals: Tiling multiplier", Float) = 0.15
		_SparkleIntensity ("Sparkle Intensity", Range(0, 10)) = 0
		_SparkleSize ("Sparkle Size", Range(0, 1)) = 0.28
		[PowerSlider(0.1)] _SunReflectionSize ("Sun Size", Range(0, 1)) = 0.5
		_SunReflectionStrength ("Sun Strength", Float) = 10
		_SunReflectionDistortion ("Sun Distortion", Range(0, 2)) = 0.49
		_PointSpotLightReflectionStrength ("Point/spot light strength", Float) = 10
		[PowerSlider(0.1)] _PointSpotLightReflectionSize ("Point/spot light size", Range(0, 1)) = 0
		_PointSpotLightReflectionDistortion ("Point/spot light distortion", Range(0, 1)) = 0.5
		_ReflectionStrength ("Strength", Range(0, 1)) = 1
		_ReflectionDistortion ("Distortion", Range(0, 1)) = 0.05
		_ReflectionBlur ("Probe Blur Factor", Range(0, 1)) = 0
		_ReflectionFresnel ("Curvature mask", Range(0.01, 20)) = 5
		_ReflectionLighting ("Lighting influence", Range(0, 1)) = 0
		_PlanarReflection ("Planar Reflections", 2D) = "" {}
		_PlanarReflectionsEnabled ("Planar Enabled", Float) = 0
		_WaveSpeed ("Speed", Float) = 2
		_WaveHeight ("Height", Range(0, 10)) = 0.25
		[Toggle] _VertexColorWaveFlattening ("Vertex color (B) wave flattening", Float) = 0
		_WaveNormalStr ("Normal Strength", Range(0, 32)) = 0.5
		_WaveDistance ("Distance", Range(0, 1)) = 0.8
		_WaveFadeDistance ("Wave fade distance (Start/End)", Vector) = (150,300,0,0)
		_WaveSteepness ("Steepness", Range(0, 5)) = 0.1
		_WaveCount ("Count", Range(1, 5)) = 1
		_WaveDirection ("Direction", Vector) = (1,1,1,1)
		[ToggleOff(_UNLIT)] _LightingOn ("Enable lighting", Float) = 1
		[ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows ("Recieve Shadows", Float) = 1
		[Toggle(_FLAT_SHADING)] _FlatShadingOn ("Flat shading", Float) = 0
		[Toggle(_TRANSLUCENCY)] _TranslucencyOn ("Enable translucency shading", Float) = 1
		[Toggle(_REFRACTION)] _RefractionOn ("_REFRACTION", Float) = 1
		[Toggle(_RIVER)] _RiverModeOn ("River Mode", Float) = 0
		[Toggle(_CAUSTICS)] _CausticsOn ("Caustics ON", Float) = 1
		[ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularReflectionsOn ("Specular Reflections", Float) = 1
		[ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflectionsOn ("Environment Reflections", Float) = 1
		[Toggle(_NORMALMAP)] _NormalMapOn ("Normal maps", Float) = 1
		[Toggle(_DISTANCE_NORMALS)] _DistanceNormalsOn ("Distance normal map", Float) = 1
		[Toggle(_FOAM)] _FoamOn ("Foam", Float) = 1
		[Toggle(_DISABLE_DEPTH_TEX)] _DisableDepthTexture ("Disable depth texture", Float) = 0
		[Toggle(_WAVES)] _WavesOn ("_WAVES", Float) = 0
		[Toggle] _ReceiveDynamicEffects ("Receive Dynamic Effects", Float) = 1
		[HideInInspector] _BaseMap ("Albedo", 2D) = "white" {}
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Universal Render Pipeline/FallbackError"
	//CustomEditor "StylizedWater2.MaterialUI"
}