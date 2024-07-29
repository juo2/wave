using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaveMaker
{
    [HelpURL("http://wavemaker.lidia-martinez.com/")]
    [CreateAssetMenu(fileName = "WaveMakerDescriptor", menuName = "WaveMaker Descriptor", order = 10)]
    public class WaveMakerDescriptor : ScriptableObject
    {
#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)

        public event EventHandler OnResolutionChanged;
        public event EventHandler OnFixedGridChanged;
        public event EventHandler OnDestroyed;

        public IntegerPair Resolution { get; private set; }

        /// <summary>Number of vertices/samples along the X axis </summary>
        public int ResolutionX {
            get => _resolution.x;
            set 
            {
                if (_resolution.x != value)
                    SetResolution(value, ResolutionZ);
            }
        }

        /// <summary>Number of vertices/samples along the Z axis</summary>
        public int ResolutionZ {
            get => _resolution.z;
            set
            {
                if (_resolution.z != value)
                    SetResolution(ResolutionX, value);
            }
        }

        [HideInInspector]
        public Color defaultColor = Color.white;

        [HideInInspector]
        public Color fixedColor = Color.black;
        
        public bool IsInitialized => _isInitialized;

        public ref bool[] FixedGridRef => ref fixedGrid;

        public static int MaxVertices => _maxVertices;
        public static int MinResolution => _minResolution;

        [SerializeField]
        bool[] fixedGrid;

        bool _isInitialized = false;

        [SerializeField]
        IntegerPair _resolution = new IntegerPair(50, 50);
        IntegerPair _oldResolution;
        const int _maxVertices = 65536;
        const int _minResolution = 3;

        private void Awake()
        {
            _isInitialized = false;
        }

        private void OnEnable()
        {
            _oldResolution = _resolution;
            UpdateFixedGrid();
            _isInitialized = true;
        }

        /// <summary>
        /// Use this function to fix and unfix samples on the grid.
        /// </summary>
        /// <param name="x">0 to ResolutionX - 1</param>
        /// <param name="z">0 to ResolutionZ - 1</param>
        /// <param name="isFixed">New fixed status</param>
        public void SetFixed(int x, int z, bool isFixed)
        {
            if (x < 0 || x >= ResolutionX || z < 0 || z >= ResolutionZ)
            {
                Debug.LogError("WaveMaker - Cannot set the fixed status to the given sample. It is out of bounds. " + x + " - " + z);
                return;
            }

            SetFixed(Utils.FromSampleIndicesToIndex(Resolution, in x, in z), isFixed);
        }

        public void SetFixed(int index, bool isFixed)
        {
            if (index >= fixedGrid.Length || index < 0)
            {
                Debug.LogError(string.Format("WaveMaker - Cannot set the fixed status to the given sample index {0}. It is out of bounds.", index));
                return;
            }

            fixedGrid[index] = isFixed;
            OnFixedGridChanged?.Invoke(this, null);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        ///<summary>If you are using this very often, then it is recommended to grab the reference of the fixeGrid array using the property</summary>
        /// <returns>True if the status of the given sample is fixed or not.</returns>
        public bool IsFixed(int index)
        {
            if (index < 0 || index >= fixedGrid.Length)
            {
                Debug.LogError("WaveMaker - Cannot get the fixed status to the given sample. It is out of bounds.");
                return true;
            }

            return fixedGrid[index];
        }

        /// <summary>
        /// Change resolution of the descriptor. This will make the whole grid regenerate
        /// </summary>
        public void SetResolution(int newResolutionX, int newResolutionZ)
        {
            if (newResolutionX * newResolutionZ > _maxVertices)
            {
                if (newResolutionX > newResolutionZ)
                    newResolutionX = newResolutionZ / _maxVertices;
                
                if (newResolutionZ > newResolutionX)
                    newResolutionZ = newResolutionX / _maxVertices;

                Debug.LogError("WaveMaker - Descriptor resolution cannot generate a mesh with more than (" + _maxVertices + "). Clamping biggest resolution.");
            }

            if (newResolutionX < _minResolution || newResolutionZ < _minResolution)
            {
                newResolutionX = newResolutionX > _minResolution? _minResolution: newResolutionX;
                newResolutionZ = newResolutionZ > _minResolution? _minResolution: newResolutionZ;
                Debug.LogError("WaveMaker - Descriptor resolution cannot be less than " + _minResolution + "). Clamping.");
            }

            if (_resolution.x == newResolutionX && _resolution.z == newResolutionZ)
                return; 

            _oldResolution = _resolution;
            _resolution = new IntegerPair(newResolutionX, newResolutionZ);
            UpdateFixedGrid();

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// It will set to fixed all border samples
        /// </summary>
        public void FixBorders()
        {

            for (int x = 0; x < ResolutionX; x++)
                for (int z = 0; z < ResolutionZ; z++)
                    if ( x == 0 || z == 0 || x == ResolutionX-1 || z == ResolutionZ-1)
                        fixedGrid[ResolutionX * z + x] = true;

            OnFixedGridChanged?.Invoke(this, null);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// will update the fixed grid changing the resolution. 
        /// </summary>
        /// <param name="copyPreviousStatus">Current values will be kept if it grows or is reduced, adding unfixed values if growing</param>
        public void UpdateFixedGrid(bool copyPreviousStatus = true)
        {
            if (fixedGrid == null)
                copyPreviousStatus = false;
            
            if (copyPreviousStatus)
            {
                // Create a new fixed grid with the new size
                bool[] fixedGridAux = new bool[ResolutionX * ResolutionZ];

                // Copy all values from the old one to the new one
                for (int x = 0; x < ResolutionX; x++)
                    for (int z = 0; z < ResolutionZ; z++)
                    {
                        int index = Utils.FromSampleIndicesToIndex(_resolution, x, z);
                        fixedGridAux[index] = false;

                        // Copy old samples
                        if (copyPreviousStatus && x < _oldResolution.x && z < _oldResolution.z)
                            fixedGridAux[index] = fixedGrid[Utils.FromSampleIndicesToIndex(in _oldResolution, x, z)];
                    }

                fixedGrid = fixedGridAux;
            }
            else
            {
                fixedGrid = new bool[ResolutionX * ResolutionZ];
                for (int i = 0; i < fixedGrid.Length; i++)
                    fixedGrid[i] = false;
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

            OnResolutionChanged?.Invoke(this, null);
        }
        
        /// <summary>
        /// Set all samples to fixed or unfixed status
        /// </summary>
        public void SetAllFixStatus(bool newValue = false)
        {
            for (int i = 0; i < fixedGrid.Length; i++)
                fixedGrid[i] = newValue;

            OnFixedGridChanged?.Invoke(this, null);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        private void OnDestroy()
        {
            //TODO: NOT CALLED! Using AssetModificationProcessor instead
            OnDestroyed?.Invoke(this, null);
        }
#endif
    }

#if UNITY_EDITOR && (UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED))
    public class WaveMakerDescriptorDeleteDetector : UnityEditor.AssetModificationProcessor
        {
            static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions opt)
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(WaveMakerDescriptor))
                {
                    foreach (var surf in GameObject.FindObjectsOfType<WaveMakerSurface>())
                    {
                        if (surf.Descriptor != null && path == AssetDatabase.GetAssetPath(surf.Descriptor.GetInstanceID()))
                            surf.Descriptor = null;
                    }
                }
                return AssetDeleteResult.DidNotDelete;
            }
        }
#endif
}


