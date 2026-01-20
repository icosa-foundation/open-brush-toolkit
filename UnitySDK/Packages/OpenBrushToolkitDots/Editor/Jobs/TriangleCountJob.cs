using System.Collections;
using System.Collections.Generic;
using TiltBrushToolkit;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct TriangleCountJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<int> triangles;

    [ReadOnly]
    public NativeArray<float> strokeIDs;

    [ReadOnly]
    public NativeArray<Vector3> uv3;

    public NativeArray<int> triangleCounts; // Output: The count of triangles for each stroke

    // index is the strokeID index
    public void Execute(int index)
    {

        float targetStrokeID = strokeIDs[index];
        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (MathematicsUtils.AreFloatsEqual(targetStrokeID, uv3[triangles[i]].x))
            {
                triangleCounts[index] += 3;
            }
        }

    }

}