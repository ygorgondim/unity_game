Shader "WizardRoyale/PickupOrb"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OrbTex ("Ability Orb Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
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
            sampler2D _OrbTex;

            half4 frag(v2f input) : SV_Target
            {
                // Bound explicitly per ability, independent of SpriteRenderer's shared _MainTex.
                half4 color = tex2D(_OrbTex, input.texcoord);
                // The supplied RGB artworks have black backdrops; the water art already has alpha.
                // Key only near-black pixels, preserving colored shadows, highlights and source alpha.
                half brightness = max(color.r, max(color.g, color.b));
                color.a *= smoothstep(0.015, 0.055, brightness);
                return color * input.color;
            }
            ENDCG
        }
    }
}
