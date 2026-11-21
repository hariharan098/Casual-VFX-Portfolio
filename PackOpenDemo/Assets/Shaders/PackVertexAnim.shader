// UI shader with procedural vertex animation, used for the pack tear.
// All offsets are in object units (canvas pixels for a UI Image) and are measured at the far
// edge of the mesh (UV distance 1 from the pivot). Pair with UIMeshSubdivide so bend/wave have vertices to move.
//
//  _Skew      x += _Skew  * dY            horizontal shear (top of a pack slides sideways)
//  _SkewY     y += _SkewY * dX            vertical shear
//  _BendX     x += _BendX * dY^2          bend along height
//  _BendY     y += _BendY * dX^2          bend along length (peel curl of the strip)
//  _WaveAmpX  x += _WaveAmpX * sin(2pi(v*_WaveFreq + _WavePhase)) * dY   flutter along height
//  _WaveAmpY  y += _WaveAmpY * sin(2pi(u*_WaveFreq + _WavePhase)) * dX   flutter along length
// where dX = uv.x - _PivotX, dY = uv.y - _PivotY (pivot is the part that stays attached).
// Do not put these sprites in an atlas: the shader assumes UV spans 0..1 across the rect.
Shader "PackOpen/UI/PackVertexAnim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _PivotX ("Pivot X (uv)", Range(0,1)) = 0.5
        _PivotY ("Pivot Y (uv)", Range(0,1)) = 0

        _Skew ("Skew X", Float) = 0
        _SkewY ("Skew Y", Float) = 0
        _BendX ("Bend X", Float) = 0
        _BendY ("Bend Y", Float) = 0
        _WaveAmpX ("Wave Amp X", Float) = 0
        _WaveAmpY ("Wave Amp Y", Float) = 0
        _WaveFreq ("Wave Frequency", Float) = 2
        _WavePhase ("Wave Phase", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _PivotX, _PivotY;
            float _Skew, _SkewY, _BendX, _BendY;
            float _WaveAmpX, _WaveAmpY, _WaveFreq, _WavePhase;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float dX = v.texcoord.x - _PivotX;
                float dY = v.texcoord.y - _PivotY;
                const float TAU = 6.2831853;

                float4 p = v.vertex;
                p.x += _Skew * dY;
                p.y += _SkewY * dX;
                p.x += _BendX * dY * dY;
                p.y += _BendY * dX * dX;
                p.x += _WaveAmpX * sin(TAU * (v.texcoord.y * _WaveFreq + _WavePhase)) * dY;
                p.y += _WaveAmpY * sin(TAU * (v.texcoord.x * _WaveFreq + _WavePhase)) * dX;

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(p);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
