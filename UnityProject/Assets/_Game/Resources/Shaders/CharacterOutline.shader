Shader "WizardRoyale/CharacterOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _OutlineUV ("Outline UV Width", Vector) = (0.01,0.01,0,0)
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _HemClock ("Hem Time", Float) = 0
        [PerRendererData] _HemPhase ("Hem Phase", Float) = 0
        [PerRendererData] _HemStrength ("Hem Movement", Float) = 0
        [PerRendererData] _HemDrag ("Hem Lateral Drag", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            #include "FloatingHem.cginc"
            float4 _OutlineUV;

            half CharacterAlpha(float2 uv)
            {
                return SampleSpriteTexture(FloatingHemUV(uv)).a;
            }

            half DilatedAlpha(float2 uv, float2 width)
            {
                half alpha = CharacterAlpha(uv + float2(width.x, 0));
                alpha = max(alpha, CharacterAlpha(uv - float2(width.x, 0)));
                alpha = max(alpha, CharacterAlpha(uv + float2(0, width.y)));
                alpha = max(alpha, CharacterAlpha(uv - float2(0, width.y)));
                float2 diagonal = width * 0.707107;
                alpha = max(alpha, CharacterAlpha(uv + diagonal));
                alpha = max(alpha, CharacterAlpha(uv - diagonal));
                alpha = max(alpha, CharacterAlpha(uv + float2(diagonal.x, -diagonal.y)));
                return max(alpha, CharacterAlpha(uv + float2(-diagonal.x, diagonal.y)));
            }

            half4 frag(v2f input) : SV_Target
            {
                half original = CharacterAlpha(input.texcoord);
                half edge = saturate(DilatedAlpha(input.texcoord, _OutlineUV.xy) - original);
                half halo = saturate(DilatedAlpha(input.texcoord, _OutlineUV.xy * 2.4) - original) * 0.20;
                half alpha = max(edge, halo) * input.color.a;
                return half4(input.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
