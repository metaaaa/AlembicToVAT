Shader "Unlit/TextureAnimPlayer"
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
			#pragma shader_feature _ IS_FLUID

			#include "UnityCG.cginc"

			#define ts _PosTex_TexelSize

			struct appdata
			{
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex, _PosTex, _NmlTex;
			float4 _PosTex_TexelSize;
			float _Length, _DT;
			int _VertCount;
			
			v2f vert (appdata v, uint vid : SV_VertexID)
			{
				uint texWidth = 1.0 / ts.x;
				uint rowNum = _VertCount / texWidth + 1;
				float t = 0;
#if ANIM_LOOP
				t = frac(_Time.y / _Length);
#else
				t = frac( _DT / _Length);
#endif
				float blockHeihgt = ts.y * rowNum;
				float x = (vid % texWidth) * ts.x;
				float baseY = floor(t / blockHeihgt) * blockHeihgt;
				float rowDiff = floor(vid / texWidth) * ts.y;
				float y = (blockHeihgt + baseY > 1.0) ? rowDiff : baseY + rowDiff;
				float4 pos = tex2Dlod(_PosTex, float4(x, y, 0, 0));
				float3 normal = tex2Dlod(_NmlTex, float4(x, y, 0, 0));

#ifdef IS_FLUID
#else
				float nextY = (y - rowDiff + blockHeihgt * 2.0 > 1.0) ? y : y + blockHeihgt;
				float4 pos2 = tex2Dlod(_PosTex, float4(x, nextY, 0, 0));
				float3 normal2 = tex2Dlod(_NmlTex, float4(x, nextY, 0, 0));

				float p = fmod(t, blockHeihgt) / blockHeihgt;
				pos = lerp(pos, pos2, p);
				normal = lerp(normal, normal2, p);
#endif
				v2f o;
				o.vertex = UnityObjectToClipPos(pos);
				o.normal = UnityObjectToWorldNormal(normal);
				o.uv = v.uv;
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
				half diff = dot(i.normal, float3(0,1,0))*0.5 + 0.5;
				half4 col = tex2D(_MainTex, i.uv);
				return diff * col;
			}
			ENDCG
		}
	}
}
