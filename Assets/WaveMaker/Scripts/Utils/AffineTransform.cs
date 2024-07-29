#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

using Unity.Mathematics;

namespace WaveMaker
{
    public struct AffineTransform
    {
        public float4 translation;
        public float4 scale;
        public quaternion rotation;

        public static AffineTransform Identity
        {
            get{ return new AffineTransform(float4.zero, quaternion.identity, new float4(1, 1, 1, 1)); }
        }

        public AffineTransform(float4 translation, quaternion rotation, float4 scale)
        {
            // make sure there are good values in the 4th component:
            translation[3] = 0;
            scale[3] = 1;

            this.translation = translation;
            this.rotation = rotation;
            this.scale = scale;
        }

        public static AffineTransform operator *(AffineTransform a, AffineTransform b)
        {
            return new AffineTransform(a.TransformPoint(b.translation),
                                            math.mul(a.rotation,b.rotation),
                                            a.scale * b.scale);
        }

        public AffineTransform Inverse()
        {
            return new AffineTransform(new float4(math.rotate(math.conjugate(rotation),(translation / -scale).xyz),0),
                                            math.conjugate(rotation),
                                            1 / scale);
        }

        public AffineTransform Interpolate(AffineTransform other, float translationalMu, float rotationalMu, float scaleMu)
        {
            return new AffineTransform(math.lerp(translation, other.translation, translationalMu),
                                            math.slerp(rotation, other.rotation, rotationalMu),
                                            math.lerp(scale, other.scale, scaleMu));
        }

        public float4 TransformPoint(float4 point)
        {
            return new float4(translation.xyz + math.rotate(rotation, (point * scale).xyz),0);
        }

        public float4 InverseTransformPoint(float4 point)
        {
            return new float4(math.rotate(math.conjugate(rotation),(point - translation).xyz) / scale.xyz , 0);
        }

        public float4 TransformPointUnscaled(float4 point)
        {
            return new float4(translation.xyz + math.rotate(rotation,point.xyz), 0);
        }

        public float4 InverseTransformPointUnscaled(float4 point)
        {
            return new float4(math.rotate(math.conjugate(rotation), (point - translation).xyz), 0);
        }

        public float4 TransformDirection(float4 direction)
        {
            return new float4(math.rotate(rotation, direction.xyz), 0);
        }

        public float4 InverseTransformDirection(float4 direction)
        {
            return new float4(math.rotate(math.conjugate(rotation), direction.xyz), 0);
        }

        public float4 TransformVector(float4 vector)
        {
            return new float4(math.rotate(rotation, (vector * scale).xyz), 0);
        }

        public float4 InverseTransformVector(float4 vector)
        {
            return new float4(math.rotate(math.conjugate(rotation),vector.xyz) / scale.xyz, 0);
        }
    }
}

#endif