Shader "WizardRoyale/WhiteSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
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

            fixed4 frag(v2f input) : SV_Target
            {
                fixed alpha = SampleSpriteTexture(FloatingHemUV(input.texcoord)).a * input.color.a;
                return fixed4(1, 1, 1, alpha);
            }
            ENDCG
        }
    }
}
