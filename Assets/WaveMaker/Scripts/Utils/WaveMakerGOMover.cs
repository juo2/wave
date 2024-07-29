using UnityEngine;

namespace WaveMaker
{
    /// <summary>
    /// If this component is used to move an interactor, set the rigidBody to isKinematic
    /// </summary>
    public class WaveMakerGOMover : MonoBehaviour
    {
        public enum MovementType
        {
            ComeAndGoTranslation,
            Rotation
        }

        public MovementType movementType = MovementType.ComeAndGoTranslation;

        [Header("Come and Go Translation movement")]
        public Vector3 translationSpeed = Vector3.zero;
        public Vector3 translationDistance = Vector3.zero;

        [Header("Rotation movement")]
        public Vector3 rotationSpeed = Vector3.zero;

        bool isRb = false;
        Rigidbody rb;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            isRb = (rb != null && !rb.isKinematic);
        }

        void FixedUpdate()
        {
            switch (movementType)
            {
                case MovementType.ComeAndGoTranslation:
                    {
                        Vector3 change = new Vector3(
                                        translationDistance.x * Mathf.Sin(Time.time * translationSpeed.x),
                                        translationDistance.y * Mathf.Sin(Time.time * translationSpeed.y),
                                        translationDistance.z * Mathf.Sin(Time.time * translationSpeed.z));
                        change = transform.TransformDirection(change);

                        //TODO: Use force when rigidbody is present. 
                        transform.localPosition = change;
                    }
                    break;
                case MovementType.Rotation:
                    {
                        Vector3 rotationSpeed_ws = transform.TransformDirection(rotationSpeed * Time.fixedDeltaTime);

                        if (isRb)
                            rb.AddTorque(rotationSpeed_ws.x, rotationSpeed_ws.y, rotationSpeed_ws.z, ForceMode.Force);
                        else
                            transform.Rotate(rotationSpeed_ws.x, rotationSpeed_ws.y, rotationSpeed_ws.z, Space.World);
                    }
                    break;
            }
        }
    }
}
