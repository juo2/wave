#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

using Unity.Burst;
using Unity.Mathematics;

namespace WaveMaker
{
    [BurstCompile]
    public struct AccelerationsJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> accelerations;
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float> heights;
        [ReadOnly] public IntegerPair ghostResolution;
        [ReadOnly] public IntegerPair resolution;
        [ReadOnly] public NativeArray<int> fixedSamples;
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<long> cineticEnergy;

        public void Execute(int indexInAccelerations)
        {
            // Initialize before simulating the surface
            //TODO: Do only once but cannot be done outside jobs if many steps are performed.
            cineticEnergy[0] = 0;

            if (fixedSamples[indexInAccelerations] == 1)
                accelerations[indexInAccelerations] = 0;
            else 
                accelerations[indexInAccelerations] = CalculateAcceleration(indexInAccelerations, in resolution, in ghostResolution);
        }

        internal float CalculateAcceleration(int indexInAccelerations, in IntegerPair resolution, in IntegerPair ghostResolution)
        {
            int indexInHeights = Utils.FromNoGhostIndexToGhostIndex(indexInAccelerations, in resolution, in ghostResolution);
            float averageNeighbourHeight = (heights[indexInHeights - 1] +
                                            heights[indexInHeights + 1] +
                                            heights[indexInHeights - ghostResolution.x] +
                                            heights[indexInHeights + ghostResolution.x]) / 4.0f;

            return averageNeighbourHeight - heights[indexInHeights];
        }
    }

    [BurstCompile]
    public struct HeightsAndVelocitiesJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<float> heights;
        [NativeDisableParallelForRestriction] public NativeArray<float> velocities;
        [NativeDisableParallelForRestriction] [WriteOnly]public unsafe NativeArray<long> cineticEnergy;

        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float> accelerations;
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<int> fixedSamples;

        [ReadOnly] public IntegerPair ghostResolution;
        [ReadOnly] public IntegerPair resolution;
        [ReadOnly] public float2 sampleSize;

        [ReadOnly] public float maxOffset;
        [ReadOnly] public float fixedDeltaTime;
        [ReadOnly] public float precalculation;
        [ReadOnly] public float damping;
        [ReadOnly] public float propagationSpeed;
        [ReadOnly] public float speedTweak;

        public void Execute(int ghostIndex)
        {
            Utils.FromIndexToSampleIndices(ghostIndex, in ghostResolution, out int ghostSampleX, out int ghostSampleZ);

            int ghostResolutionXDiff = (ghostResolution.x - resolution.x) / 2;
            int ghostResolutionZDiff = (ghostResolution.z - resolution.z) / 2;

            // For normal cells
            if (ghostSampleX >= ghostResolutionXDiff && ghostSampleZ >= ghostResolutionZDiff
                && ghostSampleX < ghostResolution.x - ghostResolutionXDiff
                && ghostSampleZ < ghostResolution.z - ghostResolutionZDiff)
            {
                int index = Utils.FromGhostIndexToNoGhostIndex(ghostIndex, in resolution, in ghostResolution);

                // Ignore fixed samples
                if (fixedSamples[index] == 1)
                    return;

                float acceleration = accelerations[index];

                // Smooth heights
                float heightCorrection = 0;
                if (acceleration > maxOffset)
                    heightCorrection += acceleration - maxOffset;
                else if (acceleration < -maxOffset)
                    heightCorrection += acceleration + maxOffset;
                acceleration -= heightCorrection;

                // Velocity delta = Time * ( Acceleration - Damping Acceleration )
                velocities[index] += fixedDeltaTime * (precalculation * acceleration - velocities[index] * damping);
                float heightOffset = fixedDeltaTime * velocities[index] + heightCorrection;
                heights[ghostIndex] += heightOffset * speedTweak;

                // 1/2 vel*vel * mass. Consider mass to be 1 . Store as a 64bit integer
                AddCineticEnergy((long)((velocities[index] * velocities[index] * 0.5f) * 100000));
            }

            //TODO: this only works with 1 ghost cell
            // For ghost cells on extremes. Corners are not used, are not really meaningful for this calculation
            else
            {
                int neighbourX = ghostSampleX;
                int neighbourZ = ghostSampleZ;
                float size = sampleSize.x;

                if (ghostSampleX == 0) neighbourX++;
                else if (ghostSampleX == ghostResolution.x - 1) neighbourX--;

                if (ghostSampleZ == 0) { neighbourZ++; size = sampleSize.y; }
                else if (ghostSampleZ == ghostResolution.z - 1) { neighbourZ--; size = sampleSize.y; }

                int otherIndex = neighbourZ * ghostResolution.x + neighbourX;
                float nextPos = propagationSpeed * fixedDeltaTime;

                heights[ghostIndex] = (nextPos * heights[otherIndex] + heights[ghostIndex] * size) / (size + nextPos);

                /*
                TODO: Activate surface?
                // If the neighbour is not moving, converge to 0
                int velIndex = Utils.FromGhostIndexToNoGhostIndex(otherIndex, in resolution, in ghostResolution);
                if (math.abs(velocities[velIndex]) < 0.0001f)
                    heights[ghostIndex] *= 0.99f;
                */
            }
        }

        private unsafe void AddCineticEnergy(long value)
        {
            Interlocked.Add(ref ((long*)cineticEnergy.GetUnsafePtr())[0], value);
        }
    }
    
    [BurstCompile]
    public struct GradientsAndNormalsJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<Vector3> gradients;
        [WriteOnly] public NativeArray<Vector3> normals;

        [ReadOnly] public NativeArray<float> heights;
        [ReadOnly] public float2 sampleSize;
        [ReadOnly] public IntegerPair resolution;
        [ReadOnly] public IntegerPair ghostResolution;

        public void Execute(int index)
        {
            int heightsIndex = Utils.FromNoGhostIndexToGhostIndex(index, resolution, ghostResolution);

            float normalization = 1 / (sampleSize.x * 2);
            Vector3 gradient = new Vector3
            {
                x = (heights[heightsIndex - 1] - heights[heightsIndex + 1]) * normalization, // previous - next cell heights
                y = 0,
                z = (heights[heightsIndex - ghostResolution.x] - heights[heightsIndex + ghostResolution.x]) * normalization // bottom - top cell heights
            };

            normals[index] = Vector3.Cross(new Vector3(0, -gradient.z, 1), new Vector3(1, -gradient.x, 0));

            //NOTE: Y should be 0 but we take advantage of the job to calculate the normalization
            // To draw the gradient, multiply x and z by 1/magnitude and set Y to -Y.
            gradient.y = gradient.magnitude; 
            gradients[index] = gradient;
        }
    }
}

#endif