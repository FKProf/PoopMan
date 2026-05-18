// colorgrade.fx — Per-biome colour grading for PoopMan
// Applies: unsharp-mask sharpening, filmic S-curve, saturation, tint, vignette.

sampler2D SourceSampler : register(s0);

float3  Tint;            // per-biome colour tint        (0..1 each channel)
float   TintStrength;    // tint blend factor             (0=none, 1=full)
float   Contrast;        // contrast multiplier           (1=neutral)
float   Saturation;      // saturation scalar             (1=neutral)
float   Brightness;      // brightness offset             (0=neutral)
float   VignetteRadius;  // vignette inner radius (UV)   (0..1)
float   VignetteStrength;// vignette darkening amount    (0=none, 1=full black)
float   Sharpness;       // unsharp-mask strength         (0=off, 1=strong)
float2  TexelSize;       // 1/texWidth, 1/texHeight

// ── Utility ──────────────────────────────────────────────────────────────────
float Luminance(float3 c)
{
    return dot(c, float3(0.299, 0.587, 0.114));
}

// Filmic S-curve: lifts blacks slightly, rolls off highlights
float3 FilmicCurve(float3 x)
{
    // Simplified ACES-inspired tone curve (cheap, no matrices)
    float3 a = x * (x + 0.0245786) - 0.000090537;
    float3 b = x * (0.983729 * x + 0.4329510) + 0.238081;
    return saturate(a / b);
}

float4 PS_ColorGrade(float2 uv : TEXCOORD0) : COLOR0
{
    // ── Unsharp-mask sharpening (5-tap cross, runs in texture space) ─────
    float4 col   = tex2D(SourceSampler, uv);
    float4 blur  = (tex2D(SourceSampler, uv + float2( TexelSize.x, 0))
                  + tex2D(SourceSampler, uv + float2(-TexelSize.x, 0))
                  + tex2D(SourceSampler, uv + float2(0,  TexelSize.y))
                  + tex2D(SourceSampler, uv + float2(0, -TexelSize.y))) * 0.25;
    col.rgb = saturate(col.rgb + (col.rgb - blur.rgb) * Sharpness);

    // ── Saturation ───────────────────────────────────────────────────────
    float lum = Luminance(col.rgb);
    col.rgb = lerp(float3(lum, lum, lum), col.rgb, Saturation);

    // ── Filmic S-curve (replaces raw contrast) ───────────────────────────
    // Scale contrast around pivot 0.5, then bake filmic roll-off
    col.rgb = (col.rgb - 0.5) * Contrast + 0.5 + Brightness;
    col.rgb = FilmicCurve(saturate(col.rgb));

    // ── Tint overlay ─────────────────────────────────────────────────────
    col.rgb = lerp(col.rgb, col.rgb * Tint, TintStrength);

    // ── Vignette (smooth cubic, pure black at corners) ───────────────────
    float2 centred = (uv - 0.5) * 2.0;          // maps [0,1] → [-1,1]
    float dist = dot(centred, centred);           // squared length (cheaper)
    float vign = smoothstep(VignetteRadius * VignetteRadius, 1.2, dist);
    col.rgb *= (1.0 - vign * VignetteStrength);

    return float4(saturate(col.rgb), col.a);
}

float4x4 MatrixTransform;

void VS_Pass(inout float4 pos : POSITION0, inout float2 uv : TEXCOORD0)
{
    pos = mul(pos, MatrixTransform);
}

technique ColorGrade
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_ColorGrade();
    }
}
