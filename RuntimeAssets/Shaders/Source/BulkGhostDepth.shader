Shader "GTR/BulkGhostDepth" {
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry-10" "IgnoreProjector"="True" }
		LOD 50
		ZWrite On
		ZTest LEqual
		Cull Off
		ColorMask 0
		Fog { Mode Off }

		Pass {
			Tags { "LightMode"="ForwardBase" }
			ZWrite On
			ZTest LEqual
			Cull Off
			ColorMask 0
			Fog { Mode Off }

			CGPROGRAM
			#pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"

			struct appdata {
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v) {
				UNITY_SETUP_INSTANCE_ID(v);
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				return 0;
			}
			ENDCG
		}
	}
}
