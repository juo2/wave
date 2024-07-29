#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

using Unity.Burst;
using Unity.Mathematics;

namespace WaveMaker
{
    public class CustomMeshManager
    {
        public Mesh Mesh { get => _mesh; }
        Mesh _mesh;

        MeshFilter _meshFilter; 

#if (UNITY_2019_3_OR_NEWER)
            NativeArray<Vector3> _vertices;
#else
            NativeArray<Vector3> _jobVertices;
            Vector3[] _vertices;
            Vector3[] _normals;
#endif

        public CustomMeshManager(MeshFilter meshFilter, in IntegerPair resolution, in float2 size)
        {
            this._meshFilter = meshFilter;
            if (_meshFilter == null)
                throw new System.NullReferenceException("Wave Maker renderer was given a null mesh filter. Cannot initialize");

            UpdateMesh(in resolution, in size);
        }

        public void SetMeshColors(ref Color[] colors)
        {
            if (_mesh != null)
                _mesh.colors = colors;
        }

        public Color[] GetMeshColorsCopy()
        {
            if (_mesh != null)
                return _mesh.colors;
            else
                return new Color[0];
        }

        public void UpdateMesh(in IntegerPair resolution, in float2 size)
        {
            Dispose();

            _mesh = new Mesh();

            var cellSize = new float2(size.x / (resolution.x - 1), size.y / (resolution.z - 1));
            IntegerPair nCells = new IntegerPair(resolution.x - 1, resolution.z -1);

            // 10 resolution means 10 vertices = 10 samples and 9 mesh cells on that axis
            int nVertices = resolution.x * resolution.z;

#if (UNITY_2019_3_OR_NEWER)
                _vertices = new NativeArray<Vector3>(nVertices, Allocator.Persistent);
#else
                _vertices = new Vector3[nVertices];
                _normals = new Vector3[nVertices];
                _jobVertices = new NativeArray<Vector3>(nVertices, Allocator.Persistent);
#endif
            
            Vector2[] uvs = new Vector2[nVertices];
            Vector3[] normals = new Vector3[nVertices];

            // Two triangles between each pair of vertices.
            // 3 vertices each triangle. 
            // e.g: Resolution 4x4 would have 3x3 cells. 9 squares with 2 triangles each, with 3 vertices each
            int[] triangles = new int[2 * 3 * nCells.x * nCells.z];

            int p0Index, p1Index, p2Index, p3Index;

            float uSection = 1.0f / nCells.x;
            float vSection = 1.0f / nCells.z;
            
            int triangleCount = 0;

            for (int z = 0; z < resolution.z; ++z)
                for (int x = 0; x < resolution.x; ++x)
                {
                    p0Index = z * resolution.x + x;

                    // Generate the new array data for this point
                    _vertices[p0Index] = new Vector3(x * cellSize.x, 0, z * cellSize.y);
                    normals[p0Index] = Vector3.up;
                    uvs[p0Index] = new Vector2(x * uSection, z * vSection);

                    // Generate triangles but not on the extreme sides
                    if (x != resolution.x - 1 && z != resolution.z - 1)
                    {
                        // Calculate indices of this grid ( 2 triangles )
                        p1Index = p0Index + 1;
                        p2Index = p0Index + resolution.x;
                        p3Index = p2Index + 1;

                        //    Z
                        //    |
                        //    |
                        //    p2 -- p3
                        //    |  /  |
                        //    p0 -- p1 --> X


                        /// 0 - 3 - 1
                        triangles[triangleCount++] = p0Index;
                        triangles[triangleCount++] = p3Index;
                        triangles[triangleCount++] = p1Index;

                        //  0 - 2 - 3
                        triangles[triangleCount++] = p0Index;
                        triangles[triangleCount++] = p2Index;
                        triangles[triangleCount++] = p3Index;
                    }
                }

            // Create the mesh and assign
#if (UNITY_2019_3_OR_NEWER)
                _mesh.SetVertices(_vertices);
                _mesh.SetNormals(normals);
#else
                _mesh.vertices = _vertices;
                _jobVertices.CopyFrom(_vertices);
                _mesh.normals = normals;
#endif
            _mesh.uv = uvs;

            // WARNING: Triangles must be asigned afterwards
            _mesh.triangles = triangles;

            _meshFilter.sharedMesh = _mesh;
        }

#if (UNITY_2019_3_OR_NEWER)
        public void CopyHeightsAndNormals(in NativeArray<float> heights, in NativeArray<Vector3> normals, in IntegerPair resolution, in IntegerPair ghostResolution)
        {
            if (!Application.isPlaying || _mesh == null)
                return;

            CopyHeightsJob job = new CopyHeightsJob
            {
                heights = heights,
                vertices = _vertices,
                resolution = resolution,
                ghostResolution = ghostResolution
            };
            JobHandle handle = job.Schedule(_vertices.Length, 64, default);

            handle.Complete();

            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(normals);

            // NOTE: mesh.RecalculateTangents(); is not called. It makes paint time multiply by 10
            // and it is removed to prevent from activating it by error. 
            // If you need them, calculate your tangents in your shader using: cross(cross(currentTangent,normal), normal)
        }
#else

        public void CopyHeightsAndNormals(in NativeArray<float> heights, in NativeArray<Vector3> normals, in IntegerPair resolution, in IntegerPair ghostResolution)
        {
            if (!Application.isPlaying || _mesh == null)
                return;

            var job = new CopyHeightsJob
            {
                heights = heights,
                vertices = _jobVertices,
                resolution = resolution,
                ghostResolution = ghostResolution
            };
            JobHandle handle = job.Schedule(_vertices.Length, 64, default);

            handle.Complete();

            normals.CopyTo(_normals);
            _mesh.normals = _normals;

            _jobVertices.CopyTo(_vertices);
            _mesh.vertices = _vertices;

            // NOTE: mesh.RecalculateTangents(); is not called. It makes paint time multiply by 10
            // and it is removed to prevent from activating it by error. 
            // If you need them, calculate your tangents in your shader using: cross(cross(currentTangent,normal), normal)
        }
#endif

        /// <summary>
        /// Deletes information stored on this object
        /// </summary>
        public void Dispose()
        {
            if (_meshFilter != null)
                _meshFilter.sharedMesh = null;

            if (_mesh != null)
                UnityEngine.Object.DestroyImmediate(_mesh);

#if (UNITY_2019_3_OR_NEWER)
                if (_vertices.IsCreated)
                    _vertices.Dispose();
#else
                if (_jobVertices.IsCreated)
                   _jobVertices.Dispose();
#endif
        }
        
        [BurstCompile]
        private struct CopyHeightsJob : IJobParallelFor
        {
            public NativeArray<Vector3> vertices;
            [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float> heights;
            [ReadOnly] public IntegerPair resolution;
            [ReadOnly] public IntegerPair ghostResolution;

            public void Execute(int index)
            {
                int ghostResolutionXDiff = (ghostResolution.x - resolution.x) / 2;
                int ghostResolutionZDiff = (ghostResolution.z - resolution.z) / 2;

                Utils.FromIndexToSampleIndices(index, in resolution, out int x, out int z);

                Vector3 vec = vertices[index];
                vec.y = heights[(z + ghostResolutionZDiff) * ghostResolution.x + (x + ghostResolutionXDiff)];
                vertices[index] = vec;
            }
        }
    }
}

#endif