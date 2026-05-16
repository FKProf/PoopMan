// colorgrade.fx — Per-biome colour grading for PoopMan
// Applies: tint overlay, contrast, saturation, vignette.

sampler2D SourceSampler : register(s0);

float3 Tint; // per-biome colour tint  (0..1 each channel)
float TintStrength; // how strongly tint mixes (0=none, 1=full)
float Contrast; // contrast multiplier      (1=neutral)
float Saturation; // saturation scalar        (1=neutral)
float Brightness; // brightness add           (0=neutral)
float VignetteRadius; // vignette inner radius   (0..1, screen UV space)
float VignetteStrength; // vignette darkening      (0=none, 1=full black edge)

float2 TexelSize; // unused but kept for pipeline uniformity

float Luminance(float3 c)
{
    return dot(c, float3(0.299, 0.587, 0.114));
}

float4 PS_ColorGrade(float2 uv : TEXCOORD0) : COLOR0
{
    float4 col = tex2D(SourceSampler, uv);

    // Saturation
    float lum = Luminance(col.rgb);
    col.rgb = lerp(float3(lum, lum, lum), col.rgb, Saturation);

    // Contrast  (pivot at 0.5)
    col.rgb = (col.rgb - 0.5) * Contrast + 0.5 + Brightness;

    // Tint overlay
    col.rgb = lerp(col.rgb, col.rgb * Tint, TintStrength);

    // Vignette  (radial darkening from centre)
    float2 centred = (uv - 0.5) * 2.0; // -1..1
    float dist = length(centred);
    float vign = smoothstep(VignetteRadius, 1.0, dist);
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
        PixelShader = compile ps_4_0_level_9_1 PS_ColorGrade();
    }
}
