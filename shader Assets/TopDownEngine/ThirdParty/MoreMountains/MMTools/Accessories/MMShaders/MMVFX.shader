Shader "MoreMountains/MMVFX"
{
	Properties
	{
		_Cutoff("Mask Clip Value", Float) = 0.5
		_MainTex("MainTex", 2D) = "white" {}
		_MainTexPanningSpeed("MainTexPanningSpeed", Vector) = (0,0,0,0)
		_Tint("Tint", Color) = (1,1,1,1)
		_Opacity("Opacity", Range(0,1)) = 1
		_OpacityMask("OpacityMask", Range(0,1)) = 0.78
		_Normal("Normal", 2D) = "bump" {}
		_UseVertexColors("UseVertexColors", Float) = 0
		_UseRimLight("UseRimLight", Float) = 0
		_RimColor("RimColor", Color) = (0,0.734,1,1)
		_RimPower("RimPower", Range(0,8)) = 2
		_RimAmount("RimAmount", Range(0,1)) = 0.7
		_HideRimUnderShadow("HideRimUnderShadow", Float) = 0
		_SharpRimLight("SharpRimLight", Float) = 1
		_EmissionTexture("EmissionTexture", 2D) = "white" {}
		_EmissionColor("EmissionColor", Color) = (2,2,2,1)
		_EmissionForce("EmissionForce", Float) = 0
		_Framerate("Framerate", Float) = 5
		_UseVertexOffset("UseVertexOffset", Float) = 0
		_VertexOffsetNoiseTexture("VertexOffsetNoiseTexture", 2D) = "white" {}
		_VertexOffsetFrequency("VertexOffsetFrequency", Float) = 2
		_VertexOffsetMagnitude("VertexOffsetMagnitude", Float) = 0.05
		_VertexOffsetX("VertexOffsetX", Float) = 0.5
		_VertexOffsetY("VertexOffsetY", Float) = 0.5
		_VertexOffsetZ("VertexOffsetZ", Float) = 0.5
		_OutlineColor("OutlineColor", Color) = (0.545,1,0,1)
		_OutlineWidth("OutlineWidth", Float) = 0.1
		_OutlineAlpha("OutlineAlpha", Range(0,1)) = 0
		_SecondaryTexture("SecondaryTexture", 2D) = "white" {}
		_SecondaryTextureStrength("SecondaryTextureStrength", Float) = 0
		_SecondaryTextureSize("SecondaryTextureSize", Float) = 1
		_SecondaryTextureSpeedFactor("SecondaryTextureSpeedFactor", Float) = 0
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" }

		// Outline pass (drawn first: front faces expanded)
		Pass
		{
			Name "OUTLINE"
			Tags { "LightMode" = "UniversalForward" }
			Cull Front
			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#pragma vertex VertOutline
			#pragma fragment FragOutline
			#pragma target 3.0

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _OutlineColor;
			float _OutlineWidth;
			float _OutlineAlpha;
			float _Cutoff;

			struct Attributes
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct Varyings
			{
				float4 pos : SV_POSITION;
			};

			Varyings VertOutline(Attributes v)
			{
				Varyings o;
				// expand along object-space normal
				float3 n = normalize(v.normal);
				float3 offset = n * _OutlineWidth;
				float4 pos = v.vertex + float4(offset, 0);
				o.pos = UnityObjectToClipPos(pos);
				return o;
			}

			fixed4 FragOutline(Varyings i) : SV_Target
			{
				// keep same clipping behavior as original outline surface
				// if OutlineAlpha below cutoff => discard
				if (_OutlineAlpha - _Cutoff <= 0) discard;
				return _OutlineColor;
			}
			ENDHLSL
		}

		// Main transparent/emissive pass
		Pass
		{
			Name "MAIN"
			Tags { "LightMode" = "UniversalForward" }
			Cull Back
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma target 3.0

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Tint;
			float2 _MainTexPanningSpeed;
			sampler2D _EmissionTexture;
			float4 _EmissionColor;
			float _EmissionForce;
			float _Opacity;
			float _OpacityMask;
			float _Cutoff;
			sampler2D _VertexOffsetNoiseTexture;
			float _VertexOffsetFrequency;
			float _VertexOffsetMagnitude;
			float _VertexOffsetX;
			float _VertexOffsetY;
			float _VertexOffsetZ;
			float _Framerate;
			sampler2D _SecondaryTexture;
			float _SecondaryTextureSize;
			float _SecondaryTextureSpeedFactor;
			float _SecondaryTextureStrength;
			float4 _OutlineColor; // unused here but kept for property order

			float _UseVertexOffset;
			float _UseVertexColors;
			float _UseRimLight;
			float4 _RimColor;
			float _RimPower;
			float _RimAmount;
			float _HideRimUnderShadow;
			float _SharpRimLight;

			struct Attributes
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct Varyings
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float3 worldNormal : TEXCOORD2;
				float4 color : COLOR;
			};

			Varyings Vert(Attributes v)
			{
				Varyings o;
				UNITY_INITIALIZE_OUTPUT(Varyings, o);

				// vertex offset animation (optional)
				float steppedTime = round(_Time.y * _Framerate) / max(0.0001, _Framerate);
				float3 posOS = v.vertex.xyz;

				if (_UseVertexOffset > 0.5)
				{
					float2 uvX = steppedTime + (posOS.xy * _VertexOffsetFrequency);
					float2 uvY = (steppedTime * 2.0) + (posOS.yz * _VertexOffsetFrequency);
					float2 uvZ = (steppedTime * 4.0) + (posOS.xz * _VertexOffsetFrequency);

					float rx = tex2Dlod(_VertexOffsetNoiseTexture, float4(uvX,0,0)).r - _VertexOffsetX;
					float ry = tex2Dlod(_VertexOffsetNoiseTexture, float4(uvY,0,0)).r - _VertexOffsetY;
					float rz = tex2Dlod(_VertexOffsetNoiseTexture, float4(uvZ,0,0)).r - _VertexOffsetZ;
					float3 offset = float3(rx, ry, rz) * _VertexOffsetMagnitude;
					posOS += offset;
				}

				o.worldPos = mul(unity_ObjectToWorld, float4(posOS,1)).xyz;
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				o.pos = UnityObjectToClipPos(float4(posOS,1));
				return o;
			}

			fixed4 Frag(Varyings i) : SV_Target
			{
				// Panner
				float2 panner = (_Time.y * _MainTexPanningSpeed.xy) + i.uv;

				float4 mainSample = tex2D(_MainTex, panner) * _Tint;

				// Secondary texture blend (subtractive like original)
				float2 secUV = i.uv * _SecondaryTextureSize + (_Time.y * _SecondaryTextureSpeedFactor);
				float4 secSample = tex2D(_SecondaryTexture, secUV) * _SecondaryTextureStrength;
				float4 albedo = saturate(mainSample - secSample);

				// Apply vertex color if enabled
				if (_UseVertexColors > 0.5)
					albedo *= i.color;

				// Emission calculation (multiplying by albedo as original)
				float2 uvEmission = i.uv;
				float4 emissionSample = tex2D(_EmissionTexture, uvEmission);
				float4 computedEmission = emissionSample * _EmissionColor * _EmissionForce;
				float3 emission = (albedo.rgb * computedEmission.rgb);

				// Rim light (optional, simple approximation)
				float3 N = normalize(i.worldNormal);
				float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
				float rim = 0;
				if (_UseRimLight > 0.5)
				{
					float ndotv = saturate(dot(N, V));
					if (_SharpRimLight > 0.5)
						rim = pow(1 - ndotv, _RimPower) * _RimAmount;
					else
						rim = smoothstep(0,1,1-ndotv) * _RimAmount;
				}

				float3 finalColor = albedo.rgb + emission + (_RimColor.rgb * rim);

				// Alpha and cutoff logic similar to original
				float alpha = _Opacity;
				float maskStep = step(albedo.r, _OpacityMask); // original used step(albedo, mask)
				if (maskStep - _Cutoff <= 0) discard;

				return fixed4(finalColor, alpha);
			}
			ENDHLSL
		}

		// Basic ShadowCaster pass to avoid URP errors (minimal)
		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			ZWrite On
			HLSLPROGRAM
			#pragma vertex vertShadow
			#pragma fragment fragShadow
			#pragma target 3.0
			#include "UnityCG.cginc"

			struct appdata_full_custom { float4 vertex : POSITION; float3 normal : NORMAL; };

			struct v2f_shadow { float4 pos : SV_POSITION; };

			v2f_shadow vertShadow(appdata_full_custom v)
			{
				v2f_shadow o;
				o.pos = UnityObjectToClipPos(v.vertex);
				return o;
			}

			float4 fragShadow(v2f_shadow i) : SV_Target
			{
				return float4(0,0,0,1);
			}
			ENDHLSL
		}
	}

	Fallback "Diffuse"
}