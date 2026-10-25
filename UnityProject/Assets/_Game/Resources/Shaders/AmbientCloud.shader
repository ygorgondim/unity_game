Shader "WizardRoyale/AmbientCloud"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cloud Texture", 2D) = "white" {}
        _IslandTex ("Island Mask", 2D) = "black" {}
        _IslandRect ("Island World Rect", Vector) = (0,0,1,1)
        _WaterOnly ("Only Over Water", Float) = 1
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
            #pragma vertex AmbientVertex
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "AmbientWaterMask.cginc"
            half _WaterOnly;

            half4 frag(AmbientVaryings input) : SV_Target
            {
                half4 cloud = SampleSpriteTexture(input.texcoord) * input.color;
                cloud.a *= lerp(1.0, ArenaWaterMask(input.world), saturate(_WaterOnly));
                return cloud;
            }
            ENDCG
        }
    }
}
