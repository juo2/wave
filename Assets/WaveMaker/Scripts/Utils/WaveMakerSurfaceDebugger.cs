
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;

#if UNITY_2018 || MATHEMATICS_INSTALLED
using Unity.Mathematics;
#endif

namespace WaveMaker
{
    /// <summary>
    /// This component has to be attached to a Wave Maker Surface to show more detailed debug 
    /// information on the internal simulation of the surface.
    /// </summary>
    [RequireComponent(typeof(WaveMakerSurface))]
    public class WaveMakerSurfaceDebugger : MonoBehaviour
    {
#if UNITY_2018 || (MATHEMATICS_INSTALLED && BURST_INSTALLED && COLLECTIONS_INSTALLED)
        #region Members

        public enum DrawMode
        {
            none,
            grid,
            asleepStatus,
            activeArea,
            gradients,
            normals,
            offset,
            velocity,
            acceleration,
            relativeVelocity,
            globalOccupancy,
            interactorOccupancy,
            interactionData,
        }

        // TODO: Obsolete
        /*
        public enum RayDrawMode
        {
            doNotDraw,
            downwardsOnly,
            upwardsOnly,
            downwardsHitsOnly,
            upwardHitsOnly,
            bothDirectionHits
        }
        */

        public DrawMode drawMode = DrawMode.none;

        // TODO: Obsolete
        //public RayDrawMode rayDrawMode = RayDrawMode.doNotDraw;

        DrawMode currentDrawMode = DrawMode.none;

        public bool showDownwardRays = false;
        public bool showUpwardRays = false;
        public bool showOnlyHittingRays = false;
        public bool showDetectionDepth = false;
        public bool printDetectedInteractors = false;
        public float rayVisualScale = 1f;
        public float offsetClamp = 2f;
        public bool occupancyNormalizedByDepth = true;

        [Tooltip("For display modes that need an interactor selected to show its information in the grid")]
        public int interactorSelected = 0;

        public List<Color> rayHitColors = new List<Color>() { Color.cyan, Color.blue, Color.green, Color.magenta };

        public WaveMakerSurface _surface;

        CustomMeshManager _meshManager;
        MeshRenderer _meshRenderer;
        Material _material;
        Material _materialBackup;
        Color[] _colors;
        Color[] _colorsBackup;
        bool _materialIsOverriden = false;
        bool _initialized = false;

        #endregion

        #region Default Methods

        private void OnEnable()
        {
            _surface = GetComponent<WaveMakerSurface>();
            _surface.OnInitialized.AddListener(Initialize);
            _surface.OnUninitialized.AddListener(Uninitialize);

            if (_surface.IsInitialized)
                Initialize();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            Uninitialize();
        }

        private void OnValidate()
        {
            if (interactorSelected < 0)
                interactorSelected = 0;

            if (rayVisualScale < 0)
                rayVisualScale = 0;

            if (offsetClamp < 0)
                offsetClamp = 0;

            if (interactorSelected < 0)
                interactorSelected = 0;

            if (_surface != null && interactorSelected > _surface.nMaxInteractorsDetected - 1)
                interactorSelected = _surface.nMaxInteractorsDetected - 1;
        }

        private void LateUpdate()
        {
            if (!_initialized)
                return;

            ApplyChangeOfDrawMode(drawMode);
            Draw();
        }

        private void OnDestroy()
        {
            Uninitialize();
            _surface?.OnInitialized.RemoveListener(Initialize);
            _surface?.OnUninitialized.RemoveListener(Uninitialize);
        }

        #endregion

        #region Other Methods

        public void Initialize()
        {
            _materialIsOverriden = false;
            _meshManager = _surface.MeshManager;
            _meshRenderer = GetComponent<MeshRenderer>();
            _colors = new Color[_surface._resolution.x * _surface._resolution.z];

            ApplyChangeOfDrawMode(drawMode);

            _initialized = true;
        }

        public void Uninitialize()
        {
            if (!_initialized)
                return;

            ApplyChangeOfDrawMode(DrawMode.none);
        }

        private void Draw()
        {
            switch (currentDrawMode)
            {
                case DrawMode.none:
                    break;
                case DrawMode.grid:
                    DrawGrid();
                    break;
                case DrawMode.asleepStatus:
                    ShowAsleepStatus();
                    break;
                case DrawMode.activeArea:
                    ShowActiveArea();
                    break;
                case DrawMode.gradients:
                    DrawGradients();
                    break;
                case DrawMode.normals:
                    DrawVectorsGrid(ref _surface._normals, true);
                    break;
                case DrawMode.offset:
                    DrawOffset();
                    break;
                case DrawMode.velocity:
                    DrawVerticalVectorsGrid(ref _surface._velocities);
                    break;
                case DrawMode.acceleration:
                    DrawVerticalVectorsGrid(ref _surface._accelerations);
                    break;
                case DrawMode.relativeVelocity:
                    if (_surface.interactionType == WaveMakerSurface.InteractionType.VelocityBased)
                        DrawVectorsGrid(ref _surface._relativeVelocities, false);
                    break;
                case DrawMode.interactionData:
                    if (_surface.interactionType == WaveMakerSurface.InteractionType.OccupancyBased)
                        ShowInteractionData();
                    break;
                case DrawMode.globalOccupancy:
                    if (_surface.interactionType == WaveMakerSurface.InteractionType.OccupancyBased)
                        ShowGlobalOccupancy();
                    break;
                case DrawMode.interactorOccupancy:
                    if (_surface.interactionType == WaveMakerSurface.InteractionType.OccupancyBased)
                        ShowInteractorOccupancy();
                    break;
            }

            if (showDetectionDepth)
                DrawDetectionDepth();

            // TODO: Obsolete
            /*
            if (_surface.interactionType == WaveMakerSurface.InteractionType.OccupancyBased)
            {
                switch (rayDrawMode)
                {
                    case RayDrawMode.downwardsOnly:
                        DrawDownwardRays(in _surface._raycasts);
                        break;
                    case RayDrawMode.upwardsOnly:
                        DrawUpwardRays(in _surface._raycasts);
                        break;
                    case RayDrawMode.downwardsHitsOnly:
                        DrawDownwardRaysHitting(in _surface._raycasts, in _surface._raycastResults);
                        break;
                    case RayDrawMode.upwardHitsOnly:
                        DrawUpwardRaysHitting(in _surface._raycasts, in _surface._raycastResults);
                        break;
                    case RayDrawMode.bothDirectionHits:
                        DrawDownwardRaysHitting(in _surface._raycasts, in _surface._raycastResults);
                        DrawUpwardRaysHitting(in _surface._raycasts, in _surface._raycastResults);
                        break;
                    default:
                        break;
                }
            }
            */

            if (printDetectedInteractors)
                PrintInteractors();
        }

        public void ApplyChangeOfDrawMode(DrawMode mode)
        {
            if (currentDrawMode == mode)
                return;

            currentDrawMode = mode;

            if (IsDrawModeOverridingMaterial(mode))
            {
                if (!OverrideMaterial())
                    return;
            }
            else
                RestoreMaterial();
        }

        private bool IsDrawModeOverridingMaterial(DrawMode mode)
        {
            return (mode == DrawMode.asleepStatus ||
                    mode == DrawMode.activeArea ||
                    mode == DrawMode.globalOccupancy ||
                    mode == DrawMode.interactorOccupancy ||
                    mode == DrawMode.offset ||
                    mode == DrawMode.interactionData);
        }

        public void ResetColorGrid()
        {
            for (int i = 0; i < _colors.Length; i++)
            {
                _colors[i].r = 0;
                _colors[i].g = 0;
                _colors[i].b = 0;
            }
        }

        private bool OverrideMaterial()
        {
            if (_materialIsOverriden)
                return true;

            Shader shader = Shader.Find("WaveMaker/WaveMakerDebugShader");
            if (shader == null)
            {
                Utils.LogError("Can't find WaveMaker Debug Shader in the resources folder", gameObject);
                return false;
            }
            _materialBackup = _meshRenderer.material;
            _colorsBackup = _meshManager.GetMeshColorsCopy();

            _material = new Material(shader);
            _meshRenderer.sharedMaterial = _material;

            _materialIsOverriden = true;
            return true;
        }

        private void RestoreMaterial()
        {
            if (!_materialIsOverriden)
                return;

            if (_materialBackup != null)
                _meshRenderer.sharedMaterial = _materialBackup;

            _meshManager.SetMeshColors(ref _colorsBackup);
            _colorsBackup = null;
            _materialBackup = null;
            _materialIsOverriden = false;
        }

        public void DisruptSurface()
        {
            if (!enabled)
                return;

            for (int i = 0; i < 10; i++)
            {
                var index = UnityEngine.Random.Range(0, _surface._fixed.Length);
                _surface?.SetHeightOffset(index, 1);
            }
        }

        #endregion

        #region Draw Methods

        private void ShowGlobalOccupancy()
        {
            float depth = _surface.detectionDepth;
            var occupancy = _surface.Occupancy;
            for (int i = 0; i < _colors.Length; i++)
            {
                float occ = occupancy[i];

                if (occ > depth || occ < 0)
                {
                    _colors[i] = Color.red;
                    continue;
                }

                if (occupancyNormalizedByDepth)
                    occ /= depth;

                _colors[i] = new Color(occ, occ, occ);
            }

            _surface.MeshManager.SetMeshColors(ref _colors);
        }

        private void ShowInteractorOccupancy()
        {
            ResetColorGrid();
            var data = _surface._interactionData;
            float depth = _surface.detectionDepth;
            var d = new InteractionData();

            // Draw only interation data for the selected interactor
            for (int i = 0; i < _surface.nMaxCellsPerInteractor; i++)
            {
                InteractionDataArray.GetData(in data,  _surface.nMaxCellsPerInteractor, interactorSelected, i, ref d);
                if (d.IsNull)
                    break;

                var occ = data[i].occupancy;

                if (_colors[d.cellIndex] == Color.red || occ < 0 || occ > depth)
                {
                    _colors[d.cellIndex] = Color.red;
                    continue;
                }

                if (occupancyNormalizedByDepth)
                    occ /= depth;

                Color col = new Color(_colors[d.cellIndex].r + occ, _colors[d.cellIndex].g + occ, _colors[d.cellIndex].b + occ);

                if (col.r > 1 || col.g > 1 || col.b > 1)
                    col = Color.blue;

                _colors[d.cellIndex] = col;
            }
            _surface.MeshManager.SetMeshColors(ref _colors);
        }

        private void ShowAsleepStatus()
        {
            var res = _surface._resolution;
            var gRes = _surface._resolutionGhost;

            var counter = _surface._asleepCounterLimit - _surface._asleepCounter;
            var normalization = Mathf.Clamp01((float)counter / _surface._asleepCounterLimit);
            
            if (_colors[0].r != normalization)
            {
                for (int i = 0; i < _colors.Length; i++)
                {
                    _colors[i].r = normalization;
                    _colors[i].g = normalization;
                    _colors[i].b = normalization;
                }

                _surface.MeshManager.SetMeshColors(ref _colors);
            }

        }

        private void ShowActiveArea()
        {
            ResetColorGrid();

            _surface.CalculateMinimumSharedAreaOfInteractors(out IntegerPair areaResolution, out int xOffset, out int zOffset);

            if (areaResolution.x > 0 && areaResolution.z > 0)
            {
                int sampleX, sampleZ;

                for (int i = 0; i < _colors.Length; i++)
                {
                    Utils.FromIndexToSampleIndices(i, _surface._resolution, out sampleX, out sampleZ);

                    bool insideActiveArea = sampleX >= xOffset &&
                                            sampleZ >= zOffset &&
                                            sampleX < xOffset + areaResolution.x &&
                                            sampleZ < zOffset + areaResolution.z;

                    _colors[i].r = insideActiveArea ? 1 : 0;
                    _colors[i].g = insideActiveArea ? 1 : 0;
                    _colors[i].b = insideActiveArea ? 1 : 0;
                }
            }

            _surface.MeshManager.SetMeshColors(ref _colors);
        }

        private void ShowInteractionData()
        {
            ResetColorGrid();

            var data = _surface._interactionData;
            InteractionData d = new InteractionData();

            // Draw only interaction data for the selected interactor
            for (int i = 0; i < _surface.nMaxCellsPerInteractor; i++)
            {
                InteractionDataArray.GetData(in data, _surface.nMaxCellsPerInteractor, interactorSelected, i, ref d);
                if (d.IsNull)
                    break;

                _colors[d.cellIndex].r += 1;
                _colors[d.cellIndex].g += 1;
                _colors[d.cellIndex].b += 1;

                Vector3 pos_ws = Utils.GetLocalPositionFromSample(d.cellIndex, _surface._resolution, _surface._sampleSize_ls);
                pos_ws.y = -_surface.detectionDepth;
                pos_ws = _surface._l2wTransformMatrix.MultiplyPoint3x4(pos_ws);
                Vector3 dir_ws = _surface._l2wTransformMatrix.MultiplyVector(Vector3.up * d.distance);

                Debug.DrawRay(pos_ws, dir_ws, Color.green);
            }

            _surface.MeshManager.SetMeshColors(ref _colors);
        }

        private void DrawDetectionDepth()
        {
            Utils.DrawDetectionDepth(_surface);
        }


        // TODO: Obsolete
        /*
        private void DrawUpwardRays(in RaycastCommandPairs rays)
        {
            if (_surface._interactorsDetected.Count <= 0)
                return;

            foreach (var ray in rays.GetUpwardRays())
            {
                Vector3 from = ray.from;
                if (ray.distance < _surface.detectionDepth)
                    from += (ray.distance - _surface.detectionDepth) * ray.direction;

                Debug.DrawRay(from, ray.direction * _surface.detectionDepth, Color.red);
            }
        }

        private void DrawDownwardRays(in RaycastCommandPairs rays)
        {
            if (_surface._interactorsDetected.Count <= 0)
                return;

            foreach (var ray in rays.GetDownwardRays())
            {
                Vector3 from = ray.from;
                if (ray.distance < _surface.detectionDepth)
                    from += (ray.distance - _surface.detectionDepth) * ray.direction;

                Debug.DrawRay(from, ray.direction * _surface.detectionDepth, Color.red);
            }
        }

        private void DrawUpwardRaysHitting(in RaycastCommandPairs rays, in RaycastCommandPairsResults results)
        {
            if (_surface._interactorsDetected.Count <= 0)
                return;

            int i = 0;
            foreach (var ray in rays.GetUpwardRays())
            {
                // Restore original from destroyed by RaycastCommandMultihit
                Vector3 from = ray.from;
                if (ray.distance < _surface.detectionDepth)
                    from += (ray.distance - _surface.detectionDepth) * ray.direction;
                Vector3 pos = from;

                int j = 0; float prevDistance = 0;
                foreach (var hit in results.GetUpwardResults(i))
                {
                    Debug.DrawRay(pos, ray.direction * (hit.distance - prevDistance), rayHitColors[j % _surface.nMaxInteractorsPerCell]);
                    prevDistance = hit.distance;
                    pos = from + ray.direction * hit.distance;
                    j++;
                }

                i++;
            }
        }

        private void DrawDownwardRaysHitting(in RaycastCommandPairs rays, in RaycastCommandPairsResults results)
        {
            if (_surface._interactorsDetected.Count <= 0)
                return;

            int i = 0;
            foreach (var ray in rays.GetDownwardRays())
            {
                // Restore original from destroyed by RaycastCommandMultihit
                Vector3 from = ray.from;
                if (ray.distance < _surface.detectionDepth)
                    from += (ray.distance - _surface.detectionDepth) * ray.direction;
                Vector3 pos = from;

                int j = 0; float prevDistance = 0;
                foreach (var hit in results.GetDownwardResults(i))
                {
                    Debug.DrawRay(pos, ray.direction * (hit.distance - prevDistance), rayHitColors[j % _surface.nMaxInteractorsPerCell]);
                    prevDistance = hit.distance;
                    pos = from + ray.direction * hit.distance;
                    j++;
                }

                i++;
            }
        }
        */

        public void PrintInteractors()
        {
            if (!enabled)
                return;

            foreach (var interactor in _surface._interactorsDetected)
            {
                if (!_surface._colliderIdsToIndices.TryGetValue(interactor.NativeCollider.instanceId, out int index))
                    Utils.Log(string.Format("Interactor {0} NOT Stored in the indices list of this surface. Error!.", interactor.gameObject), gameObject);
                else
                    Utils.Log(string.Format("Interactor {0}. Instance ID: {1}. Index: {2}", interactor.gameObject, interactor.NativeCollider.instanceId, index), gameObject);
            }
        }

        private void DrawGrid()
        {
            var botLeft = _surface.GetPositionFromSample(0, true, false, true);
            var botRight = _surface.GetPositionFromSample(_surface._resolutionGhost.x - 1, true, false, true);
            int topIndex = Utils.FromSampleIndicesToIndex(_surface._resolutionGhost, 0, _surface._resolutionGhost.z - 1);
            var topLeft = _surface.GetPositionFromSample(topIndex, true, false, true);

            var dirX = (botRight - botLeft).normalized;
            var dirZ = (topLeft - botLeft).normalized;

            var sampleSizeX = _surface.transform.localScale.x * _surface._sampleSize_ls.x;
            var sampleSizeZ = _surface.transform.localScale.z * _surface._sampleSize_ls.y;

            // For each row
            Vector3 posA = botLeft;
            Vector3 posB = botRight;
            for (int z = 0; z < _surface._resolutionGhost.z; z++)
            {
                Debug.DrawLine(posA, posB, Color.red);
                posA += dirZ * sampleSizeZ;
                posB += dirZ * sampleSizeZ;
            }

            // For each col
            posA = botLeft;
            posB = topLeft;
            for (int x = 0; x < _surface._resolutionGhost.x; x++)
            {
                Debug.DrawLine(posA, posB, Color.red);
                posA += dirX * sampleSizeX;
                posB += dirX * sampleSizeX;
            }

        }

        private void DrawOffset()
        {
            ResetColorGrid();

            var heights = _surface._heights;
            var res = _surface._resolution;
            var gRes = _surface._resolutionGhost;

            for (int i = 0; i < _colors.Length; i++)
            {
                int gI = Utils.FromNoGhostIndexToGhostIndex(i, in res, in gRes);
                var offset = heights[gI];

                if (offset > offsetClamp)
                    offset = offsetClamp;

                if (offset < -offsetClamp)
                    offset = -offsetClamp;

                var normalized = offset / offsetClamp;

                _colors[i].r = offset < 0 ? -normalized : 0;
                _colors[i].g = 0;
                _colors[i].b = offset > 0 ? normalized : 0;
            }

            _surface.MeshManager.SetMeshColors(ref _colors);
        }

        private void DrawGradients()
        {
            var gradients = _surface._gradients;
            Matrix4x4 transformMat = _surface._l2wTransformMatrix;
            var resolution = _surface._resolution;
            var sampleSize = _surface._sampleSize_ls;

            for (int i = 0; i < gradients.Length; i++)
            {
                var currentPos = Utils.GetLocalPositionFromSample(i, resolution, sampleSize);
                currentPos.y = _surface.GetHeight(i);

                var gradient = gradients[i];
                gradient.y = 0;

                Debug.DrawRay(transformMat.MultiplyPoint3x4(currentPos),
                              transformMat.MultiplyVector(gradient) * rayVisualScale,
                              Color.red);
            }
        }

        private void DrawVectorsGrid(ref NativeArray<Vector3> array, bool offsetByHeight)
        {
            Matrix4x4 transformMat = _surface._l2wTransformMatrix;
            var resolution = _surface._resolution;
            var sampleSize = _surface._sampleSize_ls;

            for (int i = 0; i < array.Length; i++)
            {
                var currentPos = Utils.GetLocalPositionFromSample(i, resolution, sampleSize);

                if (offsetByHeight)
                    currentPos.y = _surface.GetHeight(i);

                Debug.DrawRay(transformMat.MultiplyPoint3x4(currentPos),
                              transformMat.MultiplyVector(array[i]) * rayVisualScale,
                              Color.red);
            }
        }

        private void DrawVerticalVectorsGrid(ref NativeArray<float> array)
        {
            Matrix4x4 transformMat = _surface._l2wTransformMatrix;
            var resolution = _surface._resolution;
            var sampleSize = _surface._sampleSize_ls;

            for (int i = 0; i < array.Length; i++)
            {
                var currentPos = Utils.GetLocalPositionFromSample(i, resolution, sampleSize);
                currentPos.y = _surface.GetHeight(i);

                Vector3 vect = Vector3.zero;
                vect.y = array[i];

                Debug.DrawRay(transformMat.MultiplyPoint3x4(currentPos),
                              transformMat.MultiplyVector(vect) * rayVisualScale,
                              Color.red);
            }
        }

        private void DrawVectorsGrid(ref NativeArray<float4> array, bool offsetByHeight)
        {
            Matrix4x4 transformMat = _surface._l2wTransformMatrix;
            var resolution = _surface._resolution;
            var sampleSize = _surface._sampleSize_ls;

            for (int i = 0; i < array.Length; i++)
            {
                var currentPos = Utils.GetLocalPositionFromSample(i, resolution, sampleSize);

                if (offsetByHeight)
                    currentPos.y = _surface.GetHeight(i);

                Debug.DrawRay(transformMat.MultiplyPoint3x4(currentPos),
                              transformMat.MultiplyVector(array[i].xyz * rayVisualScale),
                              Color.red);
            }
        }

        #endregion
#endif
    }
}
