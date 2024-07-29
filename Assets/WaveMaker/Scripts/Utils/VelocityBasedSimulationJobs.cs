#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

using System.Threading;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

using Unity.Burst;
using Unity.Mathematics;

namespace WaveMaker
{
    [BurstCompile]
    public struct SampleVelocitiesJob : IJobParallelFor
    {
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float4> velocities;

        [ReadOnly] public NativeArray<NativeCollider> colliders;
        [ReadOnly] public Matrix4x4 l2wTransform;
        [ReadOnly] public quaternion w2lRotation;
        [ReadOnly] public WaveMakerSurface.VelocityData surfaceVelocityData;
        [ReadOnly] public NativeArray<WaveMakerSurface.VelocityData> interactorVelocitiesData;
        [ReadOnly] public NativeArray<int> fixedSamples;
        [ReadOnly] public IntegerPair resolution;
        [ReadOnly] public float2 sampleSize_ls;
        [ReadOnly] public float interactorMinimumSpeedClamp;
        [ReadOnly] public float interactorMaximumSpeedClamp;
        [ReadOnly] public IntegerPair areaResolution;
        [ReadOnly] public int xOffset;
        [ReadOnly] public int zOffset;

        public void Execute(int index)
        {
            // Calculate the index in the whole surface
            Utils.FromIndexToSampleIndices(in index, in areaResolution, out int x, out int z);
            x += xOffset; z += zOffset;
            index = Utils.FromSampleIndicesToIndex(resolution, x, z);

            if (fixedSamples[index] == 1)
                return;

            var samplePos_surfaceSpace = Utils.GetLocalPositionFromSample(index, in resolution, in sampleSize_ls);
            var samplePos_ws = new float4(l2wTransform.MultiplyPoint3x4(samplePos_surfaceSpace), 0);
            
            int nHits = 0;
            float4 velocitySum = float4.zero;

            for (int i = 0; i < colliders.Length; i++)
            {
                // Inside
                if (colliders[i].NearestPointFrom(samplePos_ws, 0, out _) < 0)
                {
                    nHits++;

                    var interactorVelocityData = interactorVelocitiesData[i];
                    float4 velocityAtPoint_interactor = Utils.VelocityAtPoint(samplePos_ws, interactorVelocityData.position, interactorVelocityData.angularVelocity, interactorVelocityData.linearVelocity);
                    float4 velocityAtPoint_surface = Utils.VelocityAtPoint(samplePos_ws, surfaceVelocityData.position, surfaceVelocityData.angularVelocity, surfaceVelocityData.linearVelocity);

                    // Apply only rotation, not scale
                    float4 speed_ls = new float4(math.mul(w2lRotation, (velocityAtPoint_interactor - velocityAtPoint_surface).xyz), 0);

                    float speedMag = math.length(speed_ls);

                    if (speedMag < interactorMinimumSpeedClamp)
                        continue;

                    if (speedMag > interactorMaximumSpeedClamp)
                        speed_ls = math.normalize(speed_ls) * interactorMaximumSpeedClamp;

                    velocitySum += speed_ls;
                }
            }

            velocities[index] = nHits > 0? velocitySum / nHits : float4.zero;
        }
    }


    [BurstCompile]
    public struct HeightsFromVelocitiesJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<float> heights;
        [ReadOnly] public NativeArray<float4> velocities;
        [ReadOnly] public float verticalPushScale;
        [ReadOnly] public float horizontalPushScale;
        [ReadOnly] public float fixedDeltaTime;
        [ReadOnly] public IntegerPair resolution;
        [ReadOnly] public IntegerPair resolutionGhost;
        [ReadOnly] public NativeArray<int> fixedSamples;
        [ReadOnly] public IntegerPair areaResolution;
        [ReadOnly] public int xOffset;
        [ReadOnly] public int zOffset;
        [WriteOnly] public NativeArray<long> isAwake;

        public void Execute(int index)
        {
            // Calculate the index in the whole surface
            Utils.FromIndexToSampleIndices(in index, in areaResolution, out int x, out int z);
            x += xOffset; z += zOffset;
            index = Utils.FromSampleIndicesToIndex(resolution, x, z);

            if (fixedSamples[index] == 1)
                return;

            var indexGhost = Utils.FromNoGhostIndexToGhostIndex(index, in resolution, in resolutionGhost);
            var velocity = velocities[index];

            // grow sample from current velocity
            var change = velocity.y * verticalPushScale * fixedDeltaTime;
            if (change < -1.1920929E-07F || change > 1.1920929E-07F)
            {
                heights[indexGhost] += change;
                ActivateAwakeStatus();
            }

            // grow or reduce sample depending on surrounding velocities
            ModifyHeightFromVelocity(in index, in indexGhost, -1, 1);
            ModifyHeightFromVelocity(in index, in indexGhost, 0, 1);
            ModifyHeightFromVelocity(in index, in indexGhost, 1, 1);

            ModifyHeightFromVelocity(in index, in indexGhost, -1, 0);
            ModifyHeightFromVelocity(in index, in indexGhost, 1, 0);

            ModifyHeightFromVelocity(in index, in indexGhost, -1, -1);
            ModifyHeightFromVelocity(in index, in indexGhost, 0, -1);
            ModifyHeightFromVelocity(in index, in indexGhost, 1, -1);
        }

        private void ModifyHeightFromVelocity(in int index, in int indexGhost, in int offsetX, in int offsetZ)
        {
            var otherIndex = Utils.GetOffsetSample_Untested(in resolution, in index, in offsetX, in offsetZ);
            if (otherIndex < 0)
                return;

            var otherVel = velocities[otherIndex];
            if (otherVel.x == 0 && otherVel.z == 0)
                return;

            var magnitude = math.length(otherVel) * horizontalPushScale* fixedDeltaTime;
            
            // scale by dot. The more direct it comes (grow) or goes away (reduce), the bigger the change.
            var change = GetVelocityDirectionValue(otherVel, -offsetX, -offsetZ) * magnitude;
            if (change > 1.1920929E-07F || change < -1.1920929E-07F) 
            {
                ActivateAwakeStatus();
                heights[indexGhost] += change;
            }
        }

        /// <returns>
        /// Positive from 0 to 1 when offset is on the same direction as the velocity. 1 when equal 0 degs
        /// Negative value from 0 to 1 when offset is contrary to the direction of the velocity. -1 when contrary 180 degs.
        /// </returns>
        internal static float GetVelocityDirectionValue(float4 velocity, in int offsetX, in int offsetZ)
        {
            velocity.y = 0;
            var otherVel_norm = math.normalizesafe(velocity);
            var otherSampleToCenter = math.normalize(new float4(offsetX, 0, offsetZ, 0));
            return math.dot(otherVel_norm, otherSampleToCenter);
        }

        private unsafe void ActivateAwakeStatus()
        {
            Interlocked.Exchange(ref ((long*)isAwake.GetUnsafePtr())[0], 1);
        }
    }
}

#endif