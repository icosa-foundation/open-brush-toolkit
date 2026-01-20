using System.Collections;
using System.Collections.Generic;
using TiltBrushToolkit;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct VertexMapJob : IJobParallelFor
{

    [ReadOnly] public NativeArray<float> strokeIDs;

    [ReadOnly] public NativeArray<Vector3> uv3;

    [NativeDisableParallelForRestriction]
    public NativeArray<int> vertexMap;

    public void Execute(int index)
    {
        float strokeId = strokeIDs[index];

        int startingVertexIndex = 0;

        bool lookingForStartingIndex = true;


        for (int i = 0; i < uv3.Length; i++)
        {
            if (lookingForStartingIndex)
            {
                if (MathematicsUtils.AreFloatsEqual(uv3[i].x, strokeId))
                {
                    startingVertexIndex = i;
                    lookingForStartingIndex = false;
                }
            }
            else
            {
                // we're done
                if (!MathematicsUtils.AreFloatsEqual(uv3[i].x, strokeId))
                {
                    break;
                }

                vertexMap[i] = i - startingVertexIndex;
            }
        }


    }
}