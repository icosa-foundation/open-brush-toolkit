void BubbleWandSurface_float(
    float3 ViewDirection,
    float3 Normal,
    float3 BaseColor,
    float Time,
    float2 UV,
    Texture2D DiffractionTexture,
    SamplerState DiffractionSampler,
    out float3 Specular,
    out float Smoothness,
    out float3 Emission)
{
    // Hardcoded specular values
    Smoothness = 0.9;
    Specular = 0.6 * BaseColor;
    
    // Calculate rim
    float3 n = Normal;
    half rim = 1.0 - abs(dot(normalize(ViewDirection), n));
    rim *= 1.0 - pow(rim, 5);
    
    // Thin slit diffraction texture ramp lookup
    float2 diffractionUV = float2(rim + Time + Normal.y, rim + Normal.y);
    float3 diffraction = DiffractionTexture.SampleLevel(DiffractionSampler, diffractionUV, 0).xyz;
    
    // Final emission
    Emission = rim * (0.25 * diffraction * rim + 0.75 * diffraction * BaseColor);
} 