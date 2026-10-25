#ifndef WIZARD_ROYALE_AMBIENT_WATER_MASK
#define WIZARD_ROYALE_AMBIENT_WATER_MASK

#include "UnitySprites.cginc"

sampler2D _IslandTex;
float4 _IslandRect;

struct AmbientVaryings
{
    float4 vertex : SV_POSITION;
    float2 texcoord : TEXCOORD0;
    half4 color : COLOR;
    float2 world : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

AmbientVaryings AmbientVertex(appdata_t input)
{
    v2f sprite = SpriteVert(input);
    AmbientVaryings output;
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.vertex = sprite.vertex;
    output.texcoord = sprite.texcoord;
    output.color = sprite.color;
    float4 position = float4(input.vertex.xy * _Flip, input.vertex.z, 1.0);
    output.world = mul(unity_ObjectToWorld, position).xy;
    return output;
}

half ArenaWaterMask(float2 world)
{
    float2 uv = (world - _IslandRect.xy) * _IslandRect.zw;
    half inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
    half island = tex2D(_IslandTex, saturate(uv)).a;
    // Include decorative cliffs and the bank, not just the walkable floor.
    return 1.0 - smoothstep(0.02, 0.30, island) * inside;
}

#endif
