Shader "Custom/Pastel Atlas Stylized"
{
    Properties
    {
        [MainTexture] _BaseMap ("Atlas Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Tint", Color) = (1,1,1,1)

        [Header(Stylized Lighting)]
        _AmbientColor ("Ambient Color", Color) = (0.75, 0.82, 1.0, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.85

        _LightColor ("Light Color", Color) = (1.0, 0.92, 0.78, 1)
        _LightStrength ("Light Strength", Range(0, 2)) = 0.85

        _ShadowColor ("Stylized Shadow Color", Color) = (0.55, 0.62, 0.85, 1)
        _ShadowStrength ("Stylized Shadow Strength", Range(0, 1)) = 0.35

        _RampThreshold ("Light Ramp Threshold", Range(0, 1)) = 0.45
        _RampSoftness ("Light Ramp Softness", Range(0.001, 1)) = 0.35

        [Header(Real Unity Shadows)]
        [Toggle] _ReceiveRealShadows ("Receive Real Shadows", Float) = 1
        _RealShadowStrength ("Real Shadow Strength", Range(0, 1)) = 0.5

        [Header(Pastel Treatment)]
        _PastelStrength ("Pastel Strength", Range(0, 1)) = 0.25
        _Brightness ("Brightness", Range(0, 2)) = 1.0
        _Saturation ("Saturation", Range(0, 2)) = 0.9

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1.0, 0.85, 0.65, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.15
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.0

        [Header(Options)]
        [Toggle] _UseVertexColor ("Use Vertex Color", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        // ============================================================
        // FORWARD PASS
        // Visible stylized material.
        // ============================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog

            // Main light shadow support.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;

                float4 _AmbientColor;
                float _AmbientStrength;

                float4 _LightColor;
                float _LightStrength;

                float4 _ShadowColor;
                float _ShadowStrength;

                float _RampThreshold;
                float _RampSoftness;

                float _ReceiveRealShadows;
                float _RealShadowStrength;

                float _PastelStrength;
                float _Brightness;
                float _Saturation;

                float4 _RimColor;
                float _RimStrength;
                float _RimPower;

                float _UseVertexColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                float fogFactor : TEXCOORD3;
            };

            float3 ApplySaturation(float3 color, float saturation)
            {
                float gray = dot(color, float3(0.299, 0.587, 0.114));
                return lerp(float3(gray, gray, gray), color, saturation);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalize(normalInputs.normalWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;

                OUT.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 atlasSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float4 baseCol = atlasSample * _BaseColor;

                if (_UseVertexColor > 0.5)
                {
                    baseCol *= IN.color;
                }

                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Calculate shadow coordinate per-pixel.
                // This is usually more stable for large/low-poly meshes
                // than calculating shadow coords per-vertex.
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Directional light angle.
                float ndotl = saturate(dot(normalWS, mainLight.direction));

                // Soft toon-style lighting ramp.
                float ramp = smoothstep(
                    _RampThreshold - _RampSoftness,
                    _RampThreshold + _RampSoftness,
                    ndotl
                );

                // Real Unity shadow map value.
                // 1 = fully lit, 0 = fully shadowed.
                float unityShadow = mainLight.shadowAttenuation;

                // Lets the material decide how strongly it receives real shadows.
                float realShadowInfluence = saturate(_ReceiveRealShadows * _RealShadowStrength);
                float controlledShadow = lerp(1.0, unityShadow, realShadowInfluence);

                // Stylized fake shadow color.
                float3 stylizedShadowBase = lerp(
                    baseCol.rgb,
                    baseCol.rgb * _ShadowColor.rgb,
                    _ShadowStrength
                );

                float3 ambient = baseCol.rgb * _AmbientColor.rgb * _AmbientStrength;
                float3 lit = baseCol.rgb * _LightColor.rgb * _LightStrength;

                // Fake stylized ramp lighting.
                float3 stylizedLight = lerp(stylizedShadowBase, lit, ramp);

                // Real shadow influence layered onto the fake stylized light.
                stylizedLight = lerp(stylizedShadowBase, stylizedLight, controlledShadow);

                float3 finalColor = ambient + stylizedLight;

                // Soft rim light for toy-like edge readability.
                float rim = 1.0 - saturate(dot(viewDirWS, normalWS));
                rim = pow(rim, _RimPower);
                finalColor += rim * _RimColor.rgb * _RimStrength;

                // Pastel treatment.
                // Gently lifts colors toward white without fully washing them out.
                finalColor = lerp(
                    finalColor,
                    lerp(finalColor, float3(1,1,1), 0.35),
                    _PastelStrength
                );

                finalColor = ApplySaturation(finalColor, _Saturation);
                finalColor *= _Brightness;

                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, baseCol.a);
            }

            ENDHLSL
        }

        // ============================================================
        // SHADOW CASTER PASS
        // Allows objects using this shader to CAST shadows.
        //
        // This version avoids ApplyShadowBias because some URP versions
        // do not expose that function in this context.
        // ============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    output.positionHCS.z = min(output.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionHCS.z = max(output.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}