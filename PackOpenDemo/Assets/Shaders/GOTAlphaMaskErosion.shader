//
// Low variant port of CityX/Particle/UberShader which was started form 
// UnityStandardParticles.cginc and associated code, see https://forum.unity.com/threads/release-standard-particle-shader.461938/


Shader "GOTRPG/Particle/Alpha_Mask_Erosion"
{
    Properties
    {
        _MainTex("Main", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _ColorAdd("Color Add", Color) = (0,0,0,0)
		_ColorBoost("Color Boost", Range(0.0, 1000.0)) = 1.0
		_AlphaBoost("Alpha Boost", Range(0.0, 255.0)) = 1.0

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

		_AddBlendIntensity("AddBlend Blend Mode Intensity", Float) = 1
		_AddBlendAlphaBlend("AddBlend Blend Mode Alpha Blend", Float) = 0.5

		_MainTexScroll("Main Scroll", Vector) = (0,0,0,0)
		_MainTexRotation("Main Rotation", Vector) = (0,0.5,0.5,0)

		_MaskTex("Mask", 2D) = "white" {}
		_MaskChannel("Mask Channel", Float) = 3.0

		_MaskTexScroll("Mask Scroll", Vector) = (0,0,0,0)
		_MaskTexRotation("Mask Rotation", Vector) = (0,0.5,0.5,0)

		_AlphaErosionTex("Alpha Erosion", 2D) = "white" {}
		_AlphaErosionFactor("Alpha Erosion Factor", Range(0.0, 1.0)) = 1.0
		_AlphaErosionChannel("Alpha Erosion Channel", Float) = 3.0
		_AlphaErosionTexScroll("Alpha Erosion Scroll", Vector) = (0,0,0,0)
		_AlphaErosionTexRotation("Alpha Erosion Rotation", Vector) = (0,0.5,0.5,0)

        // Hidden properties. Enum + Float matches GOTParticleStandardUnlit so Unity 6
        // Vulkan applies Blend [_SrcBlend] [_DstBlend] from serialized material floats.
        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _ColorMode ("__colormode", Float) = 0.0
        [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("__src", Float) = 5.0
        [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("__dst", Float) = 10.0
        [HideInInspector] _ZWrite ("__zw", Float) = 0.0
        [HideInInspector] _Cull ("__cull", Float) = 2.0
        [HideInInspector] _ColorAddSubDiff ("__coloraddsubdiff", Vector) = (0,0,0,0)
		[HideInInspector] _MaskEnabled("__maskenabled", Float) = 0.0
		[HideInInspector] _AlphaErosionMaskEnabled("__alphaeroisionmaskenabled", Float) = 0.0
		[HideInInspector] _MaskChannelParams("__maskchannelparams", Vector) = (0,0,0,0)
		[HideInInspector] _TexRotationEnabled("__texrotationenabled", Float) = 0.0
		[HideInInspector] _AlphaErosionChannelParams("__alphaerosionchannelparams", Vector) = (0,0,0,0)
    	
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
    	
    	_ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" "PerformanceChecks"="False" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        ZTest LEqual
        Cull [_Cull]
        ColorMask [_ColorMask]
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0

            #pragma multi_compile_local _ _ADDBLEND_ON
            #pragma multi_compile_local _ _ROTATION_ON
            #pragma multi_compile_local _ _MASK_ON
            #pragma multi_compile_local _ _ALPHAEROSION_MASK
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingSetup

            #pragma vertex vertParticleUnlit
            #pragma fragment fragParticleUnlit

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "UnityStandardParticleInstancing.cginc"

            struct appdata_particles
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 texcoords : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexOutput
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
#if defined(_MASK_ON)
                float2 maskTexcoord : TEXCOORD1;
#endif
                float3 alphaErosionTexcoord : TEXCOORD2;
                float4 alphaErosionChannel : TEXCOORD3;
                float4 objectPos : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half4 _ColorAdd;
            half _ColorBoost;
            half _AlphaBoost;
            float4 _MainTexScroll;
            float4 _MainTexRotation;
            float4 _ClipRect;

#if defined(_MASK_ON)
            sampler2D _MaskTex;
            float4 _MaskTex_ST;
            float4 _MaskTexScroll;
            half4 _MaskChannelParams;
    #if defined(_ROTATION_ON)
            float4 _MaskTexRotation;
    #endif
#endif

            float _AlphaErosionFactor;
            float4 _AlphaErosionChannelParams;
#if defined(_ALPHAEROSION_MASK)
            sampler2D _AlphaErosionTex;
            float4 _AlphaErosionTex_ST;
            float4 _AlphaErosionTexScroll;
    #if defined(_ROTATION_ON)
            float4 _AlphaErosionTexRotation;
    #endif
#endif

#if defined(_ADDBLEND_ON)
            half _AddBlendIntensity;
            half _AddBlendAlphaBlend;
#endif

            // Do not pass sampler2D into the vertex shader. Unity 6 Vulkan fails
            // vertex compilation with "Unrecognized sampler" and the shader then
            // falls back to UnlitTransparentColoredFade (SrcAlpha OneMinusSrcAlpha).
#if defined(_ROTATION_ON)
            float2 VertTexcoordRotation(float4 st, float2 uv, float4 scroll, float4 rotation)
            {
                float2 incoords = uv;
                incoords -= rotation.yz;
                float s, c;
                sincos(radians(rotation.w) + radians(rotation.x) * _Time.y, s, c);
                float2x2 rotationMatrix = float2x2(c, -s, s, c);
                incoords = mul(incoords, rotationMatrix);
                incoords += rotation.yz;
                return incoords.xy * st.xy + st.zw + frac(scroll.xy * _Time.y);
            }
#endif

            float2 VertTexcoord(float4 st, float2 uv, float4 scroll)
            {
                return uv * st.xy + st.zw + frac(scroll.xy * _Time.y);
            }

            VertexOutput vertParticleUnlit (appdata_particles v)
            {
                VertexOutput o;
                UNITY_INITIALIZE_OUTPUT(VertexOutput, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.color = v.color;
                vertInstancingColor(o.color);

                float2 particleUV = v.texcoords;
#ifdef UNITY_PARTICLE_INSTANCING_ENABLED
                vertInstancingUVs(v.texcoords, particleUV);
#endif

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.objectPos = v.vertex;

#if defined(_ADDBLEND_ON)
                o.color.rgb *= _AddBlendIntensity;
#endif

#if defined(_ROTATION_ON)
                o.texcoord = VertTexcoordRotation(_MainTex_ST, particleUV, _MainTexScroll, _MainTexRotation);
#else
                o.texcoord = VertTexcoord(_MainTex_ST, particleUV, _MainTexScroll);
#endif

#if defined(_MASK_ON)
    #if defined(_ROTATION_ON)
                o.maskTexcoord = VertTexcoordRotation(_MaskTex_ST, particleUV, _MaskTexScroll, _MaskTexRotation);
    #else
                o.maskTexcoord = VertTexcoord(_MaskTex_ST, particleUV, _MaskTexScroll);
    #endif
#endif

                float alpha = 1.0f - o.color.a * _AlphaErosionFactor;
                o.alphaErosionTexcoord.z = 1.0f - 2.0f * alpha;
                o.alphaErosionChannel = _AlphaErosionChannelParams * alpha;
#if defined(_ALPHAEROSION_MASK)
    #if defined(_ROTATION_ON)
                o.alphaErosionTexcoord.xy = VertTexcoordRotation(_AlphaErosionTex_ST, particleUV, _AlphaErosionTexScroll, _AlphaErosionTexRotation);
    #else
                o.alphaErosionTexcoord.xy = VertTexcoord(_AlphaErosionTex_ST, particleUV, _AlphaErosionTexScroll);
    #endif
#else
                o.alphaErosionTexcoord.xy = 0.0f;
#endif
                return o;
            }

            half4 fragParticleUnlit (VertexOutput IN) : SV_Target
            {
                half4 albedo = tex2D(_MainTex, IN.texcoord) + _ColorAdd;
                albedo *= half4(_Color.rgb * _ColorBoost, _Color.a * _AlphaBoost);
                albedo *= IN.color;

#if defined(_ADDBLEND_ON)
                albedo.rgb *= albedo.a;
                albedo.a *= _AddBlendAlphaBlend;
#endif

                half4 result = albedo;

#if defined(_ALPHAEROSION_MASK)
                float dissolve = dot(tex2D(_AlphaErosionTex, IN.alphaErosionTexcoord.xy), IN.alphaErosionChannel);
#else
                float dissolve = dot(result, IN.alphaErosionChannel);
#endif
                result.a *= max(0.0f, dissolve + IN.alphaErosionTexcoord.z);

#if defined(_MASK_ON)
                half mask = dot(tex2D(_MaskTex, IN.maskTexcoord), _MaskChannelParams);
                result.a *= mask;
#endif

#ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.objectPos.xy, _ClipRect);
#endif

                return result;
            }
            ENDCG
        }
    }

    Fallback "Mobile/Particles/Additive"
    CustomEditor "GOTAlphaMaskErosionGUI"
}
