using UnityEngine;
using UnityEditor;

namespace WaveMaker.Editor
{
    [CustomEditor(typeof(WaveMakerDescriptor))]
    public class WaveMakerDescriptorEditor : UnityEditor.Editor
    {
        int newWidth, newDepth;
        WaveMakerDescriptor descriptor;
        bool isDirty;

        private void Awake()
        {
            descriptor = (WaveMakerDescriptor)target;
        }

#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)
        private void OnEnable()
        {
            Undo.undoRedoPerformed += UndoRedoPerformed;
            isDirty = false;
            descriptor = (WaveMakerDescriptor)target;
            newWidth = descriptor.ResolutionX;
            newDepth = descriptor.ResolutionZ;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoRedoPerformed;
        }
#endif

        public override void OnInspectorGUI()
        {
#if !UNITY_2018 && (!MATHEMATICS_INSTALLED || !BURST_INSTALLED || !COLLECTIONS_INSTALLED)
            EditorGUILayout.HelpBox("PACKAGES MISSING: PACKAGES MISSING. Please follow the QuickStart in the main WaveMaker folder or visit the official website linked in the help icon on this component.", MessageType.Warning);
            return;
#else
            serializedObject.Update();
            EditorGUILayout.HelpBox("Attach this object to a WaveMaker Surface to set the properties stored here.", MessageType.Info, true);

            DrawResolutionGUI();
            EditorGUILayout.Space();
            DrawFixingGUI();
            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed)
                EditorUtility.SetDirty(target);
#endif
        }

#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

        private void DrawResolutionGUI()
        {
            GUI.SetNextControlName("LabelGrid");
            EditorGUILayout.LabelField("Grid Resolution", EditorStyles.boldLabel);
            EditorGUIUtility.labelWidth = 180;

            EditorGUI.BeginChangeCheck();
            newWidth = EditorGUILayout.IntField("Width", newWidth);
            if (EditorGUI.EndChangeCheck())
                isDirty = true;

            EditorGUI.BeginChangeCheck();
            newDepth = EditorGUILayout.IntField("Depth", newDepth);
            if (EditorGUI.EndChangeCheck())
                isDirty = true;

            EditorGUILayout.BeginHorizontal();
            if (isDirty && GUILayout.Button("Apply"))
            {
                GUIUtility.keyboardControl = 0;
                Undo.RegisterCompleteObjectUndo(descriptor, "Wave Maker Descriptor resolution change");
                descriptor.SetResolution(newWidth, newDepth);
                newWidth = descriptor.ResolutionX;
                newDepth = descriptor.ResolutionZ;
                isDirty = false;
            }

            if (isDirty && GUILayout.Button("Cancel"))
            {
                newWidth = descriptor.ResolutionX;
                newDepth = descriptor.ResolutionZ;
                GUI.FocusControl("LabelGrid");
                isDirty = false;
            }   

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            GUILayout.Label(string.Format("Number of vertices: {0} ({1} max)", newWidth * newDepth, WaveMakerDescriptor.MaxVertices));

            EditorGUIUtility.fieldWidth = 0;
        }

        private void DrawFixingGUI()
        {
            EditorGUILayout.LabelField("Cell Properties", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fix All"))
            {
                Undo.RegisterCompleteObjectUndo(descriptor, "Wave Maker Descriptor fixed cells change");
                descriptor.SetAllFixStatus(true);
            }

            if (GUILayout.Button("Unfix All"))
            {
                Undo.RegisterCompleteObjectUndo(descriptor, "Wave Maker Descriptor fixed cells change");
                descriptor.SetAllFixStatus(false);
            }

            if (GUILayout.Button("Fix Borders"))
            {
                Undo.RegisterCompleteObjectUndo(descriptor, "Wave Maker Descriptor fixed cells change");
                descriptor.FixBorders();
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.LabelField("See the stored cell properties in the preview window underneath.", EditorStyles.wordWrappedLabel);
        }

        private void UndoRedoPerformed()
        {
            //TODO: Hack to avoid this weird error. Target is working, but returns an exception, but still works.
            try
            {
                newWidth = ((WaveMakerDescriptor)target).ResolutionX;
                newDepth = ((WaveMakerDescriptor)target).ResolutionZ;

                //TODO: Updating resolution on related planes not working. Target is somehow problematic.
                ((WaveMakerDescriptor)target).SetResolution(newWidth, newDepth);
            }
            catch (System.IndexOutOfRangeException)
            {
                return;
            }
        }
#endif
    }

#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

    [CustomPreview(typeof(WaveMakerDescriptor))]
    public class WaveMakerDescriptorPreview : ObjectPreview
    {
        Texture2D _previewTexture;
        WaveMakerDescriptor descriptor;

        public override bool HasPreviewGUI()
        {
            return true;
        }

        private void CreateTexture()
        {
             descriptor = (WaveMakerDescriptor)target;

            _previewTexture = new Texture2D(descriptor.ResolutionX, descriptor.ResolutionZ, TextureFormat.RGBAHalf, false);
            _previewTexture.wrapMode = TextureWrapMode.Clamp;
        }

        private void UpdateTexture()
        {
            IntegerPair resolution = new IntegerPair(descriptor.ResolutionX, descriptor.ResolutionZ);

            // Apply properties as colors
            for (int x = 0; x < descriptor.ResolutionX; x++)
                for (int z = 0; z < descriptor.ResolutionZ; z++)
                {
                    var index = Utils.FromSampleIndicesToIndex(in resolution, x, z);
                    Color newColor = descriptor.IsFixed(index) ? descriptor.fixedColor : descriptor.defaultColor;
                    _previewTexture.SetPixel(x, z, newColor);
                }

            _previewTexture.Apply();
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            descriptor = (WaveMakerDescriptor)target;

            // Update texture data if resolution changed
            if (_previewTexture == null || descriptor.ResolutionX != _previewTexture.width || descriptor.ResolutionZ != _previewTexture.height)
                CreateTexture();

            UpdateTexture();

            GUI.DrawTexture(r, _previewTexture, ScaleMode.ScaleToFit, false, 1);
        }


    }

#endif
}
