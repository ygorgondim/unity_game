Shader "WizardRoyale/FireCorona"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LoopDuration ("Animation loop seconds", Float) = 1.28
        _Phase ("Individual phase", Float) = 0
        _Heat ("Fuse heat", Range(0,1)) = 0
        _Intensity ("Fire brightness", Float) = 1.45
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex SpriteVert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            float _LoopDuration, _Phase, _Heat, _Intensity;

            float4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * (2 * UNITY_PI / max(_LoopDuration,0.1)) + _Phase;
                float2 p = i.texcoord * 2 - 1;
                float angle = atan2(p.y,p.x);
                float boundary = 0.77 + 0.025*sin(angle*3-t*2)
                    + 0.018*sin(angle*7+t*3);
                float2 q = p / boundary;
                q.x += 0.035*sin(q.y*6+t*2);
                float r = length(q);
                float aa = clamp(fwidth(r)*0.8,0.006,0.045);
                float sphere = 1-smoothstep(1-aa,1+aa,r);
                float3 n = float3(q,sqrt(saturate(1-dot(q,q))));
                // Rotate a warped ribbon pattern over a sphere, rather than rotate a flat ring.
                float s = sin(t), c = cos(t);
                float3 flow = float3(c*n.x+s*n.z,n.y,-s*n.x+c*n.z);
                flow.y += 0.13*sin(flow.x*4+flow.z*3+t*2);
                float ribbon = sin(flow.y*5.2 + flow.x*2.1
                    + 1.25*sin(flow.z*3.8+flow.x*2.6))
                    + 0.28*sin(flow.x*6-flow.z*4+flow.y*3);
                float distanceToRibbon = abs(ribbon);
                float fire = 1-smoothstep(0.48,0.62,distanceToRibbon);
                // A thin red rim breaks into tongues, avoiding a smooth circular outline.
                float rim = exp(-pow((r-0.96)/0.065,2))
                    * smoothstep(-0.1,0.7,sin(angle*5-t*2)+0.4*sin(angle*9+t*3));
                float coverage = max(fire,rim*0.85) * sphere;
                float heat = (1-smoothstep(0.05,0.60,distanceToRibbon))
                    * (0.76+0.24*saturate(-q.y));
                float3 red = float3(1,0.025,0.003);
                float3 orange = float3(1,0.20,0.008);
                float3 yellow = float3(1,0.88,0.025);
                float3 color = lerp(red,orange,smoothstep(0.02,0.32,heat));
                color = lerp(color,yellow,smoothstep(0.28,0.78,heat));
                color = lerp(color,float3(1,0.97,0.30),saturate(heat-0.85)*_Heat);
                // A soft red bloom remains outside the silhouette; gaps stay transparent.
                float glow = exp(-pow((r-0.99)/0.15,2))*0.11;
                float alpha = saturate(coverage*0.96+glow);
                color = lerp(red,color,saturate(coverage/max(alpha,0.0001)));
                return float4(color*i.color.rgb*_Intensity*(1+_Heat*0.18),alpha*i.color.a);
            }
            ENDCG
        }
    }
}
