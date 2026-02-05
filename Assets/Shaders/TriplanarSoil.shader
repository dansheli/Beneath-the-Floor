Shader "BeneathTheFloor/TriplanarSoil"
{
    Properties
    {
        [Header(Dirt Layer)]
        _DirtTex ("Dirt Albedo", 2D) = "white" {}
        _DirtColor ("Dirt Color Tint", Color) = (0.45, 0.35, 0.25, 1)
        [Normal] _DirtBumpMap ("Dirt Normal Map", 2D) = "bump" {}

        [Header(Rock Layer)]
        _RockTex ("Rock Albedo", 2D) = "white" {}
        _RockColor ("Rock Color Tint", Color) = (0.4, 0.38, 0.35, 1)
        [Normal] _RockBumpMap ("Rock Normal Map", 2D) = "bump" {}

        [Header(Triplanar Settings)]
        _TilingScale ("Tiling Scale", Float) = 4.0
        _TriplanarSharpness ("Blend Sharpness", Range(1, 16)) = 4.0
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.4

        [Header(Layer Blending)]
        _WallRockBlendStrength ("Wall Rock Blend", Range(0, 3)) = 1.8
        _WallSteepnessThreshold ("Wall Steepness Threshold", Range(0, 1)) = 0.4

        [Header(PBR Settings)]
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        _Metallic ("Metallic", Range(0,1)) = 0.0

        [Header(Macro Variation)]
        _MacroScale ("Macro Scale", Range(0.01, 0.3)) = 0.08
        _MacroStrength ("Macro Strength", Range(0, 0.2)) = 0.06
        _MacroRoughnessVar ("Macro Roughness Variation", Range(0, 0.15)) = 0.03

        [Header(Detail Normal)]
        [Normal] _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailTiling ("Detail Tiling", Range(5, 30)) = 15.0
        _DetailNormalStrength ("Detail Normal Strength", Range(0, 1)) = 0.35

        [Header(Depth and Cavity)]
        _DepthDarkenStrength ("Depth Darken Strength", Range(0, 0.6)) = 0.3
        _DepthStart ("Depth Start Y", Float) = 0.0
        _DepthRange ("Depth Range", Float) = 50.0
        _CavityDarkenStrength ("Cavity Darken Strength", Range(0, 0.5)) = 0.25
        _CavityAOPower ("Cavity AO Power", Range(0.5, 3)) = 1.5

        [Header(Terrain Layers)]
        [Toggle] _UseLayerColors ("Enable Layer Colors", Float) = 1.0
        _Layer1Color ("Layer 1 - Topsoil", Color) = (0.45, 0.30, 0.18, 1)
        _Layer2Color ("Layer 2 - Clay", Color) = (0.65, 0.30, 0.08, 1)
        _Layer3Color ("Layer 3 - Gravel", Color) = (0.38, 0.36, 0.34, 1)
        _Layer4Color ("Layer 4 - Bedrock", Color) = (0.18, 0.18, 0.20, 1)
        _Layer1Depth ("Layer 1 End (Y)", Float) = -8.0
        _Layer2Depth ("Layer 2 End (Y)", Float) = -20.0
        _Layer3Depth ("Layer 3 End (Y)", Float) = -40.0
        _LayerBlendDistance ("Layer Blend Distance", Range(0.5, 5)) = 1.5
        _LayerColorStrength ("Layer Color Strength", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            TEXTURE2D(_DirtTex);
            TEXTURE2D(_DirtBumpMap);
            SAMPLER(sampler_DirtTex);
            SAMPLER(sampler_DirtBumpMap);

            TEXTURE2D(_RockTex);
            TEXTURE2D(_RockBumpMap);
            SAMPLER(sampler_RockTex);
            SAMPLER(sampler_RockBumpMap);

            TEXTURE2D(_DetailNormalMap);
            SAMPLER(sampler_DetailNormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _DirtColor;
                float4 _RockColor;
                float _TilingScale;
                float _TriplanarSharpness;
                float _NormalStrength;
                float _WallRockBlendStrength;
                float _WallSteepnessThreshold;
                float _Smoothness;
                float _Metallic;
                float _MacroScale;
                float _MacroStrength;
                float _MacroRoughnessVar;
                float _DetailTiling;
                float _DetailNormalStrength;
                float _DepthDarkenStrength;
                float _DepthStart;
                float _DepthRange;
                float _CavityDarkenStrength;
                float _CavityAOPower;
                // Layer colors
                float _UseLayerColors;
                float4 _Layer1Color;
                float4 _Layer2Color;
                float4 _Layer3Color;
                float4 _Layer4Color;
                float _Layer1Depth;
                float _Layer2Depth;
                float _Layer3Depth;
                float _LayerBlendDistance;
                float _LayerColorStrength;
            CBUFFER_END

            // Simple 3D noise function for macro variation
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3D(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                                 lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                                 lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            // Fractal brownian motion for smoother macro variation
            float fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * noise3D(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            float3 GetTriplanarWeights(float3 worldNormal)
            {
                float3 weights = abs(worldNormal);
                weights = pow(weights, _TriplanarSharpness);
                weights = weights / (weights.x + weights.y + weights.z + 0.001);
                return weights;
            }

            float4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 weights)
            {
                float2 uvX = worldPos.zy * _TilingScale;
                float2 uvY = worldPos.xz * _TilingScale;
                float2 uvZ = worldPos.xy * _TilingScale;

                float4 colX = SAMPLE_TEXTURE2D(tex, samp, uvX);
                float4 colY = SAMPLE_TEXTURE2D(tex, samp, uvY);
                float4 colZ = SAMPLE_TEXTURE2D(tex, samp, uvZ);

                return colX * weights.x + colY * weights.y + colZ * weights.z;
            }

            float3 SampleTriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 worldNormal, float3 weights, float strength, float tiling)
            {
                float2 uvX = worldPos.zy * tiling;
                float2 uvY = worldPos.xz * tiling;
                float2 uvZ = worldPos.xy * tiling;

                float3 tnormalX = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uvX), strength);
                float3 tnormalY = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uvY), strength);
                float3 tnormalZ = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uvZ), strength);

                // Swizzle to world space aligned
                float3 normalX = float3(0, tnormalX.y, -tnormalX.x * sign(worldNormal.x));
                float3 normalY = float3(tnormalY.x, 0, tnormalY.y * sign(worldNormal.y));
                float3 normalZ = float3(tnormalZ.x * sign(worldNormal.z), tnormalZ.y, 0);

                float3 result = normalize(
                    worldNormal +
                    normalX * weights.x +
                    normalY * weights.y +
                    normalZ * weights.z
                );

                return result;
            }

            float GetLayerBlend(float3 worldNormal)
            {
                float flatness = abs(worldNormal.y);
                float wallness = 1.0 - flatness;
                float blend = saturate((wallness - _WallSteepnessThreshold) / (1.0 - _WallSteepnessThreshold + 0.001));
                blend = pow(blend, 0.7) * _WallRockBlendStrength;
                return saturate(blend);
            }

            // Calculate approximate cavity/concavity from normal
            float GetCavityFactor(float3 worldNormal, float3 worldPos)
            {
                // Surfaces facing down or inward tend to be cavities
                float downFacing = saturate(-worldNormal.y);

                // Add some noise-based variation to break up uniformity
                float cavityNoise = fbm(worldPos * 0.5) * 0.3;

                // Combine for cavity factor
                float cavity = downFacing * 0.7 + cavityNoise;
                return saturate(cavity);
            }

            // Calculate layer color based on world Y position
            // Returns blended color between layers with smooth transitions
            float4 GetLayerColor(float worldY)
            {
                // Smooth blend between layers
                float blend = _LayerBlendDistance;

                // Layer 1 (Topsoil) - above Layer1Depth
                if (worldY > _Layer1Depth + blend)
                {
                    return _Layer1Color;
                }
                // Transition from Layer 1 to Layer 2
                else if (worldY > _Layer1Depth - blend)
                {
                    float t = saturate((_Layer1Depth + blend - worldY) / (blend * 2.0));
                    return lerp(_Layer1Color, _Layer2Color, t);
                }
                // Layer 2 (Clay) - between Layer1Depth and Layer2Depth
                else if (worldY > _Layer2Depth + blend)
                {
                    return _Layer2Color;
                }
                // Transition from Layer 2 to Layer 3
                else if (worldY > _Layer2Depth - blend)
                {
                    float t = saturate((_Layer2Depth + blend - worldY) / (blend * 2.0));
                    return lerp(_Layer2Color, _Layer3Color, t);
                }
                // Layer 3 (Gravel) - between Layer2Depth and Layer3Depth
                else if (worldY > _Layer3Depth + blend)
                {
                    return _Layer3Color;
                }
                // Transition from Layer 3 to Layer 4
                else if (worldY > _Layer3Depth - blend)
                {
                    float t = saturate((_Layer3Depth + blend - worldY) / (blend * 2.0));
                    return lerp(_Layer3Color, _Layer4Color, t);
                }
                // Layer 4 (Bedrock) - below Layer3Depth
                else
                {
                    return _Layer4Color;
                }
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                // Flip normal for backfaces so lighting works correctly
                float3 normalWS_adjusted = isFrontFace ? input.normalWS : -input.normalWS;

                float3 weights = GetTriplanarWeights(normalWS_adjusted);

                // Sample base textures
                float4 dirtAlbedo = SampleTriplanar(TEXTURE2D_ARGS(_DirtTex, sampler_DirtTex), input.positionWS, weights);
                dirtAlbedo *= _DirtColor;

                float4 rockAlbedo = SampleTriplanar(TEXTURE2D_ARGS(_RockTex, sampler_RockTex), input.positionWS, weights);
                rockAlbedo *= _RockColor;

                // Layer blend based on surface angle
                float layerBlend = GetLayerBlend(normalWS_adjusted);
                float4 albedo = lerp(dirtAlbedo, rockAlbedo, layerBlend);

                // === TERRAIN LAYER COLORS ===
                if (_UseLayerColors > 0.5)
                {
                    float4 layerColor = GetLayerColor(input.positionWS.y);
                    // Blend toward layer color directly (not multiply) for visible difference
                    // Preserve texture detail by mixing: 50% direct color + 50% tinted texture
                    float3 directColor = layerColor.rgb;
                    float3 tintedColor = albedo.rgb * layerColor.rgb * 2.5;
                    float3 targetColor = lerp(tintedColor, directColor, 0.4);
                    albedo.rgb = lerp(albedo.rgb, targetColor, _LayerColorStrength);
                }

                // === MACRO VARIATION ===
                float macroNoise = fbm(input.positionWS * _MacroScale);
                float macroVar = (macroNoise - 0.5) * 2.0 * _MacroStrength;
                albedo.rgb *= (1.0 + macroVar);

                // Macro roughness variation
                float roughnessVar = (macroNoise - 0.5) * _MacroRoughnessVar;

                // === NORMALS ===
                // Main triplanar normals
                float3 dirtNormal = SampleTriplanarNormal(TEXTURE2D_ARGS(_DirtBumpMap, sampler_DirtBumpMap),
                    input.positionWS, normalWS_adjusted, weights, _NormalStrength, _TilingScale);
                float3 rockNormal = SampleTriplanarNormal(TEXTURE2D_ARGS(_RockBumpMap, sampler_RockBumpMap),
                    input.positionWS, normalWS_adjusted, weights, _NormalStrength, _TilingScale);
                float3 baseNormal = normalize(lerp(dirtNormal, rockNormal, layerBlend));

                // === DETAIL NORMAL ===
                float3 detailNormal = SampleTriplanarNormal(TEXTURE2D_ARGS(_DetailNormalMap, sampler_DetailNormalMap),
                    input.positionWS, normalWS_adjusted, weights, _DetailNormalStrength, _DetailTiling);

                // Blend detail normal with base normal using reoriented normal mapping approach
                float3 normalWS = normalize(baseNormal + (detailNormal - normalWS_adjusted) * _DetailNormalStrength);

                // === DEPTH DARKENING ===
                float depthFactor = saturate((_DepthStart - input.positionWS.y) / _DepthRange);
                float depthDarken = 1.0 - (depthFactor * _DepthDarkenStrength);
                albedo.rgb *= depthDarken;

                // === CAVITY DARKENING ===
                float cavityFactor = GetCavityFactor(normalWS_adjusted, input.positionWS);
                float cavityAO = 1.0 - (pow(cavityFactor, _CavityAOPower) * _CavityDarkenStrength);
                albedo.rgb *= cavityAO;

                // === AMBIENT OCCLUSION ===
                // Base AO from surface orientation
                float ao = lerp(0.75, 1.0, saturate(dot(normalWS_adjusted, float3(0, 1, 0)) * 0.5 + 0.5));
                // Combine with cavity AO
                ao *= cavityAO;

                // === PBR SETUP ===
                float finalSmoothness = saturate(_Smoothness + roughnessVar);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = float3(0, 0, 0);
                surfaceData.smoothness = finalSmoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.emission = float3(0, 0, 0);
                surfaceData.occlusion = ao;
                surfaceData.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;
            float3 _LightPosition;

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                return positionCS;
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
