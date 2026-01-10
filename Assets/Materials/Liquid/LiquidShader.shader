Shader "Custom/LiquidShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.6, 0.9, 0.5)
        _ShallowColor ("Shallow Color", Color) = (0.3, 0.7, 1.0, 0.6)
        _DeepColor ("Deep Color", Color) = (0.0, 0.2, 0.5, 0.7)
        _Transparency ("Transparency", Range(0.1, 1)) = 0.5
        _WaveAmplitude ("Wave Amplitude", Float) = 0.1
        _WaveSpeed ("Wave Speed", Float) = 2.0
        _FresnelPower ("Fresnel Power", Float) = 5.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 300
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShallowColor;
                float4 _DeepColor;
                float _Transparency;
                float _WaveAmplitude;
                float _WaveSpeed;
                float _FresnelPower;
                float _Smoothness;
            CBUFFER_END
            
            // Скорость корабля
            float _ShipVelocityMagnitude;
            float _ShipVelocityX;
            float _ShipVelocityY;
            float _ShipVelocityZ;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float fillLevel : TEXCOORD3;
            };
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 positionOS = IN.positionOS.xyz;
                
                // Определяем заполнение жидкости
                float normalizedHeight = positionOS.y;
                float fillLevel = saturate((normalizedHeight + 1.0) / 2.0);
                
                // Волны только в верхней половине
                float upperHalfMask = step(0.5, fillLevel);
                
                // Амплитуда и скорость зависят от скорости корабля (уменьшено влияние)
                float velocityMagnitude = _ShipVelocityMagnitude;
                float dynamicAmplitude = _WaveAmplitude * (1.0 + velocityMagnitude * 0.3f);
                float dynamicSpeed = _WaveSpeed * (1.0 + velocityMagnitude * 0.2f);
                
                // Волны с учётом направления движения (уменьшено влияние)
                float waveX = sin((positionWS.x + _ShipVelocityX * _Time.y * 0.1f + _Time.y * dynamicSpeed) * 3.0) * dynamicAmplitude;
                float waveZ = sin((positionWS.z + _ShipVelocityZ * _Time.y * 0.1f + _Time.y * dynamicSpeed) * 3.0) * dynamicAmplitude;
                float wave = (waveX + waveZ) * 0.5;
                
                // Применяем волны только в верхней половине (уменьшена интенсивность)
                positionWS.y += wave * upperHalfMask * 0.03f;
                
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.worldPos = positionWS;
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDir = normalize(_WorldSpaceCameraPos - positionWS);
                OUT.fillLevel = fillLevel;
                
                return OUT;
            }
            
            float4 frag(Varyings IN) : SV_Target
            {
                // Глубина жидкости
                float depth = saturate(IN.worldPos.y * 0.5 + 0.5);
                
                // Цвет с учётом глубины
                float4 depthColor = lerp(_DeepColor, _ShallowColor, depth);
                
                // Нормаль
                float3 normal = normalize(IN.worldNormal);
                
                // Fresnel эффект
                float fresnel = pow(1.0 - saturate(dot(normal, IN.viewDir)), _FresnelPower);
                fresnel *= step(0.5, IN.fillLevel);
                
                // Освещение
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float ndotl = max(0.0, dot(normal, lightDir));
                
                // Финальный цвет
                float4 finalColor = depthColor;
                finalColor.rgb += mainLight.color * ndotl * 0.3;
                finalColor.rgb += fresnel * 0.4;
                
                // НОВОЕ: Используем слайдер Transparency для контроля альфы
                finalColor.a = _Transparency;
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    Fallback "Transparent/Vertex Colored"
}
