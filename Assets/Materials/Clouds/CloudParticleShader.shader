Shader "Custom/CloudParticle"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}
        _Color ("Cloud Color", Color) = (1, 1, 1, 1)
        _SoftParticleFadeDistance ("Soft Particle Fade", Float) = 1.0
        _Density ("Density", Range(0.1, 2.0)) = 1.0
        _EdgeFade ("Edge Fade Softness", Range(0, 1)) = 0.3
    }
    
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGBA
        Cull Back
        Lighting Off
        ZWrite Off
        ZTest LEqual
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            fixed4 _Color;
            float _SoftParticleFadeDistance;
            float _Density;
            float _EdgeFade;
            
            #ifdef SOFTPARTICLES_ON
            sampler2D _CameraDepthTexture;
            #endif
            
            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                #ifdef SOFTPARTICLES_ON
                float4 projPos : TEXCOORD2;
                #endif
            };
            
            float4 _MainTex_ST;
            
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                #ifdef SOFTPARTICLES_ON
                o.projPos = ComputeScreenPos(o.vertex);
                COMPUTE_EYEDEPTH(o.projPos.z);
                #endif
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord);
                col *= _Color;
                col *= i.color;
                
                // Плотность облака
                col.a *= _Density;
                
                // Мягкие края - облако выглядит пушистым
                float2 center = i.texcoord - 0.5;
                float dist = length(center);
                col.a *= 1.0 - smoothstep(0.3, 0.5 + _EdgeFade, dist);
                
                #ifdef SOFTPARTICLES_ON
                float sceneZ = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, i.projPos).r);
                float partZ = i.projPos.z;
                col.a *= saturate(_SoftParticleFadeDistance * (sceneZ - partZ));
                #endif
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
}