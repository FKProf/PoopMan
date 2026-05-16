// bloom.fx — Multi-pass bloom shader for PoopMan
// Passes:
//   0 = BrightPass  (extract bright regions)
//   1 = BlurH       (horizontal Gaussian blur, half-res)
//   2 = BlurV       (vertical Gaussian blur, half-res)
//   3 = Composite   (add blurred bloom on top of original scene)

sampler2D SourceSampler : register(s0);
sampler2D BloomSampler : register(s1);

float2 TexelSize; // 1/texWidth, 1/texHeight
float Threshold; // bright-pass cut-off  (e.g. 0.55)
float Intensity; // bloom strength        (e.g. 1.1)
float Saturation; // bloom colour boost    (e.g. 1.4)

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
float4 PS_BrightPass(float2 uv : TEXCOORD0) : COLOR0
{
    float4 col = tex2D(SourceSampler, uv);
    float lum = Luminance(col.rgb);
    float mask = saturate((lum - Threshold) / (1.0 - Threshold));
    return float4(col.rgb * mask, col.a);
}

// ── Pass 1: Horizontal Gaussian blur (7-tap) ──────────────────────────────────
static const float GaussW[7] = { 0.0625, 0.125, 0.1875, 0.25, 0.1875, 0.125, 0.0625 };

float4 PS_BlurH(float2 uv : TEXCOORD0) : COLOR0
{
    float4 sum = 0;
    for (int i = -3; i <= 3; i++)
        sum += tex2D(SourceSampler, uv + float2(TexelSize.x * i * 2.0, 0)) * GaussW[i + 3];
    return sum;
}

// ── Pass 2: Vertical Gaussian blur (7-tap) ────────────────────────────────────
float4 PS_BlurV(float2 uv : TEXCOORD0) : COLOR0
{
    float4 sum = 0;
    for (int i = -3; i <= 3; i++)
        sum += tex2D(SourceSampler, uv + float2(0, TexelSize.y * i * 2.0)) * GaussW[i + 3];
    return sum;
}

// ── Pass 3: Composite (scene + bloom) ────────────────────────────────────────
float4 PS_Composite(float2 uv : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(SourceSampler, uv);
    float4 bloom = tex2D(BloomSampler, uv);
    float3 b = AdjustSaturation(bloom.rgb, Saturation) * Intensity;
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
        PixelShader = compile ps_4_0_level_9_1 PS_BrightPass();
    }
}
technique BlurH
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader = compile ps_4_0_level_9_1 PS_BlurH();
    }
}
technique BlurV
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader = compile ps_4_0_level_9_1 PS_BlurV();
    }
}
technique Composite
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader = compile ps_4_0_level_9_1 PS_Composite();
    }
}
