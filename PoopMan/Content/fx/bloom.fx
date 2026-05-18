// bloom.fx — Multi-pass bloom shader for PoopMan
// Passes:
//   BrightPass  — extract bright regions above Threshold
//   BlurH       — horizontal Gaussian blur (half-res)
//   BlurV       — vertical Gaussian blur (half-res)
//   Composite   — luminance-preserving additive blend onto scene

sampler2D SourceSampler : register(s0);
sampler2D BloomSampler  : register(s1);

float2 TexelSize;   // 1/texWidth, 1/texHeight
float  Threshold;   // bright-pass cutoff  (e.g. 0.60 — keep bloom tight)
float  Intensity;   // bloom brightness    (e.g. 0.80 — controlled, not glaring)
float  Saturation;  // bloom colour boost  (e.g. 1.20 — moderate)

// ── Utility ──────────────────────────────────────────────────────────────────
float Luminance(float3 c)
{
    return dot(c, float3(0.299, 0.587, 0.114));
}

float3 AdjustSaturation(float3 c, float sat)
{
    float lum = Luminance(c);
    return lerp(float3(lum, lum, lum), c, sat);
}

// ── Pass 0: BrightPass ────────────────────────────────────────────────────────
// Quadratic knee: avoids hard clipping at the threshold edge.
float4 PS_BrightPass(float2 uv : TEXCOORD0) : COLOR0
{
    float4 col = tex2D(SourceSampler, uv);
    float  lum = Luminance(col.rgb);
    // Soft knee: smooth ramp above Threshold
    float  over = lum - Threshold;
    float  mask = saturate(over / max(1.0 - Threshold, 0.001));
    mask = mask * mask;  // quadratic → brighter pixels get disproportionately more bloom
    return float4(col.rgb * mask, col.a);
}

// ── Pass 1: Horizontal Gaussian blur (9-tap) ─────────────────────────────────
// Tighter kernel than a 7-tap → bloom stays local to bright pixels (no smear).
static const float GaussW9[9] = {
    0.028, 0.066, 0.124, 0.179, 0.206, 0.179, 0.124, 0.066, 0.028
};

float4 PS_BlurH(float2 uv : TEXCOORD0) : COLOR0
{
    float4 sum = 0;
    [unroll]
    for (int i = -4; i <= 4; i++)
        sum += tex2D(SourceSampler, uv + float2(TexelSize.x * i * 1.5, 0)) * GaussW9[i + 4];
    return sum;
}

// ── Pass 2: Vertical Gaussian blur (9-tap) ───────────────────────────────────
float4 PS_BlurV(float2 uv : TEXCOORD0) : COLOR0
{
    float4 sum = 0;
    [unroll]
    for (int i = -4; i <= 4; i++)
        sum += tex2D(SourceSampler, uv + float2(0, TexelSize.y * i * 1.5)) * GaussW9[i + 4];
    return sum;
}

// ── Pass 3: Composite (scene + bloom, luminance-aware) ───────────────────────
// We add bloom only in the luminance channel direction so the scene doesn't
// wash out: pixels that are already bright get less additive lift.
float4 PS_Composite(float2 uv : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(SourceSampler, uv);
    float4 bloom = tex2D(BloomSampler,  uv);

    float3 b = AdjustSaturation(bloom.rgb, Saturation) * Intensity;

    // Soft rolloff: reduce bloom where scene is already very bright
    float sceneLum = Luminance(scene.rgb);
    b *= saturate(1.0 - sceneLum * 0.5);

    return float4(saturate(scene.rgb + b), scene.a);
}

// ── Vertex passthrough ────────────────────────────────────────────────────────
float4x4 MatrixTransform;

void VS_Pass(inout float4 pos : POSITION0, inout float2 uv : TEXCOORD0)
{
    pos = mul(pos, MatrixTransform);
}

// ── Techniques ────────────────────────────────────────────────────────────────
technique BrightPass
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_BrightPass();
    }
}
technique BlurH
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_BlurH();
    }
}
technique BlurV
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_BlurV();
    }
}
technique Composite
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_Composite();
    }
}
