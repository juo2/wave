#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace WaveMaker
{
    //TODO: Obsolete
    /*
    [BurstCompile]
    public struct InteractionRaycastsPreparationJob : IJobParallelFor
    {
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<RaycastCommand> raycasts;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> heights;

        [ReadOnly] public Matrix4x4 l2wMatrix;
        [ReadOnly] public float2 sampleSize_ls;
        [ReadOnly] public Vector3 localScale;
        [ReadOnly] public IntegerPair resolution;
        [ReadOnly] public Vector3 rayUpDirection_ws;
        [ReadOnly] public LayerMask objectsMask;
        [ReadOnly] public IntegerPair areaResolution;
        [ReadOnly] public int xOffset;
        [ReadOnly] public int zOffset;
        [ReadOnly] public float detectionDepth_ls;
        [ReadOnly] public NativeArray<int> fixedSamples;
        [ReadOnly] public WaveMakerSurface.SupersamplingType supersamplingType;

        public void Execute(int raycastGroupIndex)
        {
            int sampleX = raycastGroupIndex % areaResolution.x + xOffset;
            int sampleZ = raycastGroupIndex / areaResolution.x + zOffset;
            int sampleIndex = Utils.FromSampleIndicesToIndex(resolution, sampleX, sampleZ);

            Vector3 top_ls = Utils.GetLocalPositionFromSample(sampleIndex, resolution, sampleSize_ls);
            Vector3 bottom_ls = top_ls;
            bottom_ls.y = -detectionDepth_ls;

            int extremeX = 0, extremeZ = 0;

            // TODO: Use RaycastCommandPairs?
            int firstRay = raycastGroupIndex * 2 * (int)supersamplingType;

            // 1 or more rays per sample
            for (int i = 0; i < (int)supersamplingType; i++)
            {
                var offset = Utils.GetRaycastOffset(supersamplingType, sampleSize_ls, i);

                // TODO: Use RaycastCommandPairs?
                var downwardRay = firstRay + i * 2;
                var upwardRay = firstRay + i * 2 + 1;

                // Add empty command for out of area raycasts
                if (Utils.IsSampleExtreme(sampleIndex, resolution, ref extremeX, ref extremeZ) &&
                    IsRaycastOutsideOfArea(in offset, in extremeX, in extremeZ))
                {
                    var ray = new RaycastCommand();
                    ray.maxHits = 1;

                    //TODO: Make this less error prone (order of rays in the commands)
                    raycasts[downwardRay] = ray;
                    raycasts[upwardRay] = ray;
                }

                // Add command
                else
                {
                    bottom_ls.x = bottom_ls.x + offset.x;
                    bottom_ls.z = bottom_ls.z + offset.z;
                    top_ls.x = top_ls.x + offset.x;
                    top_ls.z = top_ls.z + offset.z;

                    //TODO: Make this less error prone (order of rays in the commands)
                    AddRaycastCommand(in top_ls, downwardRay, -rayUpDirection_ws);
                    AddRaycastCommand(in bottom_ls, upwardRay, rayUpDirection_ws);
                }
            }
        }

        internal static bool IsRaycastOutsideOfArea(in Vector3 offset, in int extremeX, in int extremeZ)
        {
            return (extremeX == -1 && offset.x < 0) ||
                    (extremeX == 1 && offset.x > 0) ||
                    (extremeZ == -1 && offset.z < 0) ||
                    (extremeZ == 1 && offset.z > 0);
        }

        private void AddRaycastCommand(in Vector3 rayOrigin_ls, int raycastIndex, in Vector3 direction)
        {
            var rayOrigin_ws = l2wMatrix.MultiplyPoint3x4(rayOrigin_ls);
            var detectionDepth_ws = detectionDepth_ls * localScale.y;

            //NOTE: Max hits does not work. We use a multihit solution to fix this problem afterwards.
            raycasts[raycastIndex] = new RaycastCommand(rayOrigin_ws, direction, detectionDepth_ws, objectsMask, 1);
        }
    }

    [BurstCompile]
    public struct OccupancyJob : IJobParallelFor
    {
        // Write
        [NativeDisableParallelForRestriction] public NativeArray<InteractionData> interactionData;
        [NativeDisableParallelForRestriction] public NativeArray<float> occupancy;
        [NativeDisableParallelForRestriction] unsafe public NativeArray<int> hitsPerObject;

        // Read
        [ReadOnly] public RaycastCommandPairs raycasts;
        [ReadOnly] public RaycastCommandPairsResults raycastResults;
        [ReadOnly] public bool affectSurface;
        [ReadOnly] public bool buoyancyEnabled;
        [ReadOnly] public NativeHashMap<int, int> colliderIdsToIndices;
        [ReadOnly] public NativeHashMap<int, int> colliderToRbIndices;
        [ReadOnly] public int surfaceColliderID;
        [ReadOnly] public Matrix4x4 w2l_matrix;
        [ReadOnly] public Vector3 localScale;
        [ReadOnly] public float2 surfaceSize_ls;
        [ReadOnly] public IntegerPair resolution;
        [ReadOnly] public float2 sampleSize;
        [ReadOnly] public int nMaxCellsPerInteractor;
        [ReadOnly] public float detectionDepth_ls;
        
        public void Execute(int rayIndex)
        {
            if (localScale.y <= 0)
                return;

            // Find the index of this sample/cell in the whole results array
            float3 pos_ls = w2l_matrix.MultiplyPoint(raycasts.GetDownwardRay(rayIndex).from);
            int cellIndex = Utils.GetNearestSampleFromLocalPosition(pos_ls, surfaceSize_ls, sampleSize);
            
#if NATIVEHASHMAP_COUNT
            int nInteractors = colliderIdsToIndices.Count();
#else
            int nInteractors = colliderIdsToIndices.Length;
#endif

            //NOTE: Dispose doesn't have to be called inside jobs even though documentation says so. This is said by Joaquim Ante (CTO) in the forums
            var dataPerInteractor = new NativeArray<InteractionData>(nInteractors, Allocator.Temp);
            var botTopHitOrder = new NativeList<int>(nInteractors, Allocator.Temp);

            for (int i = 0; i < dataPerInteractor.Length; i++)
                dataPerInteractor[i] = InteractionData.Null;
            
            CalculateInteractorOccupancies(cellIndex, rayIndex, surfaceColliderID, in localScale, in detectionDepth_ls, 
                                            in raycastResults, in colliderIdsToIndices, ref dataPerInteractor, ref botTopHitOrder);

            if (botTopHitOrder.Length > 0)
            {
                if (affectSurface)
                    CalculateGlobalOccupancy(in cellIndex, in botTopHitOrder, in dataPerInteractor, ref occupancy);

                if (buoyancyEnabled)
                {
                    CollapseOccupanciesByRigidBody(ref dataPerInteractor, in colliderToRbIndices, in botTopHitOrder);
                    CopyRigidBodyHitsToFinalArray(in dataPerInteractor);
                }
            }
        }

        // Compares results for the current cell (upwards and downwards rays), and calculates how much each interactor occupies, ignoring overlap
        internal static void CalculateInteractorOccupancies(in int cellIndex, in int rayIndex, in int surfaceColliderID, in Vector3 localScale, 
                                                            in float detectionDepth_ls, in RaycastCommandPairsResults results, in NativeHashMap<int, int> colliderIdsToIndices, 
                                                            ref NativeArray<InteractionData> localInteractionData, ref NativeList<int> botTopHitOrder)
        {
            // Add all hits downwards
            // NOTE: When writing for Burst, you should avoid the C# iterators and almost all of the related features (IEnumerable, LINQ, .NET collections, foreach, etc).
            for (int i = 0; i < results.nHitsPerRay; i++)
            {
                var result = results.GetDownwardResult(rayIndex, i);

                int colliderInstanceId = Utils.GetColliderID(result);
                if (colliderInstanceId == 0)
                    break;

                if ((surfaceColliderID == colliderInstanceId) || !colliderIdsToIndices.TryGetValue(colliderInstanceId, out int colliderNativeId))
                    continue;

                float dist_ls = result.distance / localScale.y;
                localInteractionData[colliderNativeId] = new InteractionData(cellIndex, detectionDepth_ls - dist_ls, 0);
            }

            // Compare hits upwards
            for (int i = 0; i < results.nHitsPerRay; i++)
            {
                var result = results.GetUpwardResult(rayIndex, i);

                int colliderInstanceId = Utils.GetColliderID(result);
                if (surfaceColliderID == colliderInstanceId || colliderInstanceId == 0)
                    break;

                if ((surfaceColliderID == colliderInstanceId) || !colliderIdsToIndices.TryGetValue(colliderInstanceId, out int colliderNativeId))
                    continue;

                float dist_ls = result.distance / localScale.y;
                float newOcc;

                if (localInteractionData[colliderNativeId].IsNull)
                    newOcc = detectionDepth_ls - dist_ls; // This object touches the surface in this cell
                else
                    newOcc = localInteractionData[colliderNativeId].occupancy - dist_ls;

                localInteractionData[colliderNativeId] = new InteractionData(cellIndex, newOcc, dist_ls);
                botTopHitOrder.AddNoResize(colliderNativeId);
            }

            //TODO: Find a better way to add the bottom items at the beginning of the list
            // Turn list around, top to bottom
            TurnAround(ref botTopHitOrder);

            // Add bottom items at the end of the list
            for (int colliderNativeId = 0; colliderNativeId < localInteractionData.Length; ++colliderNativeId)
                if (!localInteractionData[colliderNativeId].IsNull && localInteractionData[colliderNativeId].distance == 0)
                    botTopHitOrder.Add(colliderNativeId);

            // Turn list around again to make it bottom to top again
            TurnAround(ref botTopHitOrder);
        }
        
        internal static void CalculateGlobalOccupancy(in int cellIndex, in NativeList<int> botTopHitOrder, in NativeArray<InteractionData> localInteractionData, ref NativeArray<float> occupancy)
        {
            float finalOcc = 0;
            float minNextDistance = 0;

            for (int i = 0; i < botTopHitOrder.Length; i++)
            {
                var interactorNativeId = botTopHitOrder[i];
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

        internal static void CollapseOccupanciesByRigidBody(ref NativeArray<InteractionData> dataPerInteractor, in NativeHashMap<int, int> colliderToRbIndices, in NativeList<int> botTopHitOrder)
        {
            int nInteractors = dataPerInteractor.Length;

            // Store which is the first collider per rigid body detected to store the sum of the collapsed colliders
            var firstColliderPerRb = new NativeArray<int>(nInteractors, Allocator.Temp);
            for (int i = 0; i < nInteractors; i++)
                firstColliderPerRb[i] = -1;

            for (int i = 0; i < botTopHitOrder.Length; i++)
            {
                var colliderIndex = botTopHitOrder[i];

                // Ignore colliders without rididbodies. Delete data
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
                    dataPerInteractor[firstIndex] = new InteractionData(firstData.cellIndex, newOcc, firstData.distance);
                    dataPerInteractor[colliderIndex] = InteractionData.Null;
                }
            }
            
            firstColliderPerRb.Dispose();
        }

        internal void CopyRigidBodyHitsToFinalArray(in NativeArray<InteractionData> dataPerInteractor)
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

        static void TurnAround(ref NativeList<int> list)
        {
            for (int i = 0; i < list.Length / 2; i++)
            {
                int last = list.Length - 1 - i;
                int aux = list[last];
                list[last] = list[i];
                list[i] = aux;
            }
        }
    }
    */

    [BurstCompile]
    public struct FinishOccupancyJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<InteractionData> interactionData;

        [ReadOnly] public NativeArray<int> hitsPerObject;
        [ReadOnly] public int nMaxCellsPerInteractor;

        public void Execute(int index)
        {
            var nHits = hitsPerObject[index];

            if (nHits > 0 && nHits < nMaxCellsPerInteractor)
                InteractionDataArray.SetNull(ref interactionData, nMaxCellsPerInteractor, index, nHits);
        }
    }

    [BurstCompile]
    public struct OccupancyEffectJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<float> heights;
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float> occupancy;
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float> occupancyPrevious;
        [ReadOnly] public NativeArray<int> fixedSamples;
        [ReadOnly] public IntegerPair ghostResolution;
        [ReadOnly] public IntegerPair resolution;
        [ReadOnly] public float effectScale;
        [ReadOnly] public float sleepThreshold;
        [WriteOnly] public NativeArray<long> isAwake;

        public void Execute(int index)
        {
            if (fixedSamples[index] == 1)
                return;

            float heightOffset = 0;

            Utils.FromIndexToSampleIndices(in index, in resolution, out int x, out int z);

            // Left
            if (x > 0)
            {
                int indexAux = Utils.FromSampleIndicesToIndex(resolution, x - 1, z);
                heightOffset += (occupancy[indexAux] - occupancyPrevious[indexAux]) * 0.25f;
            }

            // right
            if (x < resolution.x - 1)
            {
                int indexAux = Utils.FromSampleIndicesToIndex(resolution, x + 1, z);
                heightOffset += (occupancy[indexAux] - occupancyPrevious[indexAux]) * 0.25f;
            }

            // bottom
            if (z > 0)
            {
                int indexAux = Utils.FromSampleIndicesToIndex(resolution, x, z - 1);
                heightOffset += (occupancy[indexAux] - occupancyPrevious[indexAux]) * 0.25f;
            }

            // top
            if (z < resolution.z - 1)
            {
                int indexAux = Utils.FromSampleIndicesToIndex(resolution, x, z + 1);
                heightOffset += (occupancy[indexAux] - occupancyPrevious[indexAux]) * 0.25f;
            }

            if (heightOffset > sleepThreshold || heightOffset < -sleepThreshold)
            {
                int heightsIndex = Utils.FromNoGhostIndexToGhostIndex(index, in resolution, in ghostResolution);
                heights[heightsIndex] += heightOffset * effectScale;

                ActivateAwakeStatus();
            }
        }

        private unsafe void ActivateAwakeStatus()
        {
            Interlocked.Exchange(ref ((long*)isAwake.GetUnsafePtr())[0], 1);
        }
    }


}

#endif