Shader "UIEffect/EdgeEffect_Additive"
{
    Properties
    {
        [Header(Thickness)]
        _XThicknessScaleFactor("XThicknessScaleFactor", Range(0,10)) = 1.0

        [Header(Edge Type)]
        [Toggle(_IsCircular)] _IsCircular("IsCircular", float) = 0

        [Header(MainTex)]
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}

        [Header(UV Control)]
        _MainTexScaleOffset("MainTexScaleOffset", vector) = (1,1,0,0)
        _ScrollSpeed_X("ScrollSpeed_X", float) = 0.0
        _ScrollSpeed_Y("ScrollSpeed_Y", float) = 0.0
        _RadialUV_Power("RadialUV_Power", range(0,50)) = 1.0

        [Header(Rotation)]
        _InitialRotation("InitialRotation", float) = 0.0
        _RotationSpeed("RotationSpeed", float) = 0.0


        [Header(Color Control)]
        _ColorPower("ColorPower", float) = 1.0
        _ColorIntensity("ColorIntensity", float) = 1.0
        _HueShift("HueShift", Range(-180,180)) = 0

        [Header(ChromaticAberration)]
        [Toggle(_Use_Chromatic_Aberration)] _Use_Chromatic_Aberration("Use_Chromatic_Aberration", float) = 0
        [Space(10)]
        _ChromaticAberration_Opacity("ChromaticAberration_Opacity", range(0,1)) = 0.0
        _ChromaticAberration_Offset_R("ChromaticAberration_Offset_R", vector) = (0,0,0,0)
        _ChromaticAberration_Offset_G("ChromaticAberration_Offset_G", vector) = (0,0,0,0)
        _ChromaticAberration_Offset_B("ChromaticAberration_Offset_B", vector) = (0,0,0,0)

        [Header(Mask)]
        [Toggle(_Use_Mask)] _Use_Mask("UseMask", float) = 0
        [NoScaleOffset] _MaskTex("MaskTex", 2D) = "white"{}
        _MaskTexScaleOffset("MaskTexScaleOffset", vector) = (1,1,0,0)
        _MaskTex_ScrollSpeed_X("MaskTex_ScrollSpeed_X", float) = 0.0
        _MaskTex_ScrollSpeed_Y("MaskTex_ScrollSpeed_Y", float) = 0.0
        _Mask_Power("Mask_Power", float) = 1.0
        _Mask_Intensity("Mask_Intensity", float) = 1.0


   
        [Header(Mask2)]
        [Toggle(_Use_Mask2)] _Use_Mask2("UseMask2", float) = 0
        [NoScaleOffset] _MaskTex2("MaskTex2", 2D) = "white"{}
        _MaskTex2ScaleOffset("MaskTex2ScaleOffset", vector) = (1,1,0,0)
        _MaskTex2_ScrollSpeed_X("MaskTex2_ScrollSpeed_X", float) = 0.0
        _MaskTex2_ScrollSpeed_Y("MaskTex2_ScrollSpeed_Y", float) = 0.0
        _Mask2_Power("Mask2_Power", float) = 1.0
        _Mask2_Intensity("Mask2_Intensity", float) = 1.0

           


        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        ZTest[unity_GUIZTestMode]
        Blend One One
        Cull Off
        ZWrite Off

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
        }
    

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _Use_Mask
            #pragma shader_feature_local _Use_Mask2
            #pragma shader_feature_local _IsCircular
            #pragma shader_feature_local _Use_Chromatic_Aberration

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 edge_uv : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;

                #if UNITY_UI_CLIP_RECT
                    float4	rectMask    : TEXCOORD2;
                #endif
            };

            float _XThicknessScaleFactor;



            sampler2D _MainTex;
            float4 _MainTexScaleOffset;
            float _ScrollSpeed_X, _ScrollSpeed_Y;
            float _RadialUV_Power;


            float _HueShift;
            float _ColorIntensity;
            float _ColorPower;

            float _ActivationTime;

            #ifdef _Use_Mask
                sampler2D _MaskTex;
                float4 _MaskTexScaleOffset;
                float _MaskTex_ScrollSpeed_X;
                float _MaskTex_ScrollSpeed_Y;
                float _Mask_Power;
                float _Mask_Intensity;

            #endif

            #ifdef _Use_Mask2
                sampler2D _MaskTex2;
                float4 _MaskTex2ScaleOffset;
                float _MaskTex2_ScrollSpeed_X;
                float _MaskTex2_ScrollSpeed_Y;
                float _Mask2_Power;
                float _Mask2_Intensity;
 
            #endif

            #ifdef _Use_Chromatic_Aberration
                float _ChromaticAberration_Opacity;
                float2 _ChromaticAberration_Offset_R;
                float2 _ChromaticAberration_Offset_G;
                float2 _ChromaticAberration_Offset_B;
            #endif




            #ifdef UNITY_UI_CLIP_RECT
                float4 _ClipRect;
                float _UIMaskSoftnessX, _UIMaskSoftnessY;
            #endif


            float _InitialRotation;
            float _RotationSpeed;


            float3 ApplyHueShift(float3 aColor, float hue)
            {
                float angle = radians(hue);
                float3 k = float3(0.57735, 0.57735, 0.57735);
                float cosAngle = cos(angle);
                return aColor * cosAngle + cross(k, aColor) * sin(angle) + k * dot(k, aColor) * (1 - cosAngle);
            }
            float3x3 ZRotationMatrix(float degrees)
            {
                float alpha = degrees * 3.14 / 180.0;
                float s = sin(alpha);
                float c = cos(alpha);
                return float3x3(
                    c, -s, 0,
                    s, c, 0,
                    0, 0, 1);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float2 centered_uv = v.uv - 0.5;
                float3 rotated_uv = mul(ZRotationMatrix(_InitialRotation + _RotationSpeed * _Time.y), float3(centered_uv, 0.0));
                o.edge_uv = float2(rotated_uv.xy) + 0.5;

                o.uv = v.uv;
                o.color = v.color;

                #ifdef UNITY_UI_CLIP_RECT
                    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                    o.rectMask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * float2((_UIMaskSoftnessX + 1), (_UIMaskSoftnessY + 1))));
                #endif
              
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {

                float u = 0;
                float v = 0;


                
                float PI = 3.1415926535;
                float2 centered_uv = (i.edge_uv - 0.5) * 2.0;
 
                #ifdef _IsCircular
                    u = length(centered_uv);
                #else
                    u = max(pow(abs(centered_uv.x), _XThicknessScaleFactor), abs(centered_uv.y));
                #endif

                u = saturate(pow(u, _RadialUV_Power));
                v = (atan2(centered_uv.x, centered_uv.y) + PI) / (PI * 2.0);

          

             

                float2 uv = float2(u, v);
                float2 uv_dx = ddx(i.uv.xy);
                float2 uv_dy = ddy(i.uv.xy);
            
             

                float4 col = float4(1, 1, 1, 1);
                #ifdef _Use_Chromatic_Aberration
                    float col_r = tex2D(_MainTex, uv * _MainTexScaleOffset.xy
                        + _MainTexScaleOffset.zw
                        + float2(_ScrollSpeed_X, _ScrollSpeed_Y) * _Time.y, uv_dx, uv_dy + lerp(0.0, _ChromaticAberration_Offset_R, _ChromaticAberration_Opacity)).r;
                    float col_g = tex2D(_MainTex, uv * _MainTexScaleOffset.xy
                        + _MainTexScaleOffset.zw
                        + float2(_ScrollSpeed_X, _ScrollSpeed_Y) * _Time.y, uv_dx, uv_dy + lerp(0.0, _ChromaticAberration_Offset_G, _ChromaticAberration_Opacity)).g;
                    float col_b = tex2D(_MainTex, uv * _MainTexScaleOffset.xy
                        + _MainTexScaleOffset.zw
                        + float2(_ScrollSpeed_X, _ScrollSpeed_Y) * _Time.y, uv_dx, uv_dy + lerp(0.0, _ChromaticAberration_Offset_B, _ChromaticAberration_Opacity)).b;

                    col.rgb = float3(col_r, col_g, col_b);
                #else
                    col = tex2D(_MainTex, uv * _MainTexScaleOffset.xy
                        + _MainTexScaleOffset.zw
                        + float2(_ScrollSpeed_X, _ScrollSpeed_Y) * _Time.y, uv_dx, uv_dy);
                #endif




               

                col.rgb = pow(col.rgb, _ColorPower);
                col.rgb *= i.color.rgb* _ColorIntensity;
                col.rgb = ApplyHueShift(col.rgb, _HueShift);

                col.rgb *= i.color.a;
      


                #ifdef _Use_Mask
                    float mask = tex2D(_MaskTex, i.uv * _MaskTexScaleOffset.xy + _MaskTexScaleOffset.zw + 
                    float2(_MaskTex_ScrollSpeed_X, _MaskTex_ScrollSpeed_Y) * _Time.y).r;
                    mask = saturate(pow(mask, _Mask_Power) * _Mask_Intensity);
                    col.rgb *= mask;
      
                #endif


                #ifdef _Use_Mask2
                    float mask2 = tex2D(_MaskTex2, i.uv * _MaskTex2ScaleOffset.xy + _MaskTex2ScaleOffset.zw +
                    float2(_MaskTex2_ScrollSpeed_X, _MaskTex2_ScrollSpeed_Y) * _Time.y).r;
                    mask2 = saturate(pow(mask2, _Mask2_Power) * _Mask2_Intensity);
                    col.rgb *= mask2;
   
                #endif

                #if UNITY_UI_CLIP_RECT	
                    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.rectMask.xy)) * i.rectMask.zw);
                    col.rgb *= m.x * m.y;
                #endif


                return col;
            }
            ENDCG
        }
    }
}
