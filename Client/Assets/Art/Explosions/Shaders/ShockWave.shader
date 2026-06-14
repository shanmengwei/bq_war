Shader "HOVL/Particles/ShockWave"
{
    Properties
    {
        [MaterialToggle] _Usedepth ("Use depth?", Float) = 0
        _InvFade ("Soft Particles Factor", Float) = 1.0
        _Power("Power", Float) = 9
        _Opacity("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_particles
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                half4 color    : COLOR;
                float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex       : SV_POSITION;
                half4 color        : COLOR;
                float4 texcoord     : TEXCOORD0;
                float4 screenPos    : TEXCOORD1;
                #ifdef SOFTPARTICLES_ON
                float4 projPos      : TEXCOORD2;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _InvFade;
                float _Power;
                float _Opacity;
                float _Usedepth;
            CBUFFER_END

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.screenPos = ComputeScreenPos(o.vertex);

                #ifdef SOFTPARTICLES_ON
                    o.projPos = ComputeScreenPos(o.vertex);
                    o.projPos.z = -TransformWorldToView(TransformObjectToWorld(v.vertex.xyz)).z;
                #endif

                o.color = v.color;
                o.texcoord = v.texcoord;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float lp = 1.0;
                #ifdef SOFTPARTICLES_ON
                    float2 screenUV = i.projPos.xy / i.projPos.w;
                    float sceneZ = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float partZ = i.projPos.z;
                    float fade = saturate((sceneZ - partZ) / _InvFade);
                    lp *= lerp(1.0, fade, _Usedepth);
                    i.color.a *= lp;
                #endif

                // 计算扭曲量
                float2 uv = i.texcoord.xy;
                float2 centered = abs((uv - 0.5) * 2.1);
                float len = length(centered);
                float dist = saturate(pow(len * saturate(pow(len, -45.0)), _Power));

                // 旋转扭曲屏幕 UV
                float2 screenUVBase = i.screenPos.xy / i.screenPos.w;
                float cosA = cos(dist);
                float sinA = sin(dist);
                float2 rotated = mul(screenUVBase - 0.5, float2x2(cosA, -sinA, sinA, cosA)) + 0.5;

                // 采样不透明纹理（替代 GrabPass）
                float3 sceneColor = SampleSceneColor(rotated);

                float alpha = dist * i.color.a * _Opacity;
                return half4(sceneColor, alpha);
            }
            ENDHLSL
        }
    }
}