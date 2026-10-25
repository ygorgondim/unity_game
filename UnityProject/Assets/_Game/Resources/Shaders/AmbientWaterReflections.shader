Shader "WizardRoyale/AmbientWaterReflections"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _IslandTex ("Island Mask", 2D) = "black" {}
        _IslandRect ("Island World Rect", Vector) = (0,0,1,1)
        _AmbientTime ("Ambient Time", Float) = 0
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
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex AmbientVertex
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "AmbientWaterMask.cginc"
            float _AmbientTime;

            float CellRandom(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 frag(AmbientVaryings input) : SV_Target
            {
                float2 water = input.world;
                water.x += sin(water.y * 0.75 + _AmbientTime * 0.18) * 0.10;
                water.y += _AmbientTime * 0.018;
                float2 grid = water * float2(3.2, 11.0);
                float2 cell = floor(grid);
                float2 local = frac(grid) - 0.5;
                float seed = CellRandom(cell);
                float phase = frac(_AmbientTime * (0.12 + seed * 0.10) + seed * 9.0);
                float fade = pow(sin(phase * UNITY_PI), 2.0);
                float ripple = pow(saturate(1.0 - abs(local.x) * 2.0), 1.5)
                    * pow(saturate(1.0 - abs(local.y) * 3.5), 3.0);
                ripple *= smoothstep(0.62, 0.85, seed) * fade;

                // A broad broken path of sunset light, never a solid golden stripe.
                float sunPath = exp(-pow((input.world.x + 2.8 - input.world.y * 0.30) / 3.8, 2.0));
                float alpha = ripple * (0.08 + sunPath * 0.22) * ArenaWaterMask(input.world);
                half3 gold = lerp(half3(1.0, 0.72, 0.34), half3(1.65, 1.25, 0.64), seed);
                return half4(gold * input.color.rgb, alpha * input.color.a);
            }
            ENDCG
        }
    }
}
