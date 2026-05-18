// shockwave.fx — Radial shockwave distortion ring for explosions in PoopMan
// Full-screen additive post-process; render once per active wave.
// Distorts pixel UVs outward along the expanding ring edge.

sampler2D SourceSampler : register(s0);

float2  WaveOrigin;    // explosion centre in normalised screen UV [0..1]
float   WaveRadius;    // current outer radius of ring in UV space (grows 0→~1)
float   WaveThickness; // UV width of the distortion band
float   WaveStrength;  // distortion magnitude in UV units
float   WaveLife;      // [0..1]: 1=fresh explosion, 0=fully faded
float3  RingTint;      // additive flash colour (e.g. white, orange, green)
float2  TexelSize;     // 1/width, 1/height (aspect correction)

float4 PS_Shockwave(float2 uv : TEXCOORD0) : COLOR0
{
    // Correct for non-square aspect ratio so ring stays circular
    float2 aspect = float2(TexelSize.y / TexelSize.x, 1.0); // x stretched
    float2 delta  = (uv - WaveOrigin) * aspect;
    float  dist   = length(delta);

    // Signed distance from the ring's outer edge → soft bell shape over the band
    float ring = dist - WaveRadius;
    float mask = saturate(1.0 - abs(ring) / max(WaveThickness, 0.001));
    mask = mask * mask * mask;   // cubic falloff: sharper at centre, soft edges
    mask *= WaveLife;             // fade over lifetime

    // Push pixels outward along the ring normal
    float2 dir    = (dist > 0.001) ? normalize(delta) : float2(0, 1);
    float2 distUV = uv + dir * (mask * WaveStrength);
    float4 col    = tex2D(SourceSampler, distUV);

    // Additive flash tinted by RingTint, strongest at ring centre
    float flash = mask * 0.18 * WaveLife;
    col.rgb = saturate(col.rgb + RingTint * flash);

    return col;
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
        PixelShader  = compile ps_4_0_level_9_1 PS_Shockwave();
    }
}
