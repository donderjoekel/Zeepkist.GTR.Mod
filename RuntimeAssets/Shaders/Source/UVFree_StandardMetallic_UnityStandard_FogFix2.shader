Shader "GTR_UVFree/StandardMetallic/UnityStandard_FogFix2" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
	}
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Overlay-50" "IgnoreProjector"="True" }
		LOD 100
		ZWrite On
		ZTest LEqual
		Blend One Zero
		BlendOp Add
		Cull Off
		ColorMask RGBA
		Fog { Mode Off }

		Pass {
			Tags { "LightMode"="ForwardBase" }
			ZWrite On
			ZTest LEqual
			Blend One Zero
			BlendOp Add
			Cull Off
			ColorMask RGBA
			Fog { Mode Off }

			CGPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "Lighting.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;
			StructuredBuffer<float4x4> _Matrices;

			struct appdata {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR0;
				fixed3 light : COLOR1;
			};

			v2f vert(appdata v, uint instanceID : SV_InstanceID) {
				v2f o;
				float4 worldPosition = mul(_Matrices[instanceID], v.vertex);
				float3 worldNormal = normalize(mul((float3x3)_Matrices[instanceID], v.normal));
				o.vertex = mul(UNITY_MATRIX_VP, worldPosition);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = _Color;
				fixed3 ambient = ShadeSH9(float4(worldNormal, 1));
				fixed directional = saturate(dot(worldNormal, _WorldSpaceLightPos0.xyz));
				o.light = ambient + (_LightColor0.rgb * directional);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				fixed3 albedo = tex2D(_MainTex, i.uv).rgb * i.color.rgb;
				fixed4 color = fixed4(albedo * i.light, 1);
				color.a = 1;
				return color;
			}
			ENDCG
		}
	}
}
