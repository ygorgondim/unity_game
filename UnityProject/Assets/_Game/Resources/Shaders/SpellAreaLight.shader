Shader "WizardRoyale/SpellAreaLight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("HDR brightness", Float) = 2
        _Fill ("Soft inner light", Float) = 0.06
        _Edge ("Boundary shimmer", Float) = 0.24
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            float _Fill, _Edge, _Intensity;
            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.texcoord * 2 - 1;
                float r = length(p);
                float angle = atan2(p.y,p.x);
                float inside = 1 - smoothstep(0.98,1,r);
                float haze = exp(-r*r*2.5) * _Fill;
                // A feathered glimmer inside the true radius, never a thick opaque stroke.
                float edge = exp(-pow((r-0.958)/0.026,2));
                float shimmer = 0.42 + 0.35 * pow(0.5+0.5*sin(angle*9+_Time.y*1.2),3);
                return float4(i.color.rgb * _Intensity, (haze + edge*shimmer*_Edge)*inside*i.color.a);
            }
            ENDCG
        }
    }
}
