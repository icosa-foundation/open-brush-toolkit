using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using TiltBrushToolkit;


public class SplitMeshByStroke
{

    [MenuItem("Tilt Brush/Labs/Split mesh by stroke")]
    public static void MeshToStrokes()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected == null || selected.Length == 0)
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Split mesh by stroke");

        AssetDatabase.StartAssetEditing();

        if (!AssetDatabase.IsValidFolder($"Assets/{EditorUtils.m_SplitMeshByStrokeDirectoryName}"))
        {
            AssetDatabase.CreateFolder("Assets", "SplitMeshByStroke");
        }

        bool cancel = false;
        try
        {
            foreach (var obj in selected)
            {
                try
                {
                    MeshFilter mf = obj.GetComponent<MeshFilter>();
                    if (mf == null) throw new MissingComponentException($"Missing MeshFilter on {obj.name}");

                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr == null) throw new MissingComponentException($"Missing MeshRenderer on {obj.name}");

                    cancel = MeshToStroke(mf);
                    if (cancel)
                    {
                        Undo.RevertAllInCurrentGroup();
                        break;
                    }

                    Undo.DestroyObjectImmediate(mf);
                    Undo.DestroyObjectImmediate(mr);
                }
                catch (MissingComponentException ex)
                {
                    Debug.LogError($"Error processing object {obj.name}: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    Debug.LogError($"Error processing object {obj.name}: {ex.Message}");
                }
            }
        }
        finally // since we call AssetDatabase.StartAssetEditing(), we must also call AssetDatabase.StopAssetEditing()
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
        }
    }

// separate a mesh into strokes by timestamp in UV2
    private static bool MeshToStroke(MeshFilter meshFilter)
    {
        GameObject go = meshFilter.gameObject;
        Mesh mesh = meshFilter.sharedMesh;
        var uv3 = new List<Vector3>();
        mesh.GetUVs(2, uv3);

        bool cancel = false;

        if (uv3.Count == 0)
        {
            throw new InvalidOperationException(
                $"Mesh ({mesh.name}) has no timestamps. Make sure the sketch was exported from Open Brush with ExportStrokeTimestamp = true");
        }
        else
        {
            List<float> strokeIDs = new List<float>();

            // is using float safe as a key?
            // for our use case, yes.
            // if we were calculating the key by e.g adding two floats together, that'd be a problem
            Dictionary<float, int> vertexCounts = new Dictionary<float, int>();

            // we create the strokeGameObjects already here, because later (when we create the meshes) we parent them to 'go'
            // but if we call SetParent for each one here right after it has been instantiated
            // then in the next iteration, GameObject.Instantiate(go,..) will also clone the children that were parented
            List<GameObject> strokeGameObjects = new List<GameObject>();

            string baseNameForStrokeGameObject = go.name;
            Undo.RecordObject(go, "Split mesh by stroke");
            go.name += " (separated)";

            // explanation of the following 6 lines:
            // - the new meshes that get created are parented to 'go', which holds the mesh originally
            // - we reset go's local transform
            // - and we copy go's transform to the new mesh gameobjects (this is done later in the code, when GetMeshSubset is used)
            // This ensures that nothing changes visually when the tool is used
            // The other approach would be to apply the transform to the mesh data, but I think it's better to not modify the mesh data
            // as it's saved as an asset
            Vector3 originalScale = go.transform.localScale;
            Vector3 originalPosition = go.transform.localPosition;
            Quaternion originalRotation = go.transform.localRotation;

            go.transform.localScale = new Vector3(1, 1, 1);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                if (!vertexCounts.ContainsKey(uv3[i].x))
                {
                    vertexCounts.Add(uv3[i].x, 1);
                    strokeIDs.Add(uv3[i].x);
                    GameObject strokeGameObject = GameObject.Instantiate(go);
                    Undo.RegisterCreatedObjectUndo(strokeGameObject, "Split mesh by stroke");
                    strokeGameObjects.Add(strokeGameObject);
                }
                else
                {
                    vertexCounts[uv3[i].x]++;
                }
            }


            /*
             * We use 3 jobs to set up the data needed for creating the new meshes.
             *
             * - Jobs 1 and 2 create data that Job 3 needs
             *
             * - All jobs are IParallelFor, and Execute(index) runs for the index-th stroke
             *
             * Jobs:
             * 1. The first job will calculate how many triangles each new mesh will have.
             *
             * 2. The second job will create a 1d array that maps each vertex in OriginalMesh.vertices
             *    to that vertex's index in the new mesh's vertex array.
             *
             * 3. The third job populates a 1d array that holds all the new triangle arrays in continuous memory. (There are no Native 2d arrays)
             *  - This job depends on (2), because when creating the new triangles arrays, the .triangles array must reference indices in the new vertex array
             *  - This job depends on (1), because it needs to know the length of each strokes triangle array, in order
             *    to calculate the starting index for a stroke's triangle array.
             */


            NativeArray<int> triangleCounts = new NativeArray<int>(strokeIDs.Count, Allocator.Persistent);
            NativeArray<int> originalTriangles = new NativeArray<int>(mesh.triangles, Allocator.Persistent);
            NativeArray<float> nativeStrokeIDs = new NativeArray<float>(strokeIDs.ToArray(), Allocator.Persistent);
            NativeArray<Vector3> nativeUV3 = new NativeArray<Vector3>(uv3.ToArray(), Allocator.Persistent);

            var job1 = new TriangleCountJob()
            {
                triangles = originalTriangles,
                triangleCounts = triangleCounts,
                uv3 = nativeUV3,
                strokeIDs = nativeStrokeIDs
            };

            var job1Handle = job1.Schedule(strokeIDs.Count, 1);

            NativeArray<int> vertexMap = new NativeArray<int>(mesh.vertexCount, Allocator.Persistent);

            var job2 = new VertexMapJob()
            {
                vertexMap = vertexMap,
                uv3 = nativeUV3,
                strokeIDs = nativeStrokeIDs
            };

            var job2Handle = job2.Schedule(strokeIDs.Count, 1);

            NativeArray<int> allNewTriangles = new NativeArray<int>(mesh.triangles.Length, Allocator.Persistent);

            var job3 = new TriangleArrayCopyJob()
            {
                uv3 = nativeUV3,
                strokeIDs = nativeStrokeIDs,
                originalTriangles = originalTriangles,
                triangleCounts = triangleCounts,
                vertexMap = vertexMap,
                triangles = allNewTriangles,
            }.Schedule(strokeIDs.Count, 1, JobHandle.CombineDependencies(job1Handle, job2Handle));

            JobHandle.CompleteAll(ref job1Handle, ref job2Handle);
            job3.Complete();

            // allNewTriangles and triangleCounts will be disposed after mesh generation
            nativeUV3.Dispose();
            nativeStrokeIDs.Dispose();
            originalTriangles.Dispose();
            vertexMap.Dispose();

            const int PROGRESS_FREQUENCY = 1;
            int count = 0;

            int strokeIndex = 0;
            foreach (float strokeId in strokeIDs)
            {
                if (++count > PROGRESS_FREQUENCY)
                {
                    count = 0;
                    cancel = EditorUtility.DisplayCancelableProgressBar("Creating strokes", "Processing " + mesh.name,
                        (float)strokeIndex / (float)strokeIDs.Count);
                    if (cancel) break;
                }

                GameObject strokeGameObject = strokeGameObjects[strokeIndex];

                // These 3 lines ensure that nothing changes visually when the tool is used
                strokeGameObject.transform.localPosition = originalPosition;
                strokeGameObject.transform.localScale = originalScale;
                strokeGameObject.transform.localRotation = originalRotation;

                strokeGameObject.transform.SetParent(go.transform, false);
                strokeGameObject.name = $"{baseNameForStrokeGameObject} ({strokeIndex})";

                int startingIndexInTrianglesArray = 0;
                int startingIndexInVerticesArray = 0;
                int triangleCount = triangleCounts[strokeIndex];
                int vertexCount = vertexCounts[strokeId];

                for (int i = 0; i < strokeIndex; i++)
                {
                    float strokeId_ = strokeIDs[i];
                    startingIndexInTrianglesArray += triangleCounts[i];
                    startingIndexInVerticesArray += vertexCounts[strokeId_];
                }

                int[] trianglesForStroke = new int[triangleCount];
                Vector3[] vertices = new Vector3[vertexCount];
                Vector2[] uv = new Vector2[vertexCount];
                Vector3[] normals = new Vector3[vertexCount];
                Color[] colors = new Color[vertexCount];
                Vector3[] uv2 = new Vector3[vertexCount];
                Vector3[] uv3_subset = new Vector3[vertexCount];

                List<Vector3> uv2_full = new List<Vector3>();
                mesh.GetUVs(1, uv2_full);

                NativeArray<int>.Copy(allNewTriangles, startingIndexInTrianglesArray, trianglesForStroke, 0,
                    triangleCount);

                CopyMeshAttribute(mesh.vertices, vertices, startingIndexInVerticesArray, vertexCount);

                CopyMeshAttribute(mesh.uv, uv, startingIndexInVerticesArray, vertexCount);

                CopyMeshAttribute(mesh.normals, normals, startingIndexInVerticesArray, vertexCount);

                CopyMeshAttribute(mesh.colors, colors, startingIndexInVerticesArray, vertexCount);

                CopyMeshAttribute(uv2_full.ToArray(), uv2, startingIndexInVerticesArray, vertexCount);

                CopyMeshAttribute(uv3.ToArray(), uv3_subset, startingIndexInVerticesArray, vertexCount);

                var newMesh_ = EditorUtils.GetMeshSubset(mesh, trianglesForStroke, vertices, uv, uv2, uv3_subset, normals, colors,
                    strokeIndex);

                strokeGameObject.GetComponent<MeshFilter>().mesh = newMesh_;
                strokeIndex++;
            }

            allNewTriangles.Dispose();
            triangleCounts.Dispose();
        }

        return cancel;
    }

// copy elements from sourceArray to destinationArray starting at startingIndexInSource,
// making sure that bounds aren't exceeded
// The purpose of this function is because e.g Dots stroke has uv2, but OilPaint stroke doesn't.
    private static void CopyMeshAttribute<T>(
        T[] sourceArray,
        T[] destinationArray,
        int startingIndexInSource,
        int vertexCount)
    {
        if (sourceArray != null && sourceArray.Length >= startingIndexInSource + vertexCount)
        {
            Array.Copy(sourceArray, startingIndexInSource, destinationArray, 0, vertexCount);
        }
    }
}