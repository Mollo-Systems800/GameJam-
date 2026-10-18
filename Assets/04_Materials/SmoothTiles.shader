Shader "GameJam/Smooth Tiles"
{
    Properties
    {
        _BaseColor ("Tile color", Color) = (0.7,0.68,0.61,1)
        _GroutColor ("Joint color", Color) = (0.30,0.31,0.30,1)
        _TileSize ("Tile size in metres", Float) = 0.75
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _GroutColor;
                float _TileSize;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS; output.positionWS=p.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.fog=ComputeFogFactor(p.positionCS.z);
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.positionWS.xz/max(0.1,_TileSize);
                float2 edge=min(frac(uv),1-frac(uv))*_TileSize;
                float aa=max(0.001,max(fwidth(edge.x),fwidth(edge.y)));
                half tile=smoothstep(0.002-aa,0.005+aa,min(edge.x,edge.y));
                half variation=(Hash(floor(uv))-.5)*.045;
                half grain=(Hash(floor(input.positionWS.xz*95))-.5)*.012;
                half3 albedo=lerp(_GroutColor.rgb,_BaseColor.rgb+variation+grain,tile);
                half3 normal=normalize(input.normalWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 diffuse=SampleSH(normal)+light.color*saturate(dot(normal,light.direction))*light.shadowAttenuation;
                half3 view=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half shine=pow(saturate(dot(normal,SafeNormalize(light.direction+view))),48)*.06*tile*light.shadowAttenuation;
                return half4(MixFog(albedo*diffuse+light.color*shine,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
