Shader "AlembicToVAT/TextureAnimPlayer"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_PosTex("position texture", 2D) = "black"{}
		_NmlTex("normal texture", 2D) = "white"{}
		_DT ("delta time", float) = 0
		_Length ("animation length", Float) = 1
		_VertCount ("VertCount", Int) = 1
		[Toggle(ANIM_LOOP)] _Loop("loop", Float) = 1
		[Toggle(IS_FLUID)] _IsFluid("IsFluid", Float) = 0
		[Toggle(AlphaIsNormal)] _AlphaIsNormal("AlphaIsNormal", Float) = 0
		[Enum(UnityEngine.Rendering.CullMode)]
		_Cull("Cull", Float) = 2
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		Cull [_Cull]

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile ___ ANIM_LOOP
			#pragma multi_compile_instancing
			#pragma shader_feature _ IS_FLUID

			#include "UnityCG.cginc"

			#define ts _PosTex_TexelSize

			struct appdata
			{
				float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex, _PosTex, _NmlTex;
			float4 _PosTex_TexelSize;
			float _Length, _DT, _AlphaIsNormal;
			int _VertCount;
			

			float3 i_spherical_16(half data)
            {
                uint d = data;
                float2 v = float2(d & 255u, d >> 8) / 255.0;
                v.x = 2.0 * v.x - 1.0;
                v *= 3.141593;
                return normalize(float3(sin(v.y) * cos(v.x), cos(v.y), sin(v.y) * sin(v.x)));
            }
			
			v2f vert (appdata v, uint vid : SV_VertexID)
			{
				uint texWidth = ts.z;
				uint rowNum = _VertCount * ts.x + 1;
				float t = 0;
#if ANIM_LOOP
				t = frac(_Time.y / _Length);
#else
				t = frac( _DT / _Length);
#endif
				float x = (vid % texWidth) * ts.x;
				float blockHeihgt = ts.y * rowNum;
				float baseY = floor(t / blockHeihgt) * blockHeihgt;
				float rowDiff = floor(vid * ts.x) * ts.y;
				float y = baseY + rowDiff + (0.5*ts.y) ;
				half4 pos = tex2Dlod(_PosTex, float4(x, y, 0, 0));
				// float3 normal = tex2Dlod(_NmlTex, float4(x, y, 0, 0));
				float3 normal = _AlphaIsNormal ? i_spherical_16(pos.a) : tex2Dlod(_NmlTex, float4(x, y, 0, 0));

#ifdef IS_FLUID
#else
				float nextY = (y - rowDiff + blockHeihgt >= 1.0) ? y : y + blockHeihgt;
				float4 pos2 = tex2Dlod(_PosTex, float4(x, nextY, 0, 0));
				float3 normal2 = _AlphaIsNormal ? i_spherical_16(pos2.a) : tex2Dlod(_NmlTex, float4(x, nextY, 0, 0));

				float p = fmod(t, blockHeihgt) / blockHeihgt;
				pos = lerp(pos, pos2, p);
				normal = lerp(normal, normal2, p);
#endif
				v2f o;
				UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.vertex = UnityObjectToClipPos(pos);
				o.normal = UnityObjectToWorldNormal(normal);
				o.uv = v.uv;
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				half diff = dot(i.normal, float3(0,1,0))*0.5 + 0.5;
				half4 col = tex2D(_MainTex, i.uv);
				return diff * col;
			}
			ENDCG
		}
	}
}
