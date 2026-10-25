Shader "WizardRoyale/SpellParticle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("HDR intensity", Float) = 1
        _TextureColor ("Use texture RGB", Float) = 1
        _FlameMotion ("Animated flame deformation", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            float4 _Color;
            float _Intensity;
            float _TextureColor;
            float _FlameMotion;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float tip = smoothstep(0.12, 1, uv.y);
                float bend = sin(uv.y * 12 - _Time.y * 8) * 0.035
                    + sin(uv.y * 23 - _Time.y * 13 + uv.x * 7) * 0.018;
                uv.x += bend * tip * _FlameMotion;
                float4 tex = tex2D(_MainTex, uv);
                tex.a *= step(0, uv.x) * step(uv.x, 1);
                return float4(lerp(float3(1,1,1), tex.rgb, _TextureColor) * i.color.rgb * _Intensity, tex.a * i.color.a);
            }
            ENDCG
        }
    }
}
