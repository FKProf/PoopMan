// heatdistort.fx — Heat shimmer (Lava) and frost ripple (Ice) for PoopMan
// Two techniques share the same distortion logic; caller sets Time + Strength.

sampler2D SourceSampler : register(s0);

float Time; // running seconds, wraps freely
float Strength; // distortion magnitude in UV units (e.g. 0.004 for subtle)
float Frequency; // wave frequency multiplier (e.g. 8.0)
float Speed; // animation speed multiplier (e.g. 1.4)
float2 TexelSize; // 1/width, 1/height (unused here but consistent)

// ── Heat shimmer: wavy horizontal lines rising upward ─────────────────────────
float4 PS_Heat(float2 uv : TEXCOORD0) : COLOR0
{
    float wave = sin(uv.x * Frequency + Time * Speed)
                 + sin(uv.x * Frequency * 0.7 + Time * Speed * 1.3 + 1.5);
    float offset = wave * Strength * (1.0 - uv.y * 0.4); // stronger at bottom
    float2 distUV = float2(uv.x + offset, uv.y + offset * 0.3);
    return tex2D(SourceSampler, distUV);
}

// ── Frost ripple: gentle radial ripples ───────────────────────────────────────
float4 PS_Frost(float2 uv : TEXCOORD0) : COLOR0
{
    float2 c = uv - 0.5;
    float r = length(c);
    float wave = sin(r * Frequency - Time * Speed) * Strength * (1.0 - r * 1.2);
    float2 dir = (r > 0.001) ? normalize(c) : float2(0, 0);
    float2 distUV = uv + dir * wave;
    float4 col = tex2D(SourceSampler, distUV);
    // Light icy blue tint
    col.rgb = lerp(col.rgb, col.rgb * float3(0.82, 0.92, 1.0), 0.18);
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
        PixelShader = compile ps_4_0_level_9_1 PS_Heat();
    }
}
technique FrostRipple
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader = compile ps_4_0_level_9_1 PS_Frost();
    }
}
