#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

using Unity.Mathematics;

namespace WaveMaker
{
    public static class Utils
    {
        
        public readonly static float Epsilon = (float)1e-6;

        //NOTE: This hack given by the Unity support team is needed to be able to access 
        // the raycashit collider from raycast hits returned by RaycastCommand.Schedule
        // in order to take advantage of jobs afterwards. A UnityEngine class is not accessible 
        // from a job.
        /// TODO: Unity support team said that this is fixed in 2021.2 version. In the meantime, use this
        [StructLayout(LayoutKind.Sequential)]
        internal struct RaycastHitPublic
        {
            public Vector3 m_Point;
            public Vector3 m_Normal;
            public int m_FaceID;
            public float m_Distance;
            public Vector2 m_UV;
            public int m_ColliderID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetColliderID(RaycastHit hit)
        {
            unsafe
            {
                RaycastHitPublic h = *(RaycastHitPublic*)&hit;
                return h.m_ColliderID;
            }
        }

        public static void Log(string text, GameObject go)
        {
            Debug.Log(string.Format("WaveMaker gameObject '{0}' - {1}", go.name, text));
        }

        public static void LogWarning(string text, GameObject go)
        {
            Debug.LogWarning(string.Format("WaveMaker gameObject '{0}' - {1}", go.name, text));
        }

        public static void LogError(string text, GameObject go)
        {
            Debug.LogError(string.Format("WaveMaker gameObject '{0}' - {1}", go.name, text));
        }

        /// <summary>
        /// Data arrays can be normal or contain one or several ghost columns and row around it.
        /// Use this to convert from the array index of a normal array to the corresponding index in the ghost array
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromNoGhostIndexToGhostIndex(int indexNoGhost, in IntegerPair resolution, in IntegerPair ghostResolution)
        {
            int ghostResolutionXDiff = (ghostResolution.x - resolution.x) / 2;
            int ghostResolutionZDiff = (ghostResolution.z - resolution.z) / 2;
            FromIndexToSampleIndices(indexNoGhost, resolution, out int sampleX, out int sampleZ);
            int sampleXGhost = sampleX + ghostResolutionXDiff;
            int sampleZGhost = sampleZ + ghostResolutionZDiff;
            return FromSampleIndicesToIndex(in ghostResolution, sampleXGhost, sampleZGhost);
        }

        /// <summary>
        /// Data arrays can be normal or contain one or several ghost columns and row around it.
        /// Use this to convert from the array index of an expanded ghost array to the corresponding index in the normal array
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromGhostIndexToNoGhostIndex(int indexGhost, in IntegerPair resolution, in IntegerPair ghostResolution)
        {
            int ghostResolutionXDiff = (ghostResolution.x - resolution.x) / 2;
            int ghostResolutionZDiff = (ghostResolution.z - resolution.z) / 2;
            FromIndexToSampleIndices(indexGhost, ghostResolution, out int sampleX, out int sampleZ);
            int sampleXNoGhost = sampleX - ghostResolutionXDiff;
            int sampleZNoGhost = sampleZ - ghostResolutionZDiff;
            return FromSampleIndicesToIndex(in resolution, sampleXNoGhost, sampleZNoGhost);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromIndexToSampleIndices(in int index, in IntegerPair resolution, out int sampleX, out int sampleZ)
        {
            if (resolution.x <= 0 || resolution.z <= 0)
                throw new ArgumentException("Resolution is negative or 0");

            if (index < 0)
                throw new ArgumentException("Index is less than 0");

            sampleX = index % resolution.x;
            sampleZ = index / resolution.x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FromIndexToSampleIndices_Untested(in int index, in IntegerPair resolution, out int sampleX, out int sampleZ)
        {
            sampleX = index % resolution.x;
            sampleZ = index / resolution.x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromSampleIndicesToIndex(in IntegerPair resolution, in int sampleX, in int sampleZ)
        {
            if (resolution.x <= 0 || resolution.z <= 0)
                throw new ArgumentException("Resolution is negative or 0");

            if (sampleX < 0 || sampleZ < 0)
                throw new ArgumentException("Sample indices are less than 0");
            
            return resolution.x * sampleZ + sampleX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromSampleIndicesToIndex_Untested(in IntegerPair resolution, in int sampleX, in int sampleZ)
        {
            return resolution.x * sampleZ + sampleX;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OffsetSampleIndex(int index, in IntegerPair resolution, in int offsetX, in int offsetZ)
        {
            return index + resolution.x * offsetZ + offsetX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsGhostSampleInGhostArea(in IntegerPair resolution, in IntegerPair ghostResolution, in int sampleX, in int sampleZ)
        {
            int ghostResolutionXDiff = (ghostResolution.x - resolution.x) / 2;
            int ghostResolutionZDiff = (ghostResolution.z - resolution.z) / 2;
            return (sampleX < ghostResolutionXDiff || sampleX >= ghostResolution.x - ghostResolutionXDiff ||
                    sampleZ < ghostResolutionZDiff || sampleZ >= ghostResolution.z - ghostResolutionZDiff);
        }

        /// <summary>
        /// Returns the index of the sample selected from the given sample plus the offset
        /// </summary>
        /// <param name="index">the origin sample</param>
        /// <param name="offsetX">number of samples in X to offset (-X or +X)</param>
        /// <param name="offsetZ">number of samples in Z to offset (-Z or +Z)</param>
        /// <returns>the index of the given sample or -1 if out of the area</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetOffsetSample_Untested(in IntegerPair resolution, in int index, in int offsetX, in int offsetZ)
        {
            FromIndexToSampleIndices_Untested(in index, in resolution, out int sampleX, out int sampleZ);
            sampleX += offsetX;
            sampleZ += offsetZ;
            if (sampleX < 0 || sampleZ < 0 || sampleX > resolution.x-1 || sampleZ > resolution.z-1)
                return -1;

            return FromSampleIndicesToIndex_Untested(in resolution, in sampleX, in sampleZ);
        }

        /// <returns><c>true</c> if sample is on the border of this resolution</returns>
        /// <param name="x">The x sample index on the resolution.</param>
        /// <param name="z">The z sample index on the resolution</param>
        /// <param name="xSide">-1 if on the left x extreme, 1 if on the right. 0 rest of cases</param>
        /// <param name="zSide">-1 if on the bottom z extreme, 1 if on the top. 0 rest of cases</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSampleExtreme(in int index, in IntegerPair resolution, ref int xSide, ref int zSide)
        {
            FromIndexToSampleIndices_Untested(in index, in resolution, out int x, out int z);
            xSide = x==0? -1 : x / (resolution.x - 1);
            zSide = z==0? -1 : z / (resolution.z - 1);
            return xSide != 0 || zSide != 0;
        }

        /// <summary>
        /// Calculates the min and max sample indices for the area the given collider occupies over the surface.
        /// </summary>
        public static void GetColliderProjectedAreaOnSurface(in WaveMakerSurface surface, in NativeCollider collider,
                                                            out int sampleMin_ls, out int sampleMax_ls)
        {
            float4 min = collider.boundsMin;
            float4 max = collider.boundsMax;

            TransformBounds(ref min, ref max, in surface._w2lTransformMatrix);
            
            sampleMin_ls = GetNearestSampleFromLocalPosition(min.xyz, surface._size_ls, surface._sampleSize_ls);
            sampleMax_ls = GetNearestSampleFromLocalPosition(max.xyz, surface._size_ls, surface._sampleSize_ls);
        }

        /// <summary> Transform center and extents of the given bounds transformed by the transformation matrix passed. </summary>
        public static void TransformBounds(ref float4 min, ref float4 max, in Matrix4x4 matrix)
        {
            float4 col1 = new float4(matrix[0, 0], matrix[1, 0], matrix[2, 0], matrix[3, 0]);
            float4 col2 = new float4(matrix[0, 1], matrix[1, 1], matrix[2, 1], matrix[3, 1]);
            float4 col3 = new float4(matrix[0, 2], matrix[1, 2], matrix[2, 2], matrix[3, 2]);
            float4 col4Pos = new float4(matrix[0, 3], matrix[1, 3], matrix[2, 3], 0);

            var xa = col1 * min.x;
            var xb = col1 * max.x;

            var ya = col2 * min.y;
            var yb = col2 * max.y;

            var za = col3 * min.z;
            var zb = col3 * max.z;

            min = math.min(xa, xb) + math.min(ya, yb) + math.min(za, zb) + col4Pos;
            max = math.max(xa, xb) + math.max(ya, yb) + math.max(za, zb) + col4Pos;
        }

        /// <summary>
        /// Get position in surface local space of the given sample. Y coordinate will be 0.
        /// </summary>
        /// <exception cref="ArgumentException">If index out of range or incorrect values passed</exception>
        public static Vector3 GetLocalPositionFromSample(int sampleX, int sampleZ, in IntegerPair resolution, in float2 sampleSize)
        {
            if (sampleX >= resolution.x || sampleZ >= resolution.z || sampleX < 0 || sampleZ < 0)
            {
                sampleX = math.clamp(sampleX, 0, resolution.x - 1);
                sampleZ = math.clamp(sampleZ, 0, resolution.z - 1);

                //TODO: Error in burst. Not allowed.
                //throw new ArgumentException("Sample index is out of range : " + sampleX + " - " + sampleZ);
            }

            if (sampleSize.x <= 0 || sampleSize.y <= 0)
                throw new ArgumentException("Sample size values are not positive");

            if (resolution.x <= 0 || resolution.z <= 0)
                throw new ArgumentException("Resolution values are not positive");

            return new Vector3(sampleX * sampleSize.x, 0, sampleZ * sampleSize.y);
        }

        /// <summary>
        /// Get position in surface local space of the given sample. Y coordinate will be 0.
        /// </summary>
        /// <exception cref="ArgumentException">If index out of range or incorrect values passed</exception>
        public static Vector3 GetLocalPositionFromSample(int sampleIndex, in IntegerPair resolution, in float2 sampleSize)
        {
            FromIndexToSampleIndices(sampleIndex, in resolution, out int sampleX, out int sampleZ);
            return GetLocalPositionFromSample(sampleX, sampleZ, in resolution, in sampleSize);
        }

        /// <summary>It returns the coordinates of the nearest sample for the given position. 
        /// If the position is away from the bounding box, it will return the nearest one anyway.</summary>
        public static int GetNearestSampleFromLocalPosition(float3 pos_ls, in float2 surfaceSize, in float2 sampleSize)
        {
            if (sampleSize.x <= 0 || sampleSize.y <= 0)
                throw new ArgumentException("Sample size values are not positive");

            if (surfaceSize.x <= 0 || surfaceSize.y <= 0)
                throw new ArgumentException("Surface size values are not positive");

            int sampleX = (int) math.round(pos_ls.x / sampleSize.x);
            int sampleZ = (int) math.round(pos_ls.z / sampleSize.y);

            int resX = (int) math.ceil(surfaceSize.x / sampleSize.x) + 1;
            int resZ = (int) math.ceil(surfaceSize.y / sampleSize.y) + 1;

            sampleX = math.clamp(sampleX, 0, resX - 1);
            sampleZ = math.clamp(sampleZ, 0, resZ - 1);

            return sampleZ * resX + sampleX;
        }

        /*
        public static bool IsPointInsideCollider(Collider collider, Vector3 point)
        {
            Vector3 pointInCollider= collider.ClosestPoint(point);
            pointInCollider.x -= point.x;
            pointInCollider.y -= point.y;
            pointInCollider.z -= point.z;
            return pointInCollider.x * pointInCollider.x + pointInCollider.y * pointInCollider.y + pointInCollider.z * pointInCollider.z < 0.0001f;
        }
        */

        /// <summary>
        /// Given a point in a given space and a center of rotation in the given space, returns the velocity at that point
        /// </summary>
        public static float4 VelocityAtPoint(float4 point, float4 centerOfRotation, float4 angularVelocity, float4 linearVelocity)
        {
            return new float4(math.cross(angularVelocity.xyz, (point - centerOfRotation).xyz), 0) + linearVelocity;
        }

        /// <summary>
        /// Given an old and new rotation quaternions, return the angular velocity of the given object
        /// </summary>
        /// <param name="oldQuat">rotation before (normalized)</param>
        /// <param name="newQuat">rotation after a fixedDeltaTime (normalized)</param>
        public static float4 GetAngularVelocity(Quaternion oldQuat, Quaternion newQuat)
        {
            float scaledTime = 2 / Time.fixedDeltaTime;
            oldQuat.x = -oldQuat.x;
            oldQuat.y = -oldQuat.y;
            oldQuat.z = -oldQuat.z;
            oldQuat = newQuat * oldQuat;
            return new float4(oldQuat.x, oldQuat.y, oldQuat.z, 0) * scaledTime;
        }

        ///<summary></summary>Gradient has only X and Z coords.
        /// This returns a nomralized vector, the direction at which the gradient points at.</summary>
        public static void FromGradientToDirectionVector(ref Vector3 inoutVector)
        {
            // NOTE: Magnitude is already calculated in the Y component for efficienty reasons
            float magnitude = inoutVector.y;

            // Avoid division by zero
            if (magnitude < 0.00001f && magnitude > -0.00001f)
            {
                inoutVector.x = 0; inoutVector.y = 0; inoutVector.z = 0;
                return;
            }

            inoutVector.x = inoutVector.x / magnitude;
            inoutVector.y = -magnitude;
            inoutVector.z = inoutVector.z / magnitude;
        }

        public static Vector3 ComputeBuoyantForce(Rigidbody interactorRigidBody, float inmersedVolume, float fluidDensity,
                                            in Vector3 upDirection_ws, in Vector3 forceDirection_ws, in Vector3 hitPos_ws, float damping = 0)
        {
            float buoyantMagnitude = fluidDensity * 9.81f * inmersedVolume;
            Vector3 buoyantForce = new Vector3(buoyantMagnitude * forceDirection_ws.x, buoyantMagnitude * forceDirection_ws.y, buoyantMagnitude * forceDirection_ws.z);

            // Damping force to avoid eternal bouncing. Only applied on the Y axis.
            if (damping > 0)
            {
                Vector3 vel = interactorRigidBody.GetPointVelocity(hitPos_ws);

                // faster Vector3.Dot(vel, upDirection_ws);
                float projectedVelocity = vel.x * upDirection_ws.x + vel.y * upDirection_ws.y + vel.z * upDirection_ws.z;
                float dampingMagnitude = projectedVelocity * damping;

                // NOTE: There is a limit on velocity for the given damping and volume that will make buoyancy get to 0
                // After a certain velocity (half of maximum vel) the amount of damping is less until it is 0.
                float velMax = buoyantMagnitude / damping;
                // f(x) = 2 - (2 * x / velMax) gives us a 1 when in the velMax/2, and 0 when in velMax.
                float mult = 2 - (2 * projectedVelocity / velMax);

                // faster clamp 0 1
                dampingMagnitude *= mult < 0? 0 : (mult > 1? 1 : mult);

                buoyantForce.x -= upDirection_ws.x * dampingMagnitude;
                buoyantForce.y -= upDirection_ws.y * dampingMagnitude;
                buoyantForce.z -= upDirection_ws.z * dampingMagnitude;
            }

            return buoyantForce;
        }

        //TODO: Obsolete
        /*
        public static Vector3 GetRaycastOffset(in WaveMakerSurface.SupersamplingType sstype, in float2 sampleSize, in int raycastIndex)
        {
            if (raycastIndex < 0 || raycastIndex > (int)sstype - 1)
            {
                throw new ArgumentException(
                    string.Format("WaveMaker Antialiasing Method: {0} has only {1} raycasts per sample. Wrong index: {2}",
                                    sampleSize, (int)sstype, raycastIndex));
            }

            Vector3 pos = Vector3.zero;

            if (sstype == WaveMakerSurface.SupersamplingType.NONE)
                return pos;
            
            int size = (int)Mathf.Sqrt((int)sstype);

            float subSampleSizeX = sampleSize.x / size;
            float subSampleSizeZ = sampleSize.y / size;

            int x = raycastIndex % size;
            int z = raycastIndex / size;

            // We calculate the offset from the bottom left part of the sample
            pos.x = x * subSampleSizeX + (subSampleSizeX/2.0f);
            pos.z = z * subSampleSizeZ + (subSampleSizeZ/2.0f);

            // Then we offset the position to the center to make it an offset
            pos.x -= sampleSize.x / 2.0f;
            pos.z -= sampleSize.y / 2.0f;
            return pos;
        }
        */

        /// <param name="a">start point of segment</param>
        /// <param name="b">end point of segment</param>
        /// <param name="point">point to compare</param>
        /// <param name="h">normalized coordinate from 0 in a to 1 in b. If clamp not enabled, can surpass those values</param>
        /// <param name="clampToSegment">mu will be less than 0 or more than 1 if the point is further away from a or b</param>
        /// <returns>the point inside the edge if clamped, or following the line defined by a and b</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 NearestPointOnEdge(float4 a, float4 b, float4 point, out float h, bool clampToSegment = true)
        {
            float4 ap = point - a;
            float4 ab = b - a;

            h = math.dot(ap, ab) / math.dot(ab, ab);

            if (clampToSegment)
                h = math.saturate(h);

            return a + ab * h;
        }

        public static void IncreaseAreaBy(in IntegerPair resolution, ref IntegerPair areaResolution, ref int xOffset, ref int zOffset, int by)
        {
            // Left
            areaResolution.x += math.min(xOffset, by);
            xOffset = math.max(0, xOffset - by);

            // Bottom
            areaResolution.z += math.min(zOffset, by);
            zOffset = math.max(0, zOffset - by);

            // Top - Right
            areaResolution.x = math.min(areaResolution.x + xOffset + by, resolution.x) - xOffset;
            areaResolution.z = math.min(areaResolution.z + zOffset + by, resolution.z) - zOffset;
        }

        public static int GetCollisionsableLayersMaskFromLayer(int layer)
        {
            int mask = 0;
            for (int i = 0; i < 32; i++)
            {
                if (!Physics.GetIgnoreLayerCollision(layer, i))
                    mask |= 1 << i;
            }
            return mask;
        }


        public static void DrawDetectionDepth(WaveMakerSurface _surface)
        {
            Vector3 size = new Vector3(_surface.Size_ls.x, _surface.detectionDepth, _surface.Size_ls.y);
            var mat = _surface._l2wTransformMatrix;

            Vector3 point0_ws = mat.MultiplyPoint3x4(new Vector3(0, -size.y, 0));
            Vector3 point1_ws = mat.MultiplyPoint3x4(new Vector3(0, -size.y, size.z));
            Vector3 point2_ws = mat.MultiplyPoint3x4(new Vector3(size.x, -size.y, size.z));
            Vector3 point3_ws = mat.MultiplyPoint3x4(new Vector3(size.x, -size.y, 0));
            Vector3 vectorUp_ws = mat.MultiplyVector(new Vector3(0, size.y, 0));

            Debug.DrawRay(point0_ws, vectorUp_ws, Color.white);
            Debug.DrawRay(point1_ws, vectorUp_ws, Color.white);
            Debug.DrawRay(point2_ws, vectorUp_ws, Color.white);
            Debug.DrawRay(point3_ws, vectorUp_ws, Color.white);

            Debug.DrawLine(point0_ws, point1_ws, Color.white);
            Debug.DrawLine(point1_ws, point2_ws, Color.white, 1);
            Debug.DrawLine(point2_ws, point3_ws, Color.white, 1);
            Debug.DrawLine(point3_ws, point0_ws, Color.white, 1);
        }

        /// <summary>Slow native method using Unity colliders</summary>
        public static float GetMinDistanceToCollider(in Vector3 pos, in Collider collider)
        {
            // Warning: if the point is inside the collider???
            Vector3 nearestPoint = collider.ClosestPoint(pos);
            return (nearestPoint - pos).magnitude;
        }
    }
}
#endif