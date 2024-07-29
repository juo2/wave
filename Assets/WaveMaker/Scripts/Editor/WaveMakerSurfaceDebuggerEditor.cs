#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

using UnityEngine;
using UnityEditor;

namespace WaveMaker.Editor
{
    [CustomEditor(typeof(WaveMakerSurfaceDebugger))]
    public class WaveMakerSurfaceDebuggerEditor : UnityEditor.Editor
    {
        WaveMakerSurfaceDebugger _debugComponent;

        SerializedProperty drawMode;

        // TODO: Obsolete
        /*
        SerializedProperty rayDrawMode;
        SerializedProperty showDownwardRays;
        SerializedProperty showUpwardRays;
        SerializedProperty showOnlyHittingRays;
        */

        SerializedProperty showDetectionDepth;
        SerializedProperty printDetectedInteractors;
        SerializedProperty rayVisualScale;
        SerializedProperty offsetClamp;
        SerializedProperty interactorSelected;

        // TODO: Obsolete
        //SerializedProperty rayHitColors;

        SerializedProperty occupancyNormalizedByDepth;

        WaveMakerSurface _surface;

        private void OnEnable()
        {
            _debugComponent = (WaveMakerSurfaceDebugger)target;
            _surface = _debugComponent.GetComponent<WaveMakerSurface>();

            drawMode = serializedObject.FindProperty("drawMode");

            // TODO: Obsolete
            /*
            rayDrawMode = serializedObject.FindProperty("rayDrawMode");
            showDownwardRays = serializedObject.FindProperty("showDownwardRays");
            showUpwardRays = serializedObject.FindProperty("showUpwardRays");
            showOnlyHittingRays = serializedObject.FindProperty("showOnlyHittingRays");
            */

            showDetectionDepth = serializedObject.FindProperty("showDetectionDepth");
            printDetectedInteractors = serializedObject.FindProperty("printDetectedInteractors");
            rayVisualScale = serializedObject.FindProperty("rayVisualScale");
            offsetClamp = serializedObject.FindProperty("offsetClamp");
            interactorSelected = serializedObject.FindProperty("interactorSelected");

            // TODO: Obsolete
            //rayHitColors = serializedObject.FindProperty("rayHitColors");

            occupancyNormalizedByDepth = serializedObject.FindProperty("occupancyNormalizedByDepth");

        }

        public override void OnInspectorGUI()
        {
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Only works on play mode", MessageType.Warning);

            EditorGUILayout.Space();

            EditorGUIUtility.labelWidth = 150;

            if (Application.isPlaying)
            { 
                if (GUILayout.Button("<<< Disrupt surface >>>"))
                    _debugComponent.DisruptSurface();

                if (GUILayout.Button(new GUIContent("Print Detected Interactors Info")))
                    _debugComponent.PrintInteractors();
            }

            EditorGUILayout.PropertyField(showDetectionDepth);

            // TODO: Obsolete
            // EditorGUILayout.PropertyField(rayDrawMode);

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(drawMode);
            if (Application.isPlaying && EditorGUI.EndChangeCheck())
                _debugComponent.ResetColorGrid();

            switch (_debugComponent.drawMode)
            {
                case WaveMakerSurfaceDebugger.DrawMode.offset:
                    EditorGUILayout.PropertyField(offsetClamp);
                    break;

                case WaveMakerSurfaceDebugger.DrawMode.gradients:
                case WaveMakerSurfaceDebugger.DrawMode.normals:
                case WaveMakerSurfaceDebugger.DrawMode.velocity:
                case WaveMakerSurfaceDebugger.DrawMode.acceleration:
                    EditorGUILayout.PropertyField(rayVisualScale);
                    break;

                case WaveMakerSurfaceDebugger.DrawMode.relativeVelocity:
                    if (Application.isPlaying && _surface.interactionType != WaveMakerSurface.InteractionType.VelocityBased)
                        EditorGUILayout.HelpBox("The selected draw mode works only when Interaction Type is Velocity Based", MessageType.Error);
                    else
                        EditorGUILayout.PropertyField(rayVisualScale);
                    break;

                case WaveMakerSurfaceDebugger.DrawMode.globalOccupancy:
                    if (Application.isPlaying && _surface.interactionType != WaveMakerSurface.InteractionType.OccupancyBased)
                        EditorGUILayout.HelpBox("The selected draw mode works only when Interaction Type is Occupancy Based", MessageType.Error);
                    else
                        EditorGUILayout.PropertyField(occupancyNormalizedByDepth, new GUIContent("Normalized by Depth"));
                    break;

                case WaveMakerSurfaceDebugger.DrawMode.interactionData:
                    if (Application.isPlaying && _surface.interactionType != WaveMakerSurface.InteractionType.OccupancyBased)
                        EditorGUILayout.HelpBox("The selected draw mode works only when Interaction Type is Occupancy Based", MessageType.Error);
                    else
                        EditorGUILayout.PropertyField(interactorSelected);
                    break;

                case WaveMakerSurfaceDebugger.DrawMode.interactorOccupancy:
                    if (Application.isPlaying && _surface.interactionType != WaveMakerSurface.InteractionType.OccupancyBased)
                        EditorGUILayout.HelpBox("The selected draw mode works only when Interaction Type is Occupancy Based", MessageType.Error);
                    else
                    {
                        EditorGUILayout.PropertyField(interactorSelected);
                        EditorGUILayout.PropertyField(occupancyNormalizedByDepth, new GUIContent("Normalized by Depth"));
                    }
                    break;
                default:
                    break;
            }
            
            EditorGUIUtility.labelWidth = 0;

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif