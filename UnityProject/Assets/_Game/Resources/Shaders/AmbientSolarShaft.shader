Shader "WizardRoyale/AmbientSolarShaft"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Atmospheric brightness", Float) = 1.45
        _AmbientTime ("Time", Float) = 0
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
            float _Intensity, _AmbientTime;
            float4 frag(v2f i) : SV_Target
            {
                // Deliberately independent of island textures, floor masks and ground albedo.
                float2 p = i.texcoord * 2 - 1;
                float width = lerp(0.85, 0.32, i.texcoord.y);
                float across = exp(-pow(p.x / width, 2) * 3);
                float along = exp(-p.y * p.y * 1.2) * (1 - smoothstep(0.75, 1, abs(p.y)));
                float wisps = 0.92 + 0.08 * sin(p.x * 38 + sin(p.y * 6 + _AmbientTime * 0.12));
                return float4(i.color.rgb * _Intensity, across * along * wisps * i.color.a);
            }
            ENDCG
        }
    }
}
