Shader "WizardRoyale/FloatingRobe"
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
        [PerRendererData] _ShadowMode ("Shadow Form", Float) = 0
        [PerRendererData] _ShadowEyeA ("Eye A Bounds", Vector) = (0,0,0,0)
        [PerRendererData] _ShadowEyeB ("Eye B Bounds", Vector) = (0,0,0,0)
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
            float _ShadowMode;
            float4 _ShadowEyeA, _ShadowEyeB;

            float EyeMask(float2 uv, float4 bounds)
            {
                return step(bounds.x, uv.x) * step(uv.x, bounds.z)
                    * step(bounds.y, uv.y) * step(uv.y, bounds.w);
            }

            half4 frag(v2f input) : SV_Target
            {
                float2 uv = FloatingHemUV(input.texcoord);
                half4 color = SampleSpriteTexture(uv);
                if (_ShadowMode > 0.5)
                {
                    // Exact eye regions per character/facing; rear poses have no visible eye regions.
                    color.rgb *= max(EyeMask(uv, _ShadowEyeA), EyeMask(uv, _ShadowEyeB));
                }
                return color * input.color;
            }
            ENDCG
        }
    }
}
