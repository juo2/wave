//TODO: Obsolete
/*
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace WaveMaker
{
    /// <summary>
    /// Wrapper over the list of raycasts to make it easier to access items in the complicated struct
    /// </summary>
    public struct RaycastCommandPairs
    {
        /// <summary>
        /// Pairs or downward-upward rays in the same position.
        /// </summary>
        NativeList<RaycastCommand> commands;

        public int NumRaycasts { get{ return commands.Length; } }
        public int NumRaycastPairs { get { return commands.Length / 2; } }

        public RaycastCommandPairs(int nRays = 0)
        {
            commands = new NativeList<RaycastCommand>(nRays * 2, Allocator.Persistent);

            //NOTE: Manual initialization NEEDED to fix crash with jobs + resizeUninitialized . Fixed in 2018.4.30f (not exactly that version)
            //https://fogbugz.unity3d.com/default.asp?1300350_mgfk5fnfdkqsd6fk
            
            for (int i = 0; i < commands.Capacity; i++)
            {
                // We need to create a raycastcommand with the exact number of hits to avoid problems with the bug mentioned.
                // NOTE: maxhits set to one. RaycastCommand can't take multiple hits even though it is documented. A multihit solution is applied here.
                commands.Add(new RaycastCommand(Vector3.zero, Vector3.zero, 0, default, 1));
            }
        }

        public void SetDownwardRay(int index, in RaycastCommand command)
        {
            commands[index * 2] = command;
        }

        public void SetUpwardRay(int index, in RaycastCommand command)
        {
            commands[index * 2 + 1] = command;
        }

        public RaycastCommand GetDownwardRay(int index)
        {
            return commands[index * 2];
        }

        public RaycastCommand GetUpwardRay(int index)
        {
            return commands[index * 2 + 1];
        }

        public void ResizeUninitialized(int newSize)
        {
            // TODO: Improve using array and GetSubArray in 2019
            commands.ResizeUninitialized(newSize * 2);
        }

        public IEnumerable<RaycastCommand> GetDownwardRays()
        {
            for (int i = 0; i < commands.Length; i += 2)
                yield return commands[i];
        }

        public IEnumerable<RaycastCommand> GetUpwardRays()
        {
            for (int i = 1; i < commands.Length; i += 2)
                yield return commands[i];
        }

        public void Dispose()
        {
            if (commands.IsCreated)
                commands.Dispose();
        }

        public static implicit operator NativeArray<RaycastCommand>(RaycastCommandPairs input)
        {
            return input.commands.AsArray();
        }

        public static implicit operator NativeList<RaycastCommand>(RaycastCommandPairs input)
        {
            return input.commands;
        }
    }

    public struct RaycastCommandPairsResults
    {
        /// <summary>
        /// Format of storage is:
        /// First Pair or rays: Downward Ray results first, then Upward Ray results. Then second ray...
        /// </summary>
        NativeArray<RaycastHit> results;
        public int nHitsPerRay;

        public int Length { get { return results.Length / (2 * nHitsPerRay); } }

        public RaycastCommandPairsResults(int nRays, int nHitsPerRay)
        {
            results = new NativeArray<RaycastHit>( nRays * nHitsPerRay * 2, Allocator.Persistent);
            this.nHitsPerRay = nHitsPerRay;

            //TODO: Not sure if this is still needed now that we don't resize the results array
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < results.Length; i++)
                results[i] = default;
        }

        int DownwardsHitsStart(int rayIndex)
        {
            return rayIndex * nHitsPerRay * 2;
        }

        int UpwardsHitsStart(int rayIndex)
        {
            return rayIndex * nHitsPerRay * 2 + nHitsPerRay;
        }

        public IEnumerable<RaycastHit> GetDownwardResults(int rayIndex, bool stopIfNullHit = true)
        {
            int startIndex = DownwardsHitsStart(rayIndex);
            return GetResults(startIndex, stopIfNullHit);
        }
        
        public IEnumerable<RaycastHit> GetUpwardResults(int rayIndex, bool stopIfNullHit = true)
        {
            int startIndex = UpwardsHitsStart(rayIndex);
            return GetResults(startIndex, stopIfNullHit);
        }

        IEnumerable<RaycastHit> GetResults(int startIndex, bool stopIfNullHit)
        {
            if (startIndex >= results.Length)
                yield break;

            for (int i = 0; i < nHitsPerRay; i++)
            {
                var result = results[startIndex + i];

                if (stopIfNullHit)
                {
                    int colliderInstanceId = Utils.GetColliderID(result);
                    if (colliderInstanceId == 0)
                        yield break;
                }

                yield return result;
            }
        }

        public RaycastHit GetDownwardResult(int rayIndex, int hitIndex)
        {
            if (hitIndex >= nHitsPerRay)
                throw new ArgumentException("Trying to access a hit outside of the maximum hits per ray");

            return results[DownwardsHitsStart(rayIndex) + hitIndex];
        }

        public RaycastHit GetUpwardResult(int rayIndex, int hitIndex)
        {
            if (hitIndex >= nHitsPerRay)
                throw new ArgumentException("Trying to access a hit outside of the maximum hits per ray");

            return results[UpwardsHitsStart(rayIndex) + hitIndex];
        }

        public void SetDownwardResult(int rayIndex, int hitIndex, in RaycastHit hit)
        {
            if (hitIndex >= nHitsPerRay)
                throw new ArgumentException("Trying to access a hit outside of the maximum hits per ray");

            results[DownwardsHitsStart(rayIndex) + hitIndex] = hit;
        }

        public void SetUpwardResult(int rayIndex, int hitIndex, in RaycastHit hit)
        {
            if (hitIndex >= nHitsPerRay)
                throw new ArgumentException("Trying to access a hit outside of the maximum hits per ray");

            results[UpwardsHitsStart(rayIndex) + hitIndex] = hit;
        }

        public void Dispose()
        {
            if (results.IsCreated)
                results.Dispose();
        }
        
        public static implicit operator NativeArray<RaycastHit>(RaycastCommandPairsResults input)
        {
            return input.results;
        }
    }
}
*/