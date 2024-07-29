#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

using UnityEngine;
using Unity.Mathematics;

namespace WaveMaker
{
    [System.Serializable]
    public struct NativeCollider
    {
        public enum ColliderType
        {
            SPHERE,
            CAPSULE,
            BOX
        }

        public ColliderType type;

        /// <summary>Unique identification in the scene</summary>
        public int instanceId;

        /// <summary>Position offset from 0,0,0 of the gameobject in local space</summary>
        public float4 center;

        /// <summary>x = radius sphere or  x,y,z = size box  or  x = radius, y = height (including radiuses), z = direction capsule</summary>
        public float4 size;

        /// <summary>position of maximum point of the bbox in x,y,z expressed in world space</summary>
        public float4 boundsMin;

        /// <summary>position of minimum point of the bbox in x,y,z expressed in world space</summary>
        public float4 boundsMax;

        public float contactOffset;

        public AffineTransform transform_l2w;

        // TODO: Necessary?
        public AffineTransform transform_w2l;

        public static NativeCollider CreateSphereCollider(float radius, float4 centerPos)
        {
            float4 boundsMin = new float4(-radius*0.5f, -radius*0.5f, -radius*0.5f, 0) + centerPos;
            float4 boundsMax = -boundsMin + centerPos;
            var col = Create(boundsMin, boundsMax);
            col.type = ColliderType.SPHERE;
            col.size = new float4(radius, 0, 0, 0);
            return col;
        }

        //TODO: Use center and size and calculate inside
        public static NativeCollider CreateBoxCollider(float3 size, in float4 boundsMin, in float4 boundsMax)
        {
            var col = Create(boundsMin, boundsMax);
            col.type = ColliderType.BOX;
            col.size = new float4(size, 0);
            return col;
        }

        // TODO: Use center and size and calculate inside
        public static NativeCollider CreateCapsuleCollider(float radius, float height, int direction, in float4 boundsMin, in float4 boundsMax)
        {
            var col = Create(boundsMin, boundsMax);
            col.type = ColliderType.CAPSULE;
            col.size = new float4(radius, height, direction, 0);
            return col;
        }

        private static NativeCollider Create(in float4 boundsMin, in float4 boundsMax)
        {
            var col = new NativeCollider();
            col.boundsMin = boundsMin;
            col.boundsMax = boundsMax;
            col.transform_l2w = AffineTransform.Identity;
            col.contactOffset = 0;
            return col;
        }

        public NativeCollider(Collider inCol)
        {
            instanceId = inCol.GetInstanceID();

            Bounds b = inCol.bounds;
            boundsMin = new float4(b.min, 0);
            boundsMax = new float4(b.max, 0);
            contactOffset = inCol.contactOffset;

            var trans = inCol.transform;
            var translation = new float4(trans.position.x, trans.position.y, trans.position.z, 0);
            var scale = new float4(trans.localScale.x, trans.localScale.y, trans.localScale.z, 1);

            transform_l2w = new AffineTransform(translation, trans.rotation, scale);
            transform_w2l = new AffineTransform(-translation, math.inverse(trans.rotation), 1 / scale);

            if (inCol is SphereCollider)
            {
                var col = inCol as SphereCollider;
                type = ColliderType.SPHERE;
                center = new float4(col.center, 0);
                size = new float4(col.radius, 0, 0, 0);
            }
            else if (inCol is BoxCollider)
            {
                var col = inCol as BoxCollider;
                type = ColliderType.BOX;
                center = new float4(col.center, 0);
                size = new float4(col.size, 0);
            }
            else if (inCol is CapsuleCollider)
            {
                var col = inCol as CapsuleCollider;
                type = ColliderType.CAPSULE;
                center = new float4(col.center, 0);
                size = new float4(col.radius, col.height, col.direction, 0);
            }
            else
            {
                Utils.LogError("An unsupported type of collider used to create a NativeCollider", inCol.gameObject);
                type = ColliderType.BOX;
                size = float4.zero;
                center = float4.zero;
            }
        }

        /*
        /// <summary>Just the distance. A faster method</summary>
        /// <param name="point">point in the interactor local space from which we calculate the nearest distance to this collider</param>
        public float ShortestDistanceFrom(in float4 point)
        {
            var point_ls = point - center;

            //TODO: Not using transforms or contact offset

            switch (type)
            {
                case ColliderType.SPHERE:
                    return math.length(point_ls) - size.x;

                case ColliderType.CAPSULE:
                    float halfHeightMinusRadius = size.y - size.x;
                    float4 ab = float4.zero;
                    ab[(int)size.z] = 1;
                    float4 a = -ab * halfHeightMinusRadius;
                    float4 b = ab * halfHeightMinusRadius;
                    float4 proyectedPoint = Utils.NearestPointOnEdge(a, b, point_ls, out float _);
                    return math.length(proyectedPoint - point_ls) - size.x; // compare with radius

                case ColliderType.BOX:
                    float4 pointAbs_ls = math.abs(point_ls) - size * 0.5f;
                    return math.length(
                        math.max(pointAbs_ls, 0.0f)) +
                        math.min(math.max(pointAbs_ls.x, math.max(pointAbs_ls.y, pointAbs_ls.z)), 0.0f);
                default:
                    return 0;
            }
        }*/

        /// <returns>
        /// The distance to the collider plus the native contact offset and passed thickness too. 
        /// If inside the collider, returns the hit point and a negative distance</returns>
        public float NearestPointFrom(in float4 point_ws, in float thickness, out float4 hitPoint_ws)
        {
            switch (type)
            {
                case ColliderType.SPHERE:
                        return NearestPointToSphere(point_ws, thickness, out hitPoint_ws);

                case ColliderType.CAPSULE:
                        return NearestPointToCapsule(point_ws, thickness, out hitPoint_ws);

                case ColliderType.BOX:
                        return NearestPointToCube(point_ws, thickness, out hitPoint_ws);

                default:
                    hitPoint_ws = new float4();
                    return 0;
            }
        }

        private float NearestPointToSphere(in float4 point_ws, in float thickness, out float4 hitPoint_ws)
        {
            float4 center_scaled = center * transform_l2w.scale;
            float4 point_ls = transform_l2w.InverseTransformPointUnscaled(point_ws) - center_scaled;

            float radius = size.x * math.cmax(transform_l2w.scale.xyz);
            float fullRadius = radius + contactOffset + thickness;
            float distanceToCenter = math.length(point_ls);
            float4 normal = point_ls / (distanceToCenter + Utils.Epsilon);
            
            hitPoint_ws = transform_l2w.TransformPointUnscaled(center + normal * fullRadius);
            
            return distanceToCenter - fullRadius;
        }

        private float NearestPointToCapsule(in float4 point_ws, in float thickness, out float4 hitPoint_ws)
        {
            float4 center_scaled = center * transform_l2w.scale;
            float4 point_ls = transform_l2w.InverseTransformPointUnscaled(point_ws) - center_scaled;
            
            int direction = (int)size.z;
            float radius = size.x * math.max(transform_l2w.scale[(direction + 1) % 3],
                                             transform_l2w.scale[(direction + 2) % 3]);
            float height = math.max(radius, size.y * 0.5f * transform_l2w.scale[direction]);

            float4 halfVector = float4.zero;
            halfVector[direction] = height - radius;

            float4 centerLine = Utils.NearestPointOnEdge(-halfVector, halfVector, point_ls, out float _);
            float4 centerToPoint = point_ls - centerLine;
            float distanceToCenter = math.length(centerToPoint);

            float4 normal = centerToPoint / (distanceToCenter + Utils.Epsilon);
            float fullRadius = radius + contactOffset + thickness;

            hitPoint_ws = transform_l2w.TransformPointUnscaled(center + centerLine + normal * fullRadius);
            
            return distanceToCenter - fullRadius;
        }

        private float NearestPointToCube(in float4 point_ws, in float thickness, out float4 hitPoint_ws)
        { 
            float4 center_scaled = center * transform_l2w.scale;
            float4 half_size_scaled = size * transform_l2w.scale * 0.5f + new float4(thickness, thickness, thickness, 0);
            float4 point_ls = transform_l2w.InverseTransformPointUnscaled(point_ws) - center_scaled;
            float4 normal = float4.zero;

            // Bring all to positive quadrant
            float4 distances = half_size_scaled - math.abs(point_ls);
            float distance;

            // Inside the cube
            if (distances.x >= 0 && distances.y >= 0 && distances.z >= 0)
            {
                // find minimum distance in all three axes and the axis index
                float min = float.MaxValue;
                int axis = 0;

                for (int i = 0; i < 3; ++i)
                {
                    if (distances[i] < min)
                    {
                        min = distances[i];
                        axis = i;
                    }
                }

                normal[axis] = point_ls[axis] > 0 ? 1 : -1; // Normal points outwards when inside
                hitPoint_ws = point_ls;
                hitPoint_ws[axis] = half_size_scaled[axis] * normal[axis];
                distance = -1;
            }
            else
            {
                hitPoint_ws = math.clamp(point_ls, -half_size_scaled, half_size_scaled);
                distance = 1;
            }

            hitPoint_ws = transform_l2w.TransformPointUnscaled(hitPoint_ws + center_scaled);
            return distance * math.length((point_ws - hitPoint_ws).xyz);
        }
    }
}

#endif