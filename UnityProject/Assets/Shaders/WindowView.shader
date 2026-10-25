// 0.9: a window seen from inside a flat or an office: the real city outside, recorded as a cubemap
// from the building's own place and floor (RoomCubes.ViewFrom), looked up by the view direction.
Shader "Gad/WindowView"
{
    Properties
    {
        _View ("View", Cube) = "" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURECUBE(_View); SAMPLER(sampler_View);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.positionWS - _WorldSpaceCameraPos);
                half3 c = SAMPLE_TEXTURECUBE(_View, sampler_View, d).rgb * _Tint.rgb;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
