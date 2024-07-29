// Code by www.lidia-martinez.com . Get updated versions there. Use it freely.

using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WaveMaker
{

    /// <summary>
    /// This is a workaround to the very serious problem that RaycastCommand doesn't use "maxhits" parameter even though
    /// it is available in the API and the Docs. It always returns one hit maximum. 
    /// Here we can perform several hits and combine them. Depending on the number of commands and hits, it will get a lot slower
    /// than the original RaycastCommand.ScheduleBatch. Try to keep the values as low as possible.
    /// 
    /// TODO: Restore raycastCommands data after the process, instead of just the FROM position without additional overhead.
    /// TODO: Stop the process if all hits failed. A shared boolean or counter would not work in this chain of already created jobs.
    /// </summary>
    public static class RaycastCommandMultihit
    {
        /// <summary>
        /// This can be created beforehand and passed to RaycastCommand MultiHit functions
        /// to allow persistent Native data to be stored and reused if the commands array size doesn't change.
        /// </summary>
        public struct PersistentData
        {
            NativeArray<RaycastHit> intermediateResults;
            NativeArray<int> nHits;
            NativeArray<float> distanceSum;
            NativeArray<int> realIndices;

            public NativeArray<RaycastHit> IntermediateResults { get => intermediateResults; }
            public NativeArray<int> NHits { get => nHits; }
            public NativeArray<float> DistanceSum { get => distanceSum; }
            public NativeArray<int> RealIndices { get => realIndices; }

            /// <summary>
            /// Will create the necessary sizes for each array
            /// </summary>
            public PersistentData(in NativeArray<RaycastCommand> commands, Allocator allocator)
            {
                intermediateResults = new NativeArray<RaycastHit>(commands.Length, allocator);
                nHits = new NativeArray<int>(commands.Length, allocator);
                distanceSum = new NativeArray<float>(commands.Length, allocator);
                realIndices = new NativeArray<int>(commands.Length, allocator);
            }

            public void Dispose()
            {
                if (intermediateResults == null || !intermediateResults.IsCreated)
                    return;

                intermediateResults.Dispose();
                distanceSum.Dispose();
                nHits.Dispose();
                realIndices.Dispose();
            }
        }

        ///<summary>Similar than RaycastCommand.ScheduleBatch but maxHits is supported. This method is much slower, creates all data structs inside and disposes them, calling Complete. Good when used few times.</summary>
        /// <param name="commands">IMPORTANT: Ensure that maxHits are 1 in each RaycastCommand in this array. 
        /// Contents will be deleted or changed, so copy the contents before calling the function if you need them.</param>
        /// <param name="results">Same format than RacastCommand results. Indexing: Ray index * maxHits + hit index</param>
        /// <param name="maxHits">Max hits allowed per command</param>
        /// <param name="minCommandsPerJob">Batch size for each job. For this size of job, around 32 and 64 is fine.</param>
        /// <param name="minStep">A small distance to add for each subsequent ray, to avoid hitting the same objects again</param>
        public static JobHandle ScheduleBatch(NativeArray<RaycastCommand> commands, NativeArray<RaycastHit> results, int minCommandsPerJob, int maxHits, JobHandle dependsOn = default, float minStep = 0.0001f)
        {
            if (maxHits <= 0)
                throw new System.ArgumentException("maxHits should be greater than zero");

            if (results.Length < commands.Length * maxHits)
                throw new System.ArgumentException("Results array length does not match maxHits count");

            if (minStep < 0f)
                throw new System.ArgumentException("minStep should be more or equal to zero");

            if (maxHits == 1)
                return RaycastCommand.ScheduleBatch(commands, results, minCommandsPerJob, dependsOn);

            PersistentData data = new PersistentData(commands, Allocator.TempJob);

            dependsOn = ScheduleBatch(commands, results, minCommandsPerJob, maxHits, ref data, dependsOn, minStep);

            dependsOn.Complete();
            data.Dispose();

            return dependsOn;
        }

        ///<summary>Similar than RaycastCommand.ScheduleBatch but maxHits is supported. This function is faster. Passing already created and reusing arrays.</summary>
        /// <param name="commands">IMPORTANT: Ensure that maxHits are 1 in each RaycastCommand in this array. 
        /// Contents will be deleted or removed, so copy the contents before calling the function</param>
        /// <param name="results">Same format than RacastCommand results. Indexing: Ray index * maxHits + hit index</param>
        /// <param name="maxHits">Max hits allowed per command</param>
        /// <param name="minCommandsPerJob">Batch size for each job. For this size of job, around 32 and 64 is fine.</param>
        /// <param name="minStep">A small distance to add for each subsequent ray, to avoid hitting the same objects again</param>
        /// <param name="data">An initialized struct of data needed to be reused. Otherwise, use the other function</param>
        public static JobHandle ScheduleBatch(NativeArray<RaycastCommand> commands, NativeArray<RaycastHit> results, int minCommandsPerJob, int maxHits,
                                            ref PersistentData data, JobHandle dependsOn = default, float minStep = 0.0001f)
        {
            if (maxHits <= 0)
                throw new System.ArgumentException("maxHits should be greater than zero");

            if (results.Length < commands.Length * maxHits)
                throw new System.ArgumentException("Results array length does not match maxHits count");

            if (minStep < 0f)
                throw new System.ArgumentException("minStep should be more or equal to zero");

            if (maxHits == 1)
                return RaycastCommand.ScheduleBatch(commands, results, minCommandsPerJob, dependsOn);

            if (data.IntermediateResults == null || !data.IntermediateResults.IsCreated || data.IntermediateResults.Length < commands.Length)
                throw new System.ArgumentException("Input persistent data is not well initialized or do not have the same lenght as the commands array");
            
            int counter = 0;
            while (counter < maxHits)
            {
                dependsOn = RaycastCommand.ScheduleBatch(commands, data.IntermediateResults, minCommandsPerJob, dependsOn);

                // Read results, if any hit, create a new command for the new batch. Otherwise, create a default RaycastCommand.
                var job = new GatherHitsAndCreateNewCommands
                {
                    commands = commands,
                    results = data.IntermediateResults,
                    finalResults = results,
                    distanceSum = data.DistanceSum,
                    nHits = data.NHits,
                    minStep = minStep,
                    maxHits = maxHits,
                    iterationIndex = counter
                };
                dependsOn = job.Schedule(commands.Length, minCommandsPerJob, dependsOn);

                // Every six hits, schedule
                if (counter % 6 == 0)
                    JobHandle.ScheduleBatchedJobs();

                counter++;
            }

            return dependsOn;
        }


        [BurstCompile]
        private struct GatherHitsAndCreateNewCommands : IJobParallelFor
        {
            /// <summary> The last batch commands </summary>
            public NativeArray<RaycastCommand> commands;

            /// <summary> Results returned by the last batch </summary>
            [ReadOnly] public NativeArray<RaycastHit> results;

            /// <summary> To write only succesful intermediate hits if any </summary>
            [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<RaycastHit> finalResults;

            /// <summary> Distance passed to the actual hits (ignoring small offsets added) </summary>
            [NativeDisableParallelForRestriction] public NativeArray<float> distanceSum;

            /// <summary> Stored for each ray to choose the correct position on the results array </summary>
            [NativeDisableParallelForRestriction] public NativeArray<int> nHits;

            /// <summary> A small offset added to each ray after a hit (to avoid hitting the same items) </summary>
            [ReadOnly] public float minStep;

            /// <summary> Number of hits at the end </summary>
            [ReadOnly] public int maxHits;

            [ReadOnly] public int iterationIndex;

            public void Execute(int index)
            { 
                // Initialization
                if (iterationIndex == 0)
                    nHits[index] = 0;

                var command = commands[index];
                var result = results[index];
                var numHits = nHits[index];
                var distUntilNow = distanceSum[index];

                // No hit, then do not execute this ray anymore in the next batch
                int colliderInstanceId = GetColliderID(result);
                if (colliderInstanceId == 0)
                {
                    if (command.maxHits > 0)
                    {
                        command.maxHits = 0; // make next command cheaper
                        commands[index] = command;
                        finalResults[index * maxHits + numHits] = default;
                    }
                }

                // Hit happened
                else
                {
                    // Add fake minstep por the first hit that didn't add it
                    if (numHits == 0)
                    {
                        result.distance -= minStep;
                        command.from += command.direction * minStep;
                        command.distance -= minStep;
                    }

                    // Modify next raycast, moving forward a little bit using "minStep"
                    command.from = command.from + command.direction * (result.distance + minStep);
                    command.distance = command.distance - result.distance - minStep;
                    commands[index] = command;
                    distanceSum[index] += result.distance + minStep;

                    // Store hit
                    result.distance = distanceSum[index];
                    finalResults[index * maxHits + numHits] = result;
                    nHits[index] = numHits + 1;
                }
            }

            //NOTE: This hack is needed to be able to access 
            // the raycashit collider from raycast hits returned by RaycastCommand.Schedule
            // in order to take advantage of jobs. A UnityEngine class is not accessible from a job.
            /// TODO: Unity support team said that this is fixed in 2021.2 version. In the meantime, use this
            [StructLayout(LayoutKind.Sequential)]
            private struct RaycastHitPublic
            {
                public Vector3 m_Point;
                public Vector3 m_Normal;
                public int m_FaceID;
                public float m_Distance;
                public Vector2 m_UV;
                public int m_ColliderID;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GetColliderID(RaycastHit hit)
            {
                unsafe
                {
                    RaycastHitPublic h = *(RaycastHitPublic*)&hit;
                    return h.m_ColliderID;
                }
            }
        }
    }
}