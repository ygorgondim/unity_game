Shader "WizardRoyale/VioletCrescent"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Reveal ("Sweep reveal", Range(0,1.1)) = 1.1
        _Fade ("Fade", Range(0,1)) = 1
        _Age ("Attack age", Float) = 0
        _Phase ("Individual phase", Float) = 0
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex SpriteVert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            float _Reveal, _Fade, _Age, _Phase;

            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.texcoord * 2 - 1;
                float2 q = (p - float2(-0.18,0.02)) / float2(1,0.95);
                float r = length(q);
                float angle = atan2(q.y,q.x);
                // Clockwise crescent: a long tapered tail and a broad bright cutting end.
                float u = (2.75-angle)/3.95;
                float aa = clamp(fwidth(r)*0.7,0.004,0.035);
                float radial = r - (0.72+0.006*sin(angle*7-_Age*12+_Phase));
                float thickness = 0.012+0.145*pow(saturate(u),1.4);
                float tips = 0.035*sin(radial*185+_Phase)+0.018*sin(radial*315);
                float arc = smoothstep(0,0.10,u)
                    * (1-smoothstep(0.94+tips,1.015+tips,u));
                float reveal = 1-smoothstep(_Reveal-0.08,_Reveal+0.02,u);
                float ribbon = smoothstep(-thickness-aa,-thickness+aa,radial)
                    * (1-smoothstep(-aa,aa,radial));
                float head = smoothstep(0.25,0.88,u);
                float outerLine = exp(-pow(radial/max(0.012,aa),2));
                float satellite = exp(-pow((radial-0.036)/max(0.006,aa*0.55),2))*0.55;
                float innerLine = exp(-pow((radial+thickness*0.82)/max(0.009,aa*0.6),2))
                    * smoothstep(0.5,0.85,u);
                float glow = exp(-abs(radial+thickness*0.25)*16)*0.22;
                float3 violet = float3(0.20,0.025,0.72);
                float3 pink = float3(1.15,0.30,1.65);
                float3 body = lerp(violet,pink,head*0.85);
                float core = pow(saturate((radial+thickness)/max(thickness,0.001)),0.6)*head;
                float3 color = ribbon*(body+float3(1.6,1.4,1.9)*core)
                    + float3(1.8,1.5,2.2)*outerLine*(0.25+head)
                    + float3(0.90,0.40,1.7)*satellite
                    + float3(1.6,0.75,2)*innerLine
                    + float3(0.40,0.07,1)*glow;
                return float4(color*i.color.rgb,arc*reveal*_Fade*i.color.a);
            }
            ENDCG
        }
    }
}
