#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

using System.Collections.Generic;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace WaveMaker
{

    public class RaymarchUtils
    {
        public struct HitDistance
        {
            public int colliderIndex;
            public float distanceFromBottom;

            public HitDistance(int colIndex, float dist)
            {
                colliderIndex = colIndex;
                distanceFromBottom = dist;
            }

            public struct SortByDistance : IComparer<HitDistance>
            {
                int IComparer<HitDistance>.Compare(HitDistance a, HitDistance b)
                {
                    if (a.distanceFromBottom > b.distanceFromBottom)
                        return 1;
                    if (a.distanceFromBottom < b.distanceFromBottom)
                        return -1;
                    else
                        return 0;
                }
            }
        }

        [BurstCompile]
        public struct RaymarchOccupancy : IJobParallelFor
        {
            // Write
            [NativeDisableParallelForRestriction] public NativeArray<InteractionData> interactionData;
            [NativeDisableParallelForRestriction] public NativeArray<float> occupancy;
            [NativeDisableParallelForRestriction] unsafe public NativeArray<int> hitsPerObject;

            // Read
            [ReadOnly] public NativeArray<NativeCollider> colliders;
            [ReadOnly] public float4x4 _l2wTransformMatrix;
            [ReadOnly] public IntegerPair resolution;
            [ReadOnly] public float2 sampleSize;
            [ReadOnly] public float detectionDepth_ls;
            [ReadOnly] public IntegerPair areaResolution;
            [ReadOnly] public IntegerPair offset;
            [ReadOnly] public bool affectSurface;
            [ReadOnly] public bool buoyancyEnabled;
            [ReadOnly] public NativeHashMap<int, int> colliderToRbIndices;
            [ReadOnly] public int nMaxCellsPerInteractor;
            [ReadOnly] public float smoothedBorder;

            public void Execute(int cellIndex)
            {
                // Calculate the index in the whole surface
                int x = cellIndex % areaResolution.x + offset.x;
                int z = cellIndex / areaResolution.x + offset.z;
                cellIndex = resolution.x * z + x;

                occupancy[cellIndex] = 0;

                //NOTE: Dispose doesn't have to be called inside jobs even though documentation says so. This is said by Joaquim Ante (CTO) in the forums
                var dataPerInteractor = new NativeArray<InteractionData>(colliders.Length, Allocator.Temp);
                var botTopHitOrder = new NativeList<HitDistance>(colliders.Length, Allocator.Temp);

                float4 origin_ws = math.mul(_l2wTransformMatrix, new float4(x * sampleSize.x, -detectionDepth_ls, z * sampleSize.y, 1));
                float4 direction_ws = math.mul(_l2wTransformMatrix, new float4(0, 1, 0, 0));
                bool anyHit = false;

                for (int i = 0; i < colliders.Length; i++)
                {
                    //TODO: Detection depth local space?
                    float occ = CalculateOccupancy(colliders[i], origin_ws, direction_ws, detectionDepth_ls, smoothedBorder, out float hitDistance_ws);

                    if (occ > 0)
                    {
                        anyHit = true;
                        dataPerInteractor[i] = new InteractionData(cellIndex, occ, hitDistance_ws);
                        botTopHitOrder.AddNoResize(new HitDistance(i, hitDistance_ws));
                    }
                    else
                        dataPerInteractor[i] = InteractionData.Null;
                }

                if (anyHit)
                {
                    //TODO: Pass to the job instead of creating one
                    botTopHitOrder.Sort(new HitDistance.SortByDistance());

                    if (affectSurface)
                        CalculateGlobalOccupancy(in cellIndex, in botTopHitOrder, in dataPerInteractor, ref occupancy);
                    
                    if (buoyancyEnabled)
                    {
                        CollapseOccupanciesByRigidBody(ref dataPerInteractor, in colliderToRbIndices, in botTopHitOrder);
                        CopyHitsToFinalArray(in dataPerInteractor);
                    }
                }
            }

            internal static void CalculateGlobalOccupancy(in int cellIndex, in NativeList<HitDistance> botTopHitOrder, 
                                                in NativeArray<InteractionData> localInteractionData, ref NativeArray<float> occupancy)
            {
                float finalOcc = 0;
                float minNextDistance = 0;

                for (int i = 0; i < botTopHitOrder.Length; i++)
                {
                    var interactorNativeId = botTopHitOrder[i].colliderIndex;
                    var occ = localInteractionData[interactorNativeId].occupancy;
                    var dist = localInteractionData[interactorNativeId].distance;

                    // collider ends before the previous ended (overlapped), ignore
                    if (dist + occ < minNextDistance)
                        continue;

                    // if overlapped at the beginning
                    if (dist < minNextDistance)
                        finalOcc += dist + occ - minNextDistance; // Add the rest of the space
                    else
                        finalOcc += occ;

                    minNextDistance = dist + occ;
                }

                occupancy[cellIndex] = finalOcc;
            }

            internal void CopyHitsToFinalArray(in NativeArray<InteractionData> dataPerInteractor)
            {
                // Only the data for the collapsed rigidbodies is left. The rest is null
                for (int i = 0; i < dataPerInteractor.Length; i++)
                {
                    if (!dataPerInteractor[i].IsNull)
                        AddHit(i, dataPerInteractor[i]);
                }
            }

            internal unsafe void AddHit(int colliderIndex, in InteractionData data)
            {
                //NOTE: We always increment for new hits. It is an unsafe operation because it is shared by all samples in the grid
                int currentNHits = Interlocked.Increment(ref ((int*)hitsPerObject.GetUnsafePtr())[colliderIndex]);
                if (currentNHits > nMaxCellsPerInteractor)
                    return;

                InteractionDataArray.AddHit(ref interactionData, nMaxCellsPerInteractor, colliderIndex, 
                                            currentNHits - 1, data.cellIndex, data.occupancy, data.distance);
            }

            internal static void CollapseOccupanciesByRigidBody(ref NativeArray<InteractionData> dataPerInteractor, 
                                                    in NativeHashMap<int, int> colliderToRbIndices, in NativeList<HitDistance> botTopHitOrder)
            {
                int nInteractors = dataPerInteractor.Length;

                // Store which is the first collider per rigid body detected to store the sum of the collapsed colliders
                var firstColliderPerRb = new NativeArray<int>(nInteractors, Allocator.Temp);
                for (int i = 0; i < nInteractors; i++)
                    firstColliderPerRb[i] = -1;

                for (int i = 0; i < botTopHitOrder.Length; i++)
                {
                    var colliderIndex = botTopHitOrder[i].colliderIndex;

                    // Ignore colliders without rididbodies, delete data
                    if (!colliderToRbIndices.TryGetValue(colliderIndex, out int rbIndex))
                    {
                        dataPerInteractor[colliderIndex] = InteractionData.Null;
                        continue;
                    }

                    // If it is the first collider from this rb we find
                    int firstIndex = firstColliderPerRb[rbIndex];
                    if (firstIndex == -1)
                    {
                        firstColliderPerRb[rbIndex] = colliderIndex;
                        continue;
                    }

                    InteractionData firstData = dataPerInteractor[firstIndex];
                    InteractionData currentData = dataPerInteractor[colliderIndex];

                    //TODO: Consider ignore objects touching bottom?

                    // No overlap, we start counting from this new collider
                    if (firstData.distance + firstData.occupancy < currentData.distance)
                        firstColliderPerRb[rbIndex] = colliderIndex;

                    // Overlapped fully, delete data
                    else if (firstData.distance + firstData.occupancy >= currentData.distance + currentData.occupancy)
                        dataPerInteractor[colliderIndex] = InteractionData.Null;

                    // Overlapped partially, add occupancy
                    else if (firstData.distance + firstData.occupancy < currentData.distance + currentData.occupancy)
                    {
                        float newOcc = currentData.distance + currentData.occupancy - firstData.distance;
                        dataPerInteractor[firstIndex] = new InteractionData (firstData.cellIndex, newOcc, firstData.distance);
                        dataPerInteractor[colliderIndex] = InteractionData.Null;
                    }
                }

                firstColliderPerRb.Dispose();
            }

        }

        //TODO WARNING Doesn't support thickness == 0
        public static float CalculateOccupancy(NativeCollider collider, float4 bottomOrigin_ws, float4 upDir_ws, 
                                                float volumeDepth, float thickness, out float hitDistance_ws)
        {
            // Adjust depth adding thickness
            bottomOrigin_ws -= upDir_ws * thickness;
            volumeDepth += thickness * 2;

            // Hit with thickened collider from bottom
            var horizontalDistFromBottom = Raymarch(bottomOrigin_ws, upDir_ws, collider, out float4 bottomHit_ws, out float hitDistFromBottom_ws, thickness);
            hitDistance_ws = hitDistFromBottom_ws;

            // No hit (Out of the water area or too far horizontally)
            if (horizontalDistFromBottom > 0 || horizontalDistFromBottom < -1 || hitDistance_ws > volumeDepth)
                return 0;

            // Already inside
            if (horizontalDistFromBottom == -1)
                bottomHit_ws = bottomOrigin_ws;

            // Continue from thickened object hit to internal collider hit
            horizontalDistFromBottom = Raymarch(bottomHit_ws, upDir_ws, collider, out float4 _, out _);

            float4 topOrigin_ws = bottomOrigin_ws + upDir_ws * volumeDepth;
            Raymarch(topOrigin_ws, -upDir_ws, collider, out float4 _, out float hitDistFromTop_ws, thickness);
            // If we hit or are inside the collider, weight is 1. Otherwise, weight is smaller
            float weight = math.clamp(1 - horizontalDistFromBottom / thickness, 0, 1);

            // Calculate occupancy with bottom and top hits
            hitDistFromTop_ws = math.clamp(hitDistFromTop_ws - thickness, 0, float.MaxValue);
            hitDistFromBottom_ws = math.clamp(hitDistFromBottom_ws - thickness, 0, float.MaxValue);
            float fullOcc = volumeDepth - thickness * 2 - hitDistFromBottom_ws - hitDistFromTop_ws;
            float occ = weight * fullOcc;

            // Fix hit distance with the weighted occupancy removed and the thickness added previously
            hitDistance_ws += (fullOcc - occ) * 0.5f - thickness;

            return occ;
        }

        /// <returns> 
        /// If not hit, minimum distance to collider + thickness + contact offset. 
        /// If hit, returns 0, and the new hit pos.
        /// If already inside object or , returns -1 and the same origin as hitPos
        /// If object in the back of the ray, returns a big negative value, and same origin as hitPos.
        /// HitDist is always 0 unless there is a hit
        /// </returns>
        public static float Raymarch(in float4 origin_ws, in float4 dir_ws, in NativeCollider collider, out float4 hitPos_ws, out float hitDist_ws, float thickness = 0, float thresholdDistance = 0.01f)
        {
            float4 currentPos_ws = origin_ws;
            float minStepDistance = float.MaxValue;
            hitPos_ws = currentPos_ws;
            hitDist_ws = 0;

            float nextStepDistance = collider.NearestPointFrom(origin_ws, thickness, out float4 firstHit_ws);

            // Already inside the object (or thickened object)
            if (nextStepDistance <= thresholdDistance)
                return -1;

            // the object is in the other side and not inside. No hit.
            if (math.dot(firstHit_ws - origin_ws, dir_ws) < 0)
                return float.MinValue;

            // While next step doesn't grow and it's not too far
            while (nextStepDistance < minStepDistance)
            {
                minStepDistance = nextStepDistance;

                // Advance
                float stepSize = nextStepDistance;
                currentPos_ws.x += dir_ws.x * stepSize;
                currentPos_ws.y += dir_ws.y * stepSize;
                currentPos_ws.z += dir_ws.z * stepSize;
                hitDist_ws += nextStepDistance;

                nextStepDistance = collider.NearestPointFrom(currentPos_ws, thickness, out _);

                // object hit (or thickened object)
                if (nextStepDistance < thresholdDistance)
                {
                    hitPos_ws = currentPos_ws;
                    return 0;
                }
            }

            // Exactly in the border or no hit
            hitPos_ws = currentPos_ws;
            return minStepDistance;
        }
    }
}
#endif