#ifndef WIZARD_ROYALE_FLOATING_HEM
#define WIZARD_ROYALE_FLOATING_HEM

float _HemClock;
float _HemPhase;
float _HemStrength;
float _HemDrag;

float2 FloatingHemUV(float2 uv)
{
    // Broader lower-skirt falloff; the upper body stays outside the deformation.
    float hem = (1.0 - smoothstep(0.085, 0.22, uv.y)) * _HemStrength;
    float sideWave = sin(_HemClock * 1.9 + uv.y * 25.0 + _HemPhase);
    sideWave += sin(_HemClock * 1.25 - uv.y * 37.0 + _HemPhase) * 0.30;
    float edgeWave = sin(uv.x * 17.0 - _HemClock * 1.55 + _HemPhase);
    // Positive sampling offset moves the visible fabric left, opposite rightward travel.
    return uv + float2(sideWave * 0.030 + _HemDrag, edgeWave * 0.018) * hem;
}

#endif
