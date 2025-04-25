// Curl noise functions for displacement
float curlX(float3 p, float d) {
    return (
        (sin(p.y + d) - sin(p.y - d)) * (sin(p.z + d) + sin(p.z - d)) -
        (sin(p.z + d) - sin(p.z - d)) * (sin(p.y + d) + sin(p.y - d))
    ) / (4.0 * d * d);
}

float curlY(float3 p, float d) {
    return (
        (sin(p.z + d) - sin(p.z - d)) * (sin(p.x + d) + sin(p.x - d)) -
        (sin(p.x + d) - sin(p.x - d)) * (sin(p.z + d) + sin(p.z - d))
    ) / (4.0 * d * d);
}

float curlZ(float3 p, float d) {
    return (
        (sin(p.x + d) - sin(p.x - d)) * (sin(p.y + d) + sin(p.y - d)) -
        (sin(p.y + d) - sin(p.y - d)) * (sin(p.x + d) + sin(p.x - d))
    ) / (4.0 * d * d);
}

void BubbleWandVertex_float(
    float3 Position,
    float3 Normal,
    float3 UV,
    float Time,
    float ScrollRate,
    float ScrollJitterIntensity,
    float ScrollJitterFrequency,
    out float3 DisplacedPosition,
    out float3 DisplacedNormal)
{
    // Initial wave displacement
    float radius = UV.z;
    float wave = sin(UV.x * 3.14159);
    float3 wave_displacement = radius * Normal * wave;
    float3 pos = Position + wave_displacement;
    
    // Scroll jitter displacement
    float t = Time * ScrollRate;
    pos.x += sin(t + Time + pos.z * ScrollJitterFrequency) * ScrollJitterIntensity * 0.1;
    pos.z += cos(t + Time + pos.x * ScrollJitterFrequency) * ScrollJitterIntensity * 0.1;
    pos.y += cos(t * 1.2 + Time + pos.x * ScrollJitterFrequency) * ScrollJitterIntensity * 0.1;

    // Curl noise displacement
    float d = 30;
    float freq = 0.1;
    float3 p = pos * freq + Time;
    float3 curl_displacement = float3(
        curlX(p, d),
        curlY(p, d),
        curlZ(p, d)
    ) * ScrollJitterIntensity * 0.1; // kDecimetersToWorldUnits constant
    
    // Final position
    DisplacedPosition = pos + curl_displacement;
    
    // Perturb normal based on both wave and curl displacement
    DisplacedNormal = normalize(Normal + curl_displacement * 2.5 + wave_displacement * 2.5);
} 