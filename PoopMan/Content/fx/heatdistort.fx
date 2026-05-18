// heatdistort.fx — Ambient distortion effects for PoopMan biomes
// Techniques:
//   HeatDistort  — lava heat shimmer (horizontal ripple, subtle)
//   FrostRipple  — ice cold-breath ripple (radial, blue tint)
//   SwampFog     — swamp murky overlay (slow vertical waviness + green tint)

sampler2D SourceSampler : register(s0);

float  Time;      // running seconds
float  Strength;  // distortion magnitude in UV units
float  Frequency; // wave frequency multiplier
float  Speed;     // animation speed multiplier
float2 TexelSize; // 1/width, 1/height (unused in UV math, kept for uniformity)

// ── Heat shimmer: subtle rising wavy columns (lava) ───────────────────────────
float4 PS_Heat(float2 uv : TEXCOORD0) : COLOR0
{
    // Two overlapping sine waves for a more organic shimmer
    float w1 = sin(uv.x * Frequency        + Time * Speed);
    float w2 = sin(uv.x * Frequency * 0.6  + Time * Speed * 1.4 + 2.1);
    float wave = (w1 + w2) * 0.5;

    // Distortion is stronger at the bottom (hotter) and vanishes at the top
    float yFade = (1.0 - uv.y) * 0.6 + 0.4;  // range [0.4..1.0]
    float offset = wave * Strength * yFade;

    float2 distUV = float2(uv.x + offset, uv.y + offset * 0.25);
    float4 col = tex2D(SourceSampler, distUV);

    // Very subtle warm cast (not invasive — just a whisper of orange)
    col.rgb = lerp(col.rgb, col.rgb * float3(1.04, 0.97, 0.90), 0.12);
    return col;
}

// ── Frost ripple: cold-breath concentric ripples (ice) ───────────────────────
float4 PS_Frost(float2 uv : TEXCOORD0) : COLOR0
{
    float2 c = uv - 0.5;
    float  r = length(c);

    // Concentric rings that contract inward (breathe inward → cold feel)
    float wave = sin(r * Frequency - Time * Speed) * Strength;
    // Falloff near centre and corners
    float fade = saturate(1.0 - r * 1.6) * saturate(r * 6.0);
    float2 dir = (r > 0.001) ? normalize(c) : float2(0, 0);
    float2 distUV = uv + dir * wave * fade;

    float4 col = tex2D(SourceSampler, distUV);
    // Icy blue tint — stronger at edges (colder there)
    float iceFactor = saturate(r * 1.4) * 0.22;
    col.rgb = lerp(col.rgb, col.rgb * float3(0.78, 0.90, 1.0), iceFactor);
    return col;
}

// ── Swamp fog: slow vertical murk + green tint ───────────────────────────────
float4 PS_Swamp(float2 uv : TEXCOORD0) : COLOR0
{
    // Slow vertical waviness like gas rising through thick water
    float w1 = sin(uv.x * Frequency * 0.5 + Time * Speed * 0.4);
    float w2 = sin(uv.y * Frequency * 0.3 + Time * Speed * 0.3 + 1.7);
    float wave = (w1 + w2) * 0.5;

    // Fog is denser at bottom
    float fogDensity = (1.0 - uv.y) * 0.7 + 0.3;
    float2 distUV = float2(uv.x + wave * Strength * 0.5,
                           uv.y + wave * Strength * fogDensity);
    float4 col = tex2D(SourceSampler, distUV);

    // Murky green tint that pools at the bottom
    float3 murk = float3(0.55, 0.80, 0.45);
    col.rgb = lerp(col.rgb, col.rgb * murk, fogDensity * 0.18);
    return col;
}

float4x4 MatrixTransform;

void VS_Pass(inout float4 pos : POSITION0, inout float2 uv : TEXCOORD0)
{
    pos = mul(pos, MatrixTransform);
}

technique HeatDistort
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_Heat();
    }
}
technique FrostRipple
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_Frost();
    }
}
technique SwampFog
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_Swamp();
    }
}
