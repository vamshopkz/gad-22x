// Building facade with "interior mapping": every window shows a real-looking room behind the glass
// (back wall with furniture, side walls, floor, ceiling) that moves correctly with the camera.
// Walls are lit with URP's PBR lighting (sun, shadows, reflections, fog); glass reflects the sky
// and the room behind it glows a little by day and, for some rooms, warmly at night.
Shader "Gad/FacadeInterior"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _MaskMap ("Mask (R metal, G glass, A smooth)", 2D) = "black" {}
        _Rooms ("Room atlas", 2D) = "gray" {}
        _RoomDepth ("Room depth", Float) = 1.1
        _Cells ("Rooms per tile", Vector) = (4, 4, 0, 0)
        _LitRate ("Lit rooms at night", Float) = 0.45
        _WallSmooth ("Wall smoothness", Float) = 0.3
        _DayRoom ("Room brightness by day", Float) = 0.42
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_Rooms); SAMPLER(sampler_Rooms);
            float _GadNight;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _RoomDepth;
                float4 _Cells;
                half _LitRate;
                half _WallSmooth;
                half _DayRoom;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // Ray from the window into a unit room [0,1]x[0,1]x[-depth,0]; returns the room colour.
            half3 Room(float2 f, float3 rd, float variant, float depth)
            {
                rd.z = min(rd.z, -1e-3);
                float tx = (rd.x > 0 ? (1 - f.x) : -f.x) / (abs(rd.x) > 1e-4 ? rd.x : 1e-4);
                float ty = (rd.y > 0 ? (1 - f.y) : -f.y) / (abs(rd.y) > 1e-4 ? rd.y : 1e-4);
                float tz = -depth / rd.z;
                float t = min(min(tx, ty), tz);
                float3 h = float3(f, 0) + rd * t;
                float col = variant * 0.25;
                float2 auv;
                float shade = 1;
                if (t == tz)
                {
                    auv = float2(h.x, 0.5 + h.y * 0.5);                      // back wall: top half of the column
                }
                else if (t == tx)
                {
                    auv = float2(saturate(-h.z / depth), 0.25 + h.y * 0.25);  // side wall
                    shade = 0.82;
                }
                else if (rd.y < 0)
                {
                    auv = float2(h.x, 0.125 + saturate(-h.z / depth) * 0.125); // floor
                    shade = 0.9;
                }
                else
                {
                    auv = float2(h.x, saturate(-h.z / depth) * 0.125);          // ceiling
                    shade = 1.05;
                }
                auv.x = col + saturate(auv.x) * 0.25;
                // atlas rows run top to bottom in the image: back(0-0.5), side(0.5-0.75), floor, ceiling
                auv.y = 1 - auv.y;
                half3 c = SAMPLE_TEXTURE2D(_Rooms, sampler_Rooms, auv).rgb * shade;
                return c * lerp(1.0, 0.55, saturate(-h.z / depth));            // darker deeper in
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                half4 alb = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
                half glass = smoothstep(0.3, 0.7, mask.g);
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);

                float3 N = normalize(i.normalWS);
                float3 T = normalize(i.tangentWS.xyz);
                float3 B = cross(N, T) * i.tangentWS.w;
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3x3 tbn = float3x3(T, B, N);
                float3 nWS = normalize(mul(nTS, tbn));

                // interior behind the glass
                float2 cuv = uv * _Cells.xy;
                float2 cell = floor(cuv);
                float2 f = frac(cuv);
                float3 rd = float3(dot(-V, T), dot(-V, B), dot(-V, N));
                float h1 = Hash(cell + 0.37), h2 = Hash(cell * 1.7 + 4.1);
                half3 room = Room(f, rd, floor(h1 * 3.999), _RoomDepth);
                half lit = step(1 - _LitRate, h2);
                half night = saturate(_GadNight);
                half bright = lerp(_DayRoom, lerp(0.06, 1.35, lit), night);
                half3 roomCol = room * bright * lerp(half3(1, 1, 1), half3(1.1, 0.92, 0.7), night * lit);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalize(lerp(nWS, N, glass));
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(nWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = lerp(alb.rgb, alb.rgb * 0.04, glass);
                s.metallic = lerp(mask.r, 0.0, glass);
                s.smoothness = lerp(min(mask.a, 0.75) * _WallSmooth / 0.3, 0.96, glass);
                s.normalTS = lerp(nTS, half3(0, 0, 1), glass);
                s.occlusion = 1;
                s.alpha = 1;
                // glass: the room shows through (less at grazing angles where the reflection takes over)
                half facing = saturate(dot(N, V));
                s.emission = roomCol * glass * lerp(0.35, 1.0, facing);
                half4 c = UniversalFragmentPBR(inputData, s);
                c.rgb = MixFog(c.rgb, inputData.fogCoord);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}
