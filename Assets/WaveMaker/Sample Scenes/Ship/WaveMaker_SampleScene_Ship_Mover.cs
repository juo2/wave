using UnityEngine;

namespace WaveMaker
{
    [RequireComponent(typeof(Rigidbody))]
    public class WaveMaker_SampleScene_Ship_Mover : MonoBehaviour
    {
        Rigidbody rb;

        public float frontThrust = 3f;
        public float backThrust = 2f;
        public float sideTorque = 1f;
        public float maxVelocity = 6f;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            float scale = Time.deltaTime * 10000;

            if (Input.GetKey(KeyCode.UpArrow))
                rb.AddForce(transform.localToWorldMatrix.MultiplyVector(Vector3.forward * frontThrust) * scale, ForceMode.Force);
            else if (Input.GetKey(KeyCode.DownArrow))
                rb.AddForce(transform.localToWorldMatrix.MultiplyVector(Vector3.back * backThrust) * scale, ForceMode.Force);

            if (Input.GetKey(KeyCode.RightArrow))
                rb.AddTorque(transform.up * sideTorque * scale, ForceMode.Force);
            else if (Input.GetKey(KeyCode.LeftArrow))
                rb.AddTorque(-transform.up * sideTorque * scale, ForceMode.Force);

            //Clamp speed
            if (rb.angularVelocity.magnitude > maxVelocity)
                rb.angularVelocity = rb.angularVelocity.normalized * maxVelocity;

            if (rb.velocity.magnitude > maxVelocity)
                rb.velocity = rb.velocity.normalized* maxVelocity;

        }
    }
}
