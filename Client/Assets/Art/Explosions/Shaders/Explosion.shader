Shader "HOVL/Particles/Explosion"
{
	Properties
	{
		_MainTex ("MainText", 2D) = "white" {}
		_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
		_Noise("Noise", 2D) = "white" {}
		_FinalEmission("Final Emission", Float) = 1
		_Color("Color", Color) = (1,1,1,1)
		_GlowColor("Glow Color", Color) = (1,1,0,1)
		_NoisespeedXYpowerZ("Noise speed XY power Z", Vector) = (0.314,0.427,0.001,0)
		[MaterialToggle] _Usedepth ("Use depth?", Float ) = 0
	}

	Category 
	{
		SubShader
		{
			Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
			Blend SrcAlpha OneMinusSrcAlpha
			ColorMask RGB
			Cull Off
			Lighting Off 
			ZWrite Off
			ZTest LEqual
			
			Pass {
			
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 2.0
				#pragma multi_compile_particles
				#pragma multi_compile_fog
				#include "UnityShaderVariables.cginc"


				#include "UnityCG.cginc"

				struct appdata_t 
				{
					float4 vertex : POSITION;
					fixed4 color : COLOR;
					float4 texcoord : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					
				};

				struct v2f 
				{
					float4 vertex : SV_POSITION;
					fixed4 color : COLOR;
					float4 texcoord : TEXCOORD0;
					UNITY_FOG_COORDS(1)
					#ifdef SOFTPARTICLES_ON
					float4 projPos : TEXCOORD2;
					#endif
					UNITY_VERTEX_OUTPUT_STEREO
					
				};
				
				uniform sampler2D _MainTex;
				uniform float4 _MainTex_ST;
				uniform sampler2D_float _CameraDepthTexture;
				uniform float _InvFade;
				uniform float4 _NoisespeedXYpowerZ;
				uniform sampler2D _Noise;
				uniform float4 _Noise_ST;
				uniform float4 _GlowColor;
				uniform float4 _Color;
				uniform float _FinalEmission;
				uniform fixed _Usedepth;

				v2f vert ( appdata_t v  )
				{
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
					

					v.vertex.xyz +=  float3( 0, 0, 0 ) ;
					o.vertex = UnityObjectToClipPos(v.vertex);
					#ifdef SOFTPARTICLES_ON
						o.projPos = ComputeScreenPos (o.vertex);
						COMPUTE_EYEDEPTH(o.projPos.z);
					#endif
					o.color = v.color;
					o.texcoord = v.texcoord;
					UNITY_TRANSFER_FOG(o,o.vertex);
					return o;
				}

				fixed4 frag ( v2f i  ) : SV_Target
				{
					float lp = 1;
					#ifdef SOFTPARTICLES_ON
						float sceneZ = LinearEyeDepth (SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
						float partZ = i.projPos.z;
						float fade = saturate (_InvFade * (sceneZ-partZ));
						lp *= lerp(1, fade, _Usedepth);
						i.color.a *= lp;
					#endif

					float3 uv_Noise = i.texcoord.xyz;
					uv_Noise.xy = i.texcoord.xyz.xy * _Noise_ST.xy + _Noise_ST.zw;
					float2 appendResult1 = (float2(_NoisespeedXYpowerZ.x , _NoisespeedXYpowerZ.y));
					float3 uv_MainTexture = i.texcoord.xyz;
					uv_MainTexture.xy = i.texcoord.xyz.xy * _MainTex_ST.xy + _MainTex_ST.zw;
					float4 tex2DNode14 = tex2D( _MainTex, ( ( _NoisespeedXYpowerZ.z * tex2D( _Noise, ( uv_Noise + float3( ( _Time.y * appendResult1 ) ,  0.0 ) ).xy ) ) + float4( uv_MainTexture , 0.0 ) ).rg );
					float3 appendResult34 = (float3(_GlowColor.r , _GlowColor.g , _GlowColor.b));
					

					fixed4 col = ( ( ( pow( tex2DNode14 , 3.0 ) * float4( appendResult34 , 0.0 ) * uv_MainTexture.z ) + tex2DNode14 ) * _Color * i.color * _FinalEmission );
					UNITY_APPLY_FOG(i.fogCoord, col);
					return col;
				}
				ENDCG 
			}
		}	
	}	
}
