Shader "Toon/URP/LightedOutline"
{
	Properties
	{
		_Color ("Main Color", Color) = (0.5,0.5,0.5,1)
		_OutlineColor ("Outline Color", Color) = (0,0,0,1)
		_Outline ("Outline width", Range (.001, 0.1)) = .005
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Ramp ("Toon Ramp (RGB)", 2D) = "gray" {}
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

		// Main lit-like toon pass (works in URP forward)
		Pass
		{
			Name "FORWARD"
			Tags { "LightMode" = "UniversalForward" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase
			#pragma target 3.0

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _Ramp;
			float4 _Color;

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				// basic directional lighting using built-in _WorldSpaceLightPos0 (commonly set by URP)
				float3 N = normalize(i.worldNormal);

				// _WorldSpaceLightPos0 for directional has w == 0 and direction in .xyz
				float3 L = normalize(_WorldSpaceLightPos0.xyz);
				float ndotl = saturate(dot(N, L));

				// sample ramp texture horizontally using ndotl
				float rampSample = tex2D(_Ramp, float2(ndotl, 0.5)).r;

				fixed4 baseCol = tex2D(_MainTex, i.uv) * _Color;
				fixed3 finalRGB = baseCol.rgb * rampSample;

				return fixed4(finalRGB, baseCol.a);
			}
			ENDCG
		}

		// Outline pass: render backfaces enlarged (typical cartoon outline)
		Pass
		{
			Name "OUTLINE"
			Tags { "LightMode" = "UniversalForward" }
			Cull Front
			ZWrite On

			CGPROGRAM
			#pragma vertex vertOutline
			#pragma fragment fragOutline
			#pragma target 3.0

			#include "UnityCG.cginc"

			float4 _OutlineColor;
			float _Outline;

			struct appdata_outline
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f_outline
			{
				float4 pos : SV_POSITION;
			};

			v2f_outline vertOutline(appdata_outline v)
			{
				v2f_outline o;
				// offset along object-space normal (keeps outline stable with scaling if uniform)
				float3 offsetObj = normalize(v.normal) * _Outline;
				float4 posOffset = v.vertex + float4(offsetObj, 0.0);
				o.pos = UnityObjectToClipPos(posOffset);
				return o;
			}

			fixed4 fragOutline(v2f_outline i) : SV_Target
			{
				return _OutlineColor;
			}
			ENDCG
		}
	}

	Fallback "Toon/Lighted"
}