using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

namespace WaveMaker.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(WaveMakerSurface))]
    public class WaveMakerSurfaceEditor : UnityEditor.Editor
    {
#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

        float _auxWidth, _auxDepth;

#if UNITY_2019_1_OR_NEWER
        bool showWaveSimulationProperties = true;
        bool showInteractionProperties = true;
        bool showCellProperties = false;
        bool showOtherSettings = false;
#endif

        bool _meshSizeChanged = false;

        WaveMakerSurface _surface;

        // General properties
        SerializedProperty interactionType;
        SerializedProperty substeps;
        SerializedProperty damping;
        SerializedProperty propagationSpeed;
        SerializedProperty waveSmoothness;
        SerializedProperty speedTweak;
        SerializedProperty collisionDetectionHeight;
        SerializedProperty showDetailedLogMessages;

        // Velocity based
        SerializedProperty verticalPushScale;
        SerializedProperty horizontalPushScale;
        SerializedProperty interactorMaximumSpeedClamp;
        SerializedProperty interactorMinimumSpeedClamp;

        // Buoyancy based
        SerializedProperty simulate;
        SerializedProperty buoyancy;
        SerializedProperty horizontalBuoyancy;
        SerializedProperty detectionDepth;
        SerializedProperty density;
        SerializedProperty buoyancyDamping;
        SerializedProperty nMaxCellsPerInteractor;
        SerializedProperty nMaxInteractorsDetected;

        // TODO: Obsolete
        //SerializedProperty nMaxInteractorsPerCell;

        SerializedProperty effectScale;
        SerializedProperty showSettingWarningLogs;

        // Paint Mode
        bool paintModeEnabled = false;
        static PaintMode paintMode = PaintMode.FIX;
        [Min(0)] static float pencilRadius = 1;
        static Color paintAreaColor = Color.red;
        Mesh _cellPropertiesMesh;

        // Fixed cells
        public enum PaintMode { FIX, UNFIX }
        bool showFixedSamples = false;
        int fixDetectionLayer = 0;
        Color32[] _fixedSampleColors;

        // Interaction mode
        bool interactionModeEnabled = false;

        /// <summary>
        /// Material used for the paint mode. It contains a custom shader that draws colors on mesh
        /// </summary>
        Material MaterialFixedCells { get => _materialPaintMode; set => _materialPaintMode = value; }

        Material _materialPaintMode;

        bool _keyPressed = false;


        /***********************************************/

        #region METHODS

        private void OnEnable()
        {
            _keyPressed = false;
            _meshSizeChanged = false;

            _surface = (WaveMakerSurface)target;

            Undo.undoRedoPerformed += UndoRedoPerformed;

            if (Application.isPlaying)
                _surface.OnAwakeStatusChanged.AddListener(OnAwakeStatusChanged);

            // General
            interactionType = serializedObject.FindProperty("interactionType");
            substeps = serializedObject.FindProperty("substeps");
            damping = serializedObject.FindProperty("damping");
            propagationSpeed = serializedObject.FindProperty("propagationSpeed");
            waveSmoothness = serializedObject.FindProperty("waveSmoothness");
            speedTweak = serializedObject.FindProperty("speedTweak");
            collisionDetectionHeight = serializedObject.FindProperty("collisionDetectionHeight");
            showDetailedLogMessages = serializedObject.FindProperty("showDetailedLogMessages");

            // Velocity based
            verticalPushScale = serializedObject.FindProperty("verticalPushScale");
            horizontalPushScale = serializedObject.FindProperty("horizontalPushScale");
            interactorMaximumSpeedClamp = serializedObject.FindProperty("interactorMaximumSpeedClamp");
            interactorMinimumSpeedClamp = serializedObject.FindProperty("interactorMinimumSpeedClamp");

            // Buoyancy Based
            simulate = serializedObject.FindProperty("simulate");
            buoyancy = serializedObject.FindProperty("buoyancy");
            horizontalBuoyancy = serializedObject.FindProperty("horizontalBuoyancy");
            detectionDepth = serializedObject.FindProperty("detectionDepth");
            density = serializedObject.FindProperty("density");
            buoyancyDamping = serializedObject.FindProperty("buoyancyDamping");
            nMaxCellsPerInteractor = serializedObject.FindProperty("nMaxCellsPerInteractor");
            nMaxInteractorsDetected = serializedObject.FindProperty("nMaxInteractorsDetected");

            // TODO: Obsolete
            //nMaxInteractorsPerCell = serializedObject.FindProperty("nMaxInteractorsPerCell");

            effectScale = serializedObject.FindProperty("effectScale");
            showSettingWarningLogs = serializedObject.FindProperty("showSettingWarningLogs");

            //TODO: Provisional solution, scriptableObjects deleted via the project view don't execute OnDestroy. 
            // If the descriptor has been deleted before selecting the waveMakerSurface...
            if (_surface.Descriptor == null && _surface.IsInitialized)
                _surface.Uninitialize();
            
            _auxWidth = _surface._size_ls.x;
            _auxDepth = _surface._size_ls.y;
        }

        private void InitializeCellPropertiesData()
        {
            if (Application.isPlaying)
                return;

            if (_surface.Descriptor == null)
                return;

            if (_cellPropertiesMesh != null)
                DestroyImmediate(_cellPropertiesMesh);

            // Sometimes Application.isPlaying is not detected at this point
            if (_surface.MeshManager == null || _surface.MeshManager.Mesh == null)
                return; 

            Mesh sharedMesh = _surface.MeshManager.Mesh;

            _cellPropertiesMesh = new Mesh
            {
                vertices = sharedMesh.vertices,
                triangles = sharedMesh.triangles,
                uv = sharedMesh.uv,
                normals = sharedMesh.normals,
                tangents = sharedMesh.tangents
            };

            //TODO: Assign default material first. find a way to find a material and shader not by name
            //  mr.material = new Material(Shader.Find("diffuse"))??  Test this
            if (MaterialFixedCells == null)
            {
                Shader shader = Shader.Find("WaveMaker/WaveMakerFixedPreviewShader");

                if (shader != null)
                    MaterialFixedCells = new Material(shader);
                else
                {
                    Utils.LogError("'WaveMakerFixedPreviewShader' not found. Please fix the name or reinstall WaveMaker", _surface.gameObject);
                    MaterialFixedCells = new Material(Shader.Find("Diffuse"));
                }
            }

            UpdateFixedSamplesMeshColors();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoRedoPerformed;

            if (Application.isPlaying)
                _surface.OnAwakeStatusChanged.RemoveListener(OnAwakeStatusChanged);
            else
            {
                if (paintModeEnabled)
                    DisablePaintMode();
                if (interactionModeEnabled)
                    DisableInteractionMode();
                UninitializeCellPropertiesData();
            }
        }

        private void UninitializeCellPropertiesData()
        {
            DestroyImmediate(MaterialFixedCells);
            DestroyImmediate(_cellPropertiesMesh);
        }

        private void OnAwakeStatusChanged()
        {
            Repaint();
        }

        #endregion
#endif
        /***********************************************/

        #region INSPECTOR GUI METHODS

        public override void OnInspectorGUI()
        {
#if !UNITY_2018 && (!MATHEMATICS_INSTALLED || !BURST_INSTALLED || !COLLECTIONS_INSTALLED)
            EditorGUILayout.HelpBox("PACKAGES MISSING: Please follow the QuickStart in the main WaveMaker folder or visit the official website linked in the help icon on this component.", MessageType.Warning);
            return;
#endif

#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)
            serializedObject.Update();

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space();
                DrawDescriptorSelectionGUI();
            }

            if (_surface.Descriptor == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Read tooltips for details and visit the online documentation linked in the help icon next to the component name.", MessageType.Info);

            EditorGUILayout.Space();
            if (!Application.isPlaying)
                DrawMeshSizeGUI();
            else
                DrawAwakeStatusGUI();

            EditorGUILayout.Space();
            DrawWaveSimulationPropertiesGUI();

            EditorGUILayout.Space();
            DrawInteractionPropertiesGUI();
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.Space();
                DrawCellProperties();
                
                // TODO: Interaction mode not finished
                //EditorGUILayout.Space();
                //DrawInteractionModeGUI();
            }

            EditorGUILayout.Space();
            DrawOtherSettings();

            serializedObject.ApplyModifiedProperties();

#endif

        }

#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

        private void DrawOtherSettings()
        {
#if UNITY_2019_1_OR_NEWER
            showOtherSettings = EditorGUILayout.Foldout(showOtherSettings, "Other Settings", EditorStyles.foldoutHeader);
            if (showOtherSettings)
            {
                EditorGUILayout.Space();
#else
            {
#endif
                showDetailedLogMessages.boolValue = EditorGUILayout.ToggleLeft(" Show Detailed Log Messages", showDetailedLogMessages.boolValue);
                EditorGUILayout.Space();
            }
        }

        private void DrawWaveSimulationPropertiesGUI()
        {
#if UNITY_2019_1_OR_NEWER
            showWaveSimulationProperties = EditorGUILayout.Foldout(showWaveSimulationProperties, "Wave Simulation Properties", EditorStyles.foldoutHeader);
            if (showWaveSimulationProperties)
            {
                EditorGUILayout.Space();
#else
            GUILayout.Label("Wave Simulation Properties", EditorStyles.boldLabel);
            { 
#endif
                EditorGUIUtility.labelWidth = 150;
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(propagationSpeed);

                EditorGUIUtility.labelWidth = 0;
                EditorGUILayout.PropertyField(damping);
                EditorGUILayout.PropertyField(waveSmoothness);
                EditorGUILayout.PropertyField(speedTweak);

                EditorGUIUtility.labelWidth = 150;
                EditorGUILayout.PropertyField(substeps, new GUIContent("Substeps (Advanced)"));

                DrawStabilityWarningGUI();

                EditorGUIUtility.labelWidth = 0;
            }
        }

        private void DrawInteractionPropertiesGUI()
        {
#if UNITY_2019_1_OR_NEWER
            showInteractionProperties = EditorGUILayout.Foldout(showInteractionProperties, "Interaction Properties", EditorStyles.foldoutHeader);
            if (showInteractionProperties)
            {
                EditorGUILayout.Space();
#else
            GUILayout.Label("Interaction Properties", EditorStyles.boldLabel);
            {
#endif
                EditorGUILayout.Space();

                if (!Application.isPlaying)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(interactionType);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (_surface.interactionType == WaveMakerSurface.InteractionType.VelocityBased)
                        {
                            Undo.RecordObject(_surface, "WaveMaker Surface simulation enabled");
                            _surface.simulate = true;
                        }
                    }
                }

                EditorGUILayout.Space();

                if (_surface.interactionType == WaveMakerSurface.InteractionType.OccupancyBased)
                    DrawBuoyancyBasedPropertiesGUI();

                else if (_surface.interactionType == WaveMakerSurface.InteractionType.VelocityBased)
                    DrawVelocityBasedPropertiesGUI();
            }
        }

        private void DrawVelocityBasedPropertiesGUI()
        {
            EditorGUIUtility.labelWidth = 150;
            EditorGUILayout.PropertyField(verticalPushScale);
            EditorGUILayout.PropertyField(horizontalPushScale);
            EditorGUILayout.PropertyField(interactorMinimumSpeedClamp, new GUIContent("Interactor Min speed"));
            EditorGUILayout.PropertyField(interactorMaximumSpeedClamp, new GUIContent("Interactor Max speed"));
            EditorGUIUtility.labelWidth = 0;
        }

        private void DrawBuoyancyBasedPropertiesGUI()
        {
            EditorGUIUtility.labelWidth = 135;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(detectionDepth, new GUIContent("Depth"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                _surface.UpdateCollider();
            }

            /******************* SIMULATION ****************************/
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(simulate, new GUIContent("Simulate Surface"));
            if (EditorGUI.EndChangeCheck())
            {
                if (!simulate.boolValue && !buoyancy.boolValue)
                {
                    simulate.boolValue = true;
                    Utils.LogWarning("Instead of disabling simulation and buoyancy, please disable the whole component.", _surface.gameObject);
                }
            }

            EditorGUI.BeginDisabledGroup(!simulate.boolValue);
            EditorGUILayout.PropertyField(effectScale, new GUIContent("Simulation scale"));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            /******************* LIMITS *******************************/
            EditorGUIUtility.labelWidth = 170;
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            EditorGUILayout.PropertyField(nMaxInteractorsDetected, new GUIContent("Max Detected Interactors"));
            EditorGUILayout.PropertyField(nMaxCellsPerInteractor, new GUIContent("Max Cells per Interactor"));

            // TODO: Obsolete
            //EditorGUILayout.PropertyField(nMaxInteractorsPerCell, new GUIContent("Max Overlapped Interactors"));

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();

            /********************  BUOYANCY ****************************/
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(buoyancy, new GUIContent("Buoyancy   ↑"));
            if (EditorGUI.EndChangeCheck())
            {
                if (!buoyancy.boolValue && !simulate.boolValue)
                {
                    buoyancy.boolValue = true;
                    Utils.LogWarning("Instead of disabling simulation and buoyancy, please disable the whole component.", _surface.gameObject);
                }
            }


            EditorGUI.BeginDisabledGroup(!buoyancy.boolValue);
            EditorGUILayout.PropertyField(horizontalBuoyancy, new GUIContent("Drifting   ⇄"));
            EditorGUILayout.PropertyField(density);
            EditorGUILayout.PropertyField(buoyancyDamping, new GUIContent("Damping"));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(showSettingWarningLogs);

            EditorGUIUtility.labelWidth = 0;
        }

        private void DrawStabilityWarningGUI()
        {
            foreach(var t in targets)
            {
                var surface = (WaveMakerSurface)t;

                // Check Stability condition
                if (!surface.CheckStabilityCondition())
                {
                    EditorGUILayout.Space();

                    if (serializedObject.isEditingMultipleObjects)
                    {
                        EditorGUILayout.HelpBox(string.Format("Warning! At least one of the selected surfaces '{0}' has a settings warning. Please select only that surface for more info.", surface.name), MessageType.Warning);
                        break;
                    }
                    else
                        EditorGUILayout.HelpBox("Warning! The surface can be unstable with these settings. Reduce descriptor resolution or reduce propagation speed. The last option is to increase substeps but it will be take more time to compute.", MessageType.Warning);
                }
            }
        }

        private void DrawSizeRatioWarningGUI()
        {
            foreach (var t in targets)
            {
                var surface = (WaveMakerSurface)t;

                // Check size ratio
                if (surface._sampleSize_ls.x / surface._sampleSize_ls.y > 2 || surface._sampleSize_ls.y / surface._sampleSize_ls.x > 2)
                {
                    EditorGUILayout.Space();

                    if (serializedObject.isEditingMultipleObjects)
                    {
                        EditorGUILayout.HelpBox(string.Format("Warning! At least one of the selected surfaces '{0}' has a settings warning. Please select only that surface for more info.", surface.name), MessageType.Warning);
                        break;
                    }
                    else
                        EditorGUILayout.HelpBox("Warning! Cells must be approximately square in local space. Set scale to 1. Activate the Wireframe shading in the Scene View to find a proper combination of resolution and surface size.", MessageType.Warning);
                }
            }
        }

        private void DrawDescriptorSelectionGUI()
        {
            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox("Setting a Descriptor in multiple selected surfaces is currently not supported, but the feature will be added soon.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Descriptor", GUILayout.ExpandWidth(false), GUILayout.MaxWidth(70));
            EditorGUI.BeginChangeCheck();
            var newDescriptor = (WaveMakerDescriptor)EditorGUILayout.ObjectField(_surface.Descriptor, typeof(WaveMakerDescriptor), false, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                UninitializeCellPropertiesData();

                Undo.RecordObject(_surface, "WaveMaker Descriptor changed");
                _surface.Descriptor = newDescriptor;
                InitializeCellPropertiesData();
            }
            EditorGUILayout.EndHorizontal();

            if (_surface.Descriptor == null)
                EditorGUILayout.HelpBox("Attach a Wave Maker Descriptor file (Right click on project view, Create, WaveMakerDescriptor) that defines the resolution and stores properties for each cell of the surface.", MessageType.Info);
        }

        private void DrawAwakeStatusGUI()
        {
            if (serializedObject.isEditingMultipleObjects)
                return;

            if (!_surface.IsInitialized)
            {
                EditorGUILayout.LabelField(new GUIContent(string.Format("Status: Disabled!")));
                return;
            }

            string text = "";
            if (_surface.IsAwake)
            {
                text += "Awake.";

                if (_surface.IsAwakeDueToInteraction() || _surface.IsAwakeDueToSimulation())
                {
                    if (_surface.IsAwakeDueToInteraction())
                        text += " Interacting with collider.";

                    if (_surface.IsAwakeDueToSimulation())
                        text += " Simulating.";
                }
                else
                    text += " Getting asleep...";
            }
            else
                text = "Asleep (not simulating or interacting).";

            EditorGUILayout.LabelField(new GUIContent(string.Format("Status: {0}", text)));
        }

        private void DrawMeshSizeGUI()
        {
            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox("Changing size in multiple selected surfaces is currently not supported, but the feature will be added soon.", MessageType.Info);
                DrawSizeRatioWarningGUI();
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Local Mesh Size", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Width", GUILayout.MinWidth(15));
            _auxWidth = EditorGUILayout.FloatField(_auxWidth, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Depth", GUILayout.MinWidth(15));
            _auxDepth = EditorGUILayout.FloatField(_auxDepth, GUILayout.ExpandWidth(true));
            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
                _meshSizeChanged = true;
            
            EditorGUILayout.EndHorizontal();

            if (_meshSizeChanged)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Apply"))
                {
                    Undo.RecordObject(_surface, "WaveMaker Surface resolution changed");
                    _surface.Size_ls = new Vector2(_auxWidth, _auxDepth);

                    // Get clamped vals
                    _auxWidth = _surface.Size_ls.x;
                    _auxDepth = _surface.Size_ls.y;
                    _meshSizeChanged = false;
                }
            }

            DrawSizeRatioWarningGUI();
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        private void DrawCellProperties()
        {
            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox("Cell Properties section in multiple selected surfaces is currently not supported. Edit just one, if the descriptor is shared, the rest will be updated.", MessageType.Info);
                return;
            }


#if UNITY_2019_1_OR_NEWER
            showCellProperties = EditorGUILayout.Foldout(showCellProperties, "Fixed Cells", EditorStyles.foldoutHeader);
            if (showCellProperties)
            {
                EditorGUILayout.Space();
#else
            GUILayout.Label("Cell Properties", EditorStyles.boldLabel);
            {
#endif
                EditorGUILayout.HelpBox("Changes will be stored in the Descriptor and can be shared between surfaces.", MessageType.Info);
                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();
                showFixedSamples = EditorGUILayout.ToggleLeft(" Show in Scene View", showFixedSamples);
                if (EditorGUI.EndChangeCheck())
                {
                    // Turned on
                    if (showFixedSamples)
                    {
#if UNITY_2019_1_OR_NEWER
                        foreach (SceneView view in SceneView.sceneViews)
                            view.drawGizmos = true;
#endif

                        InitializeCellPropertiesData();
                    }
                    // turned off
                    else if (!showFixedSamples)
                    {
                        if (paintModeEnabled)
                        {
                            //TODO: Instad of showing a warning, set interactable to false. But since this is not a variable or a class... no idea
                            showFixedSamples = true;
                            Debug.LogError("Cannot disable display of fixed cells while in paint mode");
                        }
                        else
                            DisableFixedSamplesDisplay();
                    }
                }

                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Fix All"))
                {
                    Undo.RecordObject(_surface.Descriptor, "WaveMaker Descriptor Fixed Cells Change");
                    _surface.Descriptor.SetAllFixStatus(true);
                    UpdateFixedSamplesMeshColors();
                }
                if (GUILayout.Button("Unix All"))
                {
                    Undo.RecordObject(_surface.Descriptor, "WaveMaker Descriptor Fixed Cells Change");
                    _surface.Descriptor.SetAllFixStatus(false);
                    UpdateFixedSamplesMeshColors();
                }
                if (GUILayout.Button("Fix Borders"))
                {
                    Undo.RecordObject(_surface.Descriptor, "WaveMaker Descriptor Fixed Cells Change");
                    _surface.Descriptor.FixBorders();
                    UpdateFixedSamplesMeshColors();
                }
                GUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                DrawPaintModeGUI();

                EditorGUILayout.Space();
                DrawAutomaticDetectionGUI();
            }
        }
        
        private void DrawAutomaticDetectionGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Automatic detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(collisionDetectionHeight, new GUIContent("Distance Detected"));
            fixDetectionLayer = EditorGUILayout.LayerField(new GUIContent("Layer Detected"), fixDetectionLayer);
            if (GUILayout.Button("Fix cells touching colliders in that layer"))
            {
                _surface.FixCollisions(fixDetectionLayer);
                EnableFixedSamplesDisplay();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPaintModeGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Paint Mode (by hand)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var newPaintModeEnabled = GUILayout.Toggle(paintModeEnabled, new GUIContent(paintModeEnabled ? "Disable" : "Enable"), EditorStyles.miniButton, GUILayout.ExpandWidth(false), GUILayout.MinHeight(20));
            if (EditorGUI.EndChangeCheck())
            {
                if (newPaintModeEnabled)
                {
#if UNITY_2019_1_OR_NEWER
                    foreach (SceneView view in SceneView.sceneViews)
                        view.drawGizmos = true;
#endif

                    EnablePaintMode();
                }
                else
                    DisablePaintMode();
            }

            if (paintModeEnabled)
            {
                paintMode = (PaintMode)EditorGUILayout.EnumPopup(new GUIContent("Mode"), paintMode);
                pencilRadius = EditorGUILayout.FloatField(new GUIContent("Pencil Radius"), pencilRadius);
                paintAreaColor = EditorGUILayout.ColorField(new GUIContent("Pencil Color"), paintAreaColor);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawInteractionModeGUI()
        {
            EditorGUI.BeginChangeCheck();
            var newInteractionModeEnabled = GUILayout.Toggle(interactionModeEnabled, new GUIContent(interactionModeEnabled ? "Interaction Mode (Enabled)" : "Interaction Mode (Disabled)"), EditorStyles.miniButton, GUILayout.ExpandWidth(false), GUILayout.MinHeight(20));
            if (EditorGUI.EndChangeCheck())
            {
                if (newInteractionModeEnabled)
                    EnableInteractionMode();
                else
                    DisableInteractionMode();
            }
        }
#endif

        #endregion

        /***********************************************/

        #region SCENE PAINT METHODS

#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)
        
        private void OnSceneGUI()
        {
            if (_surface == null)
                return;

            if (!Application.isPlaying && (paintModeEnabled || interactionModeEnabled))
            {
                if (Event.current.type == EventType.KeyDown)
                    _keyPressed = true;

                if (Event.current.type == EventType.KeyUp)
                    _keyPressed = false;

                if (_keyPressed)
                    return;

                //TODO: Maybe do just once
                // Don't allow selecting another object
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

                // Get ray from the camera at the mouse position
                var mousePos = Event.current.mousePosition;

                mousePos.y = Camera.current.pixelHeight - mousePos.y;
                var ray = Camera.current.ScreenPointToRay(mousePos);

                // Hit with this wave maker object
                var plane = new Plane(_surface.transform.up, _surface.transform.position);
                if (plane.Raycast(ray, out float dist))
                {
                    Vector3 hitPos = ray.origin + ray.direction * dist;

                    if (paintModeEnabled)
                    {
                        Handles.color = paintAreaColor;
                        Handles.DrawWireDisc(hitPos, _surface.transform.up, pencilRadius);
                    }

                    SceneView.RepaintAll();

                    if (Event.current.button == 0 && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag))
                    {
                        if (paintModeEnabled)
                        {
                            if (Event.current.type == EventType.MouseDown)
                                Undo.RegisterCompleteObjectUndo(_surface.Descriptor, "WaveMaker Descriptor Fixed Cells Change");

                            PaintArea(hitPos);
                        }
                        else if (interactionModeEnabled)
                            InteractWithPos(hitPos);
                    }
                }
            }

            if (showFixedSamples)
                Graphics.DrawMesh(_cellPropertiesMesh, _surface.transform.localToWorldMatrix, _materialPaintMode, 0);
        }

        private void UpdateFixedSamplesMeshColors()
        {
            var desc = _surface.Descriptor;
            _fixedSampleColors = new Color32[desc.ResolutionX * desc.ResolutionZ];
            for (int i = 0; i < _fixedSampleColors.Length; i++)
                _fixedSampleColors[i] = desc.IsFixed(i) ? desc.fixedColor : desc.defaultColor;
        
            if (_cellPropertiesMesh != null)
                _cellPropertiesMesh.colors32 = _fixedSampleColors;
        }

        private void InteractWithPos(Vector3 pos_ws)
        {
            float3 pos_ls = _surface.transform.worldToLocalMatrix.MultiplyPoint3x4(pos_ws);
            int index = Utils.GetNearestSampleFromLocalPosition(pos_ls, in _surface._size_ls, in _surface._sampleSize_ls);
            _surface.SetHeightOffset(index, 10f);
        }

        public void EnableFixedSamplesDisplay()
        {
            showFixedSamples = true;
            InitializeCellPropertiesData();
            UpdateFixedSamplesMeshColors();
        }

        public void DisableFixedSamplesDisplay()
        {
            showFixedSamples = false;

            UninitializeCellPropertiesData();

            //TODO: In 2019+ stopping Graphics.DrawMesh for the fixed samples mesh doesn't stop it from showing up. 
            // This is a workaround until a solution is found.
            _surface.enabled = false;
            _surface.enabled = true;

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        public void EnablePaintMode()
        {
            if (Application.isPlaying)
                return;

            if (interactionModeEnabled)
                DisableInteractionMode();

            paintModeEnabled = true;
            EnableFixedSamplesDisplay();

            Tools.current = Tool.None;
        }

        public void DisablePaintMode()
        {
            if (Application.isPlaying)
                return;

            DisableFixedSamplesDisplay();
            paintModeEnabled = false;
            Tools.current = Tool.Move;
        }

        public void EnableInteractionMode()
        {
            if (Application.isPlaying)
                return;

            if (paintModeEnabled)
                DisablePaintMode();

            _surface.Initialize();
            interactionModeEnabled = true;
            Tools.current = Tool.None;
        }

        public void DisableInteractionMode()
        {
            if (Application.isPlaying)
                return;

            interactionModeEnabled = false;
            Tools.current = Tool.Move;
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <param name="hitPoint_ws">A position in worldspace where to paint</param>
        public void PaintArea(Vector3 hitPoint_ws)
        {
            if (Application.isPlaying || !_surface.Descriptor.IsInitialized)
                return;

            var hitPoint_ls = _surface.transform.InverseTransformPoint(hitPoint_ws);
            int centerIndex  = Utils.GetNearestSampleFromLocalPosition(hitPoint_ls, in _surface._size_ls, in _surface._sampleSize_ls);
            Utils.FromIndexToSampleIndices(centerIndex, in _surface._resolution, out int centerX, out int centerZ);

            var sampleSize = _surface._sampleSize_ls;
            sampleSize.x *= _surface.transform.localScale.x;
            sampleSize.y *= _surface.transform.localScale.z;
            int radiusCellSizeX = Mathf.Max(1, Mathf.FloorToInt(pencilRadius / sampleSize.x));
            int radiusCellSizeZ = Mathf.Max(1, Mathf.FloorToInt(pencilRadius / sampleSize.y));
            bool colorsModified = false;

            for (int x = centerX - radiusCellSizeX; x <= centerX + radiusCellSizeX; x++)
            {
                if (x < 0 || x >= _surface.Descriptor.ResolutionX)
                    continue;

                for (int z = centerZ - radiusCellSizeZ; z <= centerZ + radiusCellSizeZ; z++)
                {
                    if (z < 0 || z >= _surface.Descriptor.ResolutionZ)
                        continue;

                    int index = Utils.FromSampleIndicesToIndex(in _surface._resolution, in x, in z);
                    Vector3 pos_ls = _surface.GetPositionFromSample(index);

                    // Ignore out of the circle area
                    if ((hitPoint_ls - pos_ls).magnitude > pencilRadius)
                        continue;

                    switch (paintMode)
                    {
                        case PaintMode.FIX:
                            _surface.Descriptor.SetFixed(index, true);
                            _fixedSampleColors[index] = _surface.Descriptor.fixedColor;
                            colorsModified = true;
                            break;

                        case PaintMode.UNFIX:
                            _surface.Descriptor.SetFixed(index, false);
                            _fixedSampleColors[index] = _surface.Descriptor.defaultColor;
                            colorsModified = true;
                            break;

                        default:
                            break;
                    }
                }
            }

            if (showFixedSamples && colorsModified)
                _cellPropertiesMesh.colors32 = _fixedSampleColors;
        }

        private void UndoRedoPerformed()
        {
            if (_surface._size_ls.x != _auxWidth || _surface._size_ls.y != _auxDepth)
            {
                _auxWidth = _surface._size_ls.x;
                _auxDepth = _surface._size_ls.y;
                _surface.Size_ls = new Vector2(_auxWidth, _auxDepth);
            }

            UpdateFixedSamplesMeshColors();
        }

#endif
    }
    #endregion
}
