Shader "WizardRoyale/WhiteLightWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Brightness ("HDR brightness", Float) = 1.55
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
            float _Fade, _Age, _Phase, _Brightness;
            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.texcoord * 2 - 1;
                float side = 1-smoothstep(0.55,0.96,abs(p.y));
                float flow = sin(p.y*9-_Age*24+_Phase)*0.032
                    + sin(p.y*21+_Age*17+_Phase)*0.012;
                float front = 0.36-0.30*p.y*p.y+flow;
                float d = p.x-front;
                float aa = max(0.025,fwidth(p.x)*0.7);
                float crest = exp(-pow(d/aa,2));
                float rippleA = exp(-pow((d+0.17+0.035*sin(p.y*8-_Age*19))/max(0.018,aa*0.65),2));
                float rippleB = exp(-pow((d+0.43+0.05*sin(p.y*6-_Age*15+_Phase))/max(0.025,aa*0.8),2));
                float rippleC = exp(-pow((d+0.67+0.055*sin(p.y*7-_Age*13+_Phase))/max(0.030,aa*0.85),2));
                float softBody = exp(-pow((d+0.18)/0.26,2));
                float haze = exp(-pow((d+0.25)/0.50,2))*0.15;
                float light = crest*2.0+rippleA*0.75+rippleB*0.38+rippleC*0.18+softBody*0.28+haze;
                float wake = 0.85+0.15*sin(p.y*15-_Age*31+_Phase);
                float birth = smoothstep(0,0.035,_Age);
                return float4(float3(1,1,1)*light*_Brightness*i.color.rgb,side*wake*birth*_Fade*i.color.a);
            }
            ENDCG
        }
    }
}
