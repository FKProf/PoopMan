// shockwave.fx — Radial shockwave distortion for explosions in PoopMan
// Apply as a full-screen post-process pass right after world draw.
// Multiple shockwaves: caller renders this pass once per active wave
// (or use a single pass with 4 wave slots via arrays).

sampler2D SourceSampler : register(s0);

// Shockwave origin in normalised screen UV [0..1]
float2 WaveOrigin;
// Current radius of the ring in UV space (0=just exploded, grows to ~1)
float WaveRadius;
// Thickness of the distortion ring in UV units
float WaveThickness;
// Distortion magnitude
float WaveStrength;
// Life fraction [0..1]: 1=fresh, 0=faded
float WaveLife;

float2 TexelSize;

float4 PS_Shockwave(float2 uv : TEXCOORD0) : COLOR0
{
    float2 delta = uv - WaveOrigin;
    float dist = length(delta);
    // Signed distance from the ring edge
    float ring = dist - WaveRadius;
    // Soft mask inside the ring band
    float mask = 1.0 - saturate(abs(ring) / WaveThickness);
    mask *= mask; // sharpen the bell
    mask *= WaveLife; // fade over time
    // Outward push along the delta direction
    float2 dir = (dist > 0.001) ? normalize(delta) : float2(0, 1);
    float2 distUV = uv + dir * mask * WaveStrength;
    float4 col = tex2D(SourceSampler, distUV);
    // Slight brightness flash at the ring
    col.rgb += mask * 0.12;
    return float4(saturate(col.rgb), col.a);
}

float4x4 MatrixTransform;

void VS_Pass(inout float4 pos : POSITION0, inout float2 uv : TEXCOORD0)
{
    pos = mul(pos, MatrixTransform);
}

technique Shockwave
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader = compile ps_4_0_level_9_1 PS_Shockwave();
    }
}
