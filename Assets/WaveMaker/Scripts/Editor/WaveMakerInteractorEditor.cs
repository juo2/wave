using UnityEngine;
using UnityEditor;

namespace WaveMaker.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(WaveMakerInteractor))]
    public class WaveMakerInteractorEditor : UnityEditor.Editor
    {
        SerializedProperty associatedCollider;
        SerializedProperty speedDampening;
        SerializedProperty speedDampValue;
        SerializedProperty showSpeed;

        void OnEnable()
        {
            associatedCollider = serializedObject.FindProperty("associatedCollider");
            speedDampening = serializedObject.FindProperty("speedDampening");
            speedDampValue = serializedObject.FindProperty("speedDampValue");
            showSpeed = serializedObject.FindProperty("showSpeed");
        }
        
        public override void OnInspectorGUI()
        {
#if !UNITY_2018 && (!MATHEMATICS_INSTALLED || !BURST_INSTALLED || !COLLECTIONS_INSTALLED)
            EditorGUILayout.HelpBox("PACKAGES MISSING. Please follow the QuickStart in the main WaveMaker folder or visit the official website linked in the help icon on this component.", MessageType.Warning);
            return;
#else
            EditorGUILayout.HelpBox("The associated collider will interact with surfaces. Respects the collision matrix and layers.", MessageType.None);
                       
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(associatedCollider);
            if (EditorGUI.EndChangeCheck())
            {
                if (associatedCollider.objectReferenceValue != null)
                {
                    var interactor = (WaveMakerInteractor)target;

                    if ((associatedCollider.objectReferenceValue as Component).gameObject != interactor.gameObject)
                    {
                        Utils.LogError("The associated collider must be in the same GameObject as the Interactor", interactor.gameObject);
                        associatedCollider.objectReferenceValue = null;
                    }

                    else if (associatedCollider.objectReferenceValue as MeshCollider != null)
                    {
                        Utils.LogError("Mesh colliders are currently not supported in WaveMaker Interactors.", interactor.gameObject);
                        associatedCollider.objectReferenceValue = null;
                    }
                }
            }

            if (!associatedCollider.hasMultipleDifferentValues)
            {
                var interactor = (WaveMakerInteractor)target;

                if (Application.isPlaying && interactor.AssociatedCollider == null)
                    EditorGUILayout.HelpBox("A Collider component is required.", MessageType.Error);
            }

            EditorGUILayout.Space();

            foreach (var t in targets)
            {
                //TODO: AttachedRigidBody out of play mode is always null in 2019+, but works in 2018... How to detect without runnning a GetComponent?
                var interactor = (WaveMakerInteractor)t;
                if (interactor.AssociatedCollider != null && interactor.AssociatedCollider.attachedRigidbody == null)
                {
                    EditorGUILayout.HelpBox("Remember to add a Rigidbody if using Buoyancy", MessageType.Info);
                    break;
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(showSpeed);
            EditorGUILayout.PropertyField(speedDampening);
            
            if (!speedDampening.hasMultipleDifferentValues)
            {
                var interactor = (WaveMakerInteractor)target;
                if (interactor.speedDampening)
                    EditorGUILayout.PropertyField(speedDampValue);
            }

            serializedObject.ApplyModifiedProperties();
#endif
        }
    }
}
