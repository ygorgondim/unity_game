Shader "WizardRoyale/AmbientGroundLight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ArenaTex ("Ground albedo", 2D) = "white" {}
        _FloorTex ("Walkable floor mask", 2D) = "white" {}
        _ArenaRect ("Arena world rect", Vector) = (0,0,1,1)
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Light intensity", Float) = 1
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            sampler2D _ArenaTex, _FloorTex;
            float4 _ArenaRect;
            float _Intensity;
            struct ground_v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
                float4 color : COLOR;
            };
            ground_v2f vert(appdata_t v)
            {
                ground_v2f o;
                v2f sprite = SpriteVert(v);
                o.vertex = sprite.vertex;
                o.uv = v.texcoord;
                o.color = sprite.color;
                o.world = mul(unity_ObjectToWorld, float4(v.vertex.xy * _Flip, v.vertex.z, 1)).xy;
                return o;
            }
            float4 frag(ground_v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float radius2 = dot(p,p);
                // Monotonically fading Gaussian: no ring, stroke, or visible disk boundary.
                float gaussian = exp(-radius2 * 5.5) * (1 - smoothstep(0.5, 1, radius2));
                float2 uv = (i.world - _ArenaRect.xy) * _ArenaRect.zw;
                float inside = step(0,uv.x) * step(uv.x,1) * step(0,uv.y) * step(uv.y,1);
                float floorMask = tex2D(_FloorTex, saturate(uv)).g;
                float3 albedo = tex2D(_ArenaTex, saturate(uv)).rgb;
                float floorWeight = smoothstep(0.1,0.9,floorMask) * inside;
                // Only the diffuse personal lights use this masked pass.
                return float4(albedo * i.color.rgb * _Intensity, gaussian * floorWeight * i.color.a);
            }
            ENDCG
        }
    }
}
