#ifndef MYLARTUBE_VERTEX_DEFORMATION
#define MYLARTUBE_VERTEX_DEFORMATION

void mylarTubeVertex_float(
    float3 position,
    float3 normal,
    float radius,
    float u,
    float squeeze_amount,
    out float3 modified_position,
    out float3 modified_normal)
{
    float squeeze = sin(u * 3.14159);
    float3 squeeze_displacement = radius * normal * squeeze;
    modified_position = position - (squeeze_displacement * squeeze_amount);
    modified_normal = normalize(normal + squeeze_displacement * 2.5);
}

#endif
