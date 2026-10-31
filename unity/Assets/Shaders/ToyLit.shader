// Piamo：おもちゃのピアノのような、やわらかい光のシェーダー（URP）
//  - 光の当たらない側も暗くなりすぎない（かげの いろ _ShadowColor へ）
//  - つやのハイライトと、ふちの光（リム）
//  - 頂点カラー（Blender で入れた、すき間の暗さ）をかける
//  - _EmissionColor で ひからせる（おした鍵盤・ヒント）
Shader "Piamo/ToyLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _ShadowColor ("Shadow Color", Color) = (0.72, 0.7, 0.86, 1)
        _Gloss ("Gloss", Range(0, 1)) = 0.6
        _SpecStrength ("Specular", Range(0, 2)) = 0.6
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimStrength ("Rim", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half4 _ShadowColor;
                half _Gloss;
                half _SpecStrength;
                half4 _RimColor;
                half _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                Light light = GetMainLight();
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half wrap = dot(n, light.direction) * 0.5h + 0.5h;
                half lit = smoothstep(0.25h, 0.85h, wrap);
                half3 baseCol = _BaseColor.rgb * i.color.rgb;
                half3 col = lerp(baseCol * _ShadowColor.rgb, baseCol, lit) * light.color;
                // 空からの光（上を向いた面を少し明るく）
                col += baseCol * (0.12h + 0.1h * saturate(n.y));
                float3 h = normalize(light.direction + viewDir);
                half spec = pow(saturate(dot(n, h)), lerp(10.0h, 90.0h, _Gloss)) * _SpecStrength * _Gloss;
                col += spec * light.color;
                half rim = pow(1.0h - saturate(dot(n, viewDir)), 3.0h) * _RimStrength;
                col += rim * _RimColor.rgb;
                col += _EmissionColor.rgb;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
