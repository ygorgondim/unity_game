Shader "WizardRoyale/ShadowBubble"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _BubblePhase ("Shape Seed", Float) = 0
        [PerRendererData] _Smoke ("Soft Smoke", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            float _BubblePhase, _Smoke;
            half4 frag(v2f input) : SV_Target
            {
                float2 p = input.texcoord * 2.0 - 1.0;
                float angle = atan2(p.y, p.x);
                float clock = _Time.y;
                float radius = 0.76 + sin(angle * 3.0 + clock * 2.7 + _BubblePhase) * 0.075
                    + sin(angle * 5.0 - clock * 1.9 + _BubblePhase * 2.3) * 0.045;
                float softness = lerp(0.018, 0.38, _Smoke);
                float alpha = 1.0 - smoothstep(radius - softness, radius, length(p));
                return half4(input.color.rgb, input.color.a * alpha);
            }
            ENDCG
        }
    }
}
