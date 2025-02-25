/*
Copyright (c) 2021 Omar Duarte
Unauthorized copying of this file, via any medium is strictly prohibited.
Writen by Omar Duarte, 2021.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
using System.Linq;
using UnityEngine;

namespace PluginMaster
{
    #region DATA & SETTINGS
    [System.Serializable]
    public class TerrainFlatteningSettings
    {
        [SerializeField] private float _hardness = 0f;
        [SerializeField] private float _padding = 0f;
        [SerializeField] private bool _clearTrees = true;
        [SerializeField] private bool _clearDetails = true;
        private Vector2 _coreSize = Vector2.one;
        private Vector2 _density = Vector2.zero;
        private float _angle = 0;
        private bool _updateHeightmap = true;
        private float[,] _heightmap = null;

        public TerrainFlatteningSettings() { }

        public float hardness
        {
            get => _hardness;
            set
            {
                if (_hardness == value) return;
                _hardness = value;
                _updateHeightmap = true;
                PWBCore.SetSavePending();
            }
        }
        public float padding
        {
            get => _padding;
            set
            {
                value = Mathf.Max(value, 0);
                if (_padding == value) return;
                _padding = value;
                _updateHeightmap = true;
                PWBCore.SetSavePending();
            }
        }
        public bool clearTrees
        {
            get => _clearTrees;
            set
            {
                if (_clearTrees == value) return;
                _clearTrees = value;
                PWBCore.SetSavePending();
            }
        }
        public bool clearDetails
        {
            get => _clearDetails;
            set
            {
                if (_clearDetails == value) return;
                _clearDetails = value;
                PWBCore.SetSavePending();
            }
        }
        public Vector2 size
        {
            get => _coreSize;
            set
            {
                if (_coreSize == value) return;
                _coreSize = value;
                _updateHeightmap = true;
            }
        }
        public Vector2 density
        {
            set
            {
                if (_density == value) return;
                _density = value;
                _updateHeightmap = true;
            }
        }
        public float angle
        {
            get => _angle;
            set
            {
                if (_angle == value) return;
                _angle = value;
                _updateHeightmap = true;
            }
        }
        public float[,] heightmap
        {
            get
            {
                if (_updateHeightmap || _heightmap == null) UpdateHeightmap();
                return _heightmap;
            }
        }

        private void UpdateHeightmap()
        {
            _updateHeightmap = false;
            var cornerSize = (_coreSize.x + _coreSize.y) / 2 * (1 - _hardness);
            if (_hardness < 0.8) cornerSize = Mathf.Max(cornerSize, 10f / Mathf.Min(_density.x, _density.y));
            var coreWithPaddingSize = _coreSize + Vector2.one * _padding * 2;
            var size = coreWithPaddingSize + Vector2.one * (cornerSize * 2);
            var unrotatedSize = new Vector2Int(Mathf.RoundToInt(size.x * _density.x),
                Mathf.RoundToInt(size.y * _density.y));

            var coreMapSize = new Vector2Int(Mathf.RoundToInt(coreWithPaddingSize.x * _density.x),
                Mathf.RoundToInt(coreWithPaddingSize.y * _density.y));
            var cornerMapSize = new Vector2Int(Mathf.RoundToInt(_density.x * cornerSize),
                Mathf.RoundToInt(_density.y * cornerSize));
            var unrotatedMap = new float[unrotatedSize.x, unrotatedSize.y];

            var minCore = new Vector2Int(Mathf.Max(cornerMapSize.x - 1, 0), Mathf.Max(cornerMapSize.y - 1, 0));
            var maxCoreI = Mathf.Min(coreMapSize.y + cornerMapSize.y + 1, unrotatedSize.y);
            var maxCoreJ = Mathf.Min(coreMapSize.x + cornerMapSize.x + 1, unrotatedSize.x);
            for (int i = minCore.y; i < maxCoreI; ++i)
                for (int j = minCore.x; j < maxCoreJ; ++j)
                    unrotatedMap[j, i] = 1f;

            float ParametricBlend(float t)
            {
                if (t > 1) return 1;
                if (t < 0) return 0;
                float tSquared = t * t;
                return tSquared / (2.0f * (tSquared - t) + 1.0f);
            }


            for (int i = 0; i < cornerMapSize.x; ++i)
            {
                var i1 = unrotatedSize.x - 1 - i;
                var iDistance = (cornerMapSize.x - i) / _density.x;
                for (int j = 0; j < cornerMapSize.y; ++j)
                {
                    var j1 = unrotatedSize.y - 1 - j;
                    var jDistance = (cornerMapSize.y - j) / _density.y;
                    var distance = 1 - Mathf.Sqrt(iDistance * iDistance + jDistance * jDistance) / cornerSize;
                    var h = ParametricBlend(distance);
                    unrotatedMap[i, j] = unrotatedMap[i1, j] = unrotatedMap[i, j1] = unrotatedMap[i1, j1] = h;

                }
                var distanceNorm = 1 - (float)iDistance / cornerSize;
                var iH = ParametricBlend(distanceNorm);
                for (int j = minCore.y; j < maxCoreI; ++j) unrotatedMap[i, j] = unrotatedMap[i1, j] = iH;
            }
            for (int j = 0; j < cornerMapSize.y; ++j)
            {
                var j1 = unrotatedSize.y - 1 - j;
                var jDistance = (cornerMapSize.y - j) / _density.y;
                var distanceNorm = 1 - (float)jDistance / cornerSize;
                var jH = ParametricBlend(distanceNorm);
                for (int i = minCore.x; i < maxCoreJ; ++i) unrotatedMap[i, j] = unrotatedMap[i, j1] = jH;
            }

            if (_angle == 0)
            {
                _heightmap = unrotatedMap;
                return;
            }

            var angleRad = _angle * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angleRad);
            var sin = Mathf.Sin(angleRad);
            var aspect = _density.x / _density.y;
            Vector2Int RotatePoint(Vector2 centerToPoint)
            {
                if (_angle == 0) return new Vector2Int(Mathf.RoundToInt(centerToPoint.x), Mathf.RoundToInt(centerToPoint.y));
                var result = Vector2Int.zero;
                centerToPoint.y = centerToPoint.y * aspect;
                result.x = Mathf.RoundToInt((centerToPoint.x * cos - centerToPoint.y * sin));
                result.y = Mathf.RoundToInt((centerToPoint.x * sin + centerToPoint.y * cos) / aspect);
                return result;
            }
            var centerToCorner1 = new Vector2Int(Mathf.CeilToInt(unrotatedSize.x / 2f), Mathf.CeilToInt(unrotatedSize.y / 2f));
            var rotatedCorner1 = RotatePoint(centerToCorner1);
            rotatedCorner1 = new Vector2Int(Mathf.Abs(rotatedCorner1.x), Mathf.Abs(rotatedCorner1.y));
            var centerToCorner2 = new Vector2Int(-Mathf.CeilToInt(unrotatedSize.x / 2f),
                Mathf.CeilToInt(unrotatedSize.y / 2f));
            var rotatedCorner2 = RotatePoint(centerToCorner2);
            rotatedCorner2 = new Vector2Int(Mathf.Abs(rotatedCorner2.x), Mathf.Abs(rotatedCorner2.y));
            var rotatedCorner = Vector2Int.Max(rotatedCorner1, rotatedCorner2);

            var rotationPadding = Vector2Int.Max(rotatedCorner - centerToCorner1, Vector2Int.zero);

            var rotatedHeightmapSize = unrotatedSize + rotationPadding * 2;
            _heightmap = new float[rotatedHeightmapSize.x, rotatedHeightmapSize.y];


            Vector2Int ClampPoint(Vector2Int point) => new Vector2Int(Mathf.Clamp(point.x, 0, rotatedHeightmapSize.x - 1),
                    Mathf.Clamp(point.y, 0, rotatedHeightmapSize.y - 1));

            void SetHeight(Vector2Int point, float value)
            {
                var clampPoint = ClampPoint(point);
                _heightmap[clampPoint.x, clampPoint.y] = value;
                var points = new Vector2Int[] { ClampPoint(point + Vector2Int.up), ClampPoint(point + Vector2Int.down),
                    ClampPoint(point + Vector2Int.left), ClampPoint(point + Vector2Int.right)};
                foreach (var p in points)
                    _heightmap[p.x, p.y] = _heightmap[p.x, p.y] < 0.0001 ? value : (_heightmap[p.x, p.y] * 6 + value) / 7;
            }

            var unrotatedCenter = new Vector2Int(Mathf.FloorToInt(unrotatedSize.x / 2f),
                Mathf.FloorToInt(unrotatedSize.y / 2f));
            var center = new Vector2Int(Mathf.FloorToInt(rotatedHeightmapSize.x / 2f),
                Mathf.FloorToInt(rotatedHeightmapSize.y / 2f));
            for (int i = 0; i < unrotatedSize.y; ++i)
            {
                for (int j = 0; j < unrotatedSize.x; ++j)
                {
                    var h = unrotatedMap[j, i];
                    var point = new Vector2(j, i);
                    var centerToPoint = point - unrotatedCenter;
                    var rotatedPoint = RotatePoint(centerToPoint) + center;
                    SetHeight(rotatedPoint, h);
                }
            }

            var smoothMap = new float[rotatedHeightmapSize.x, rotatedHeightmapSize.y];
            for (int i = 0; i < rotatedHeightmapSize.x; ++i)
            {
                for (int j = 0; j < rotatedHeightmapSize.y; ++j)
                {
                    var count = 0;
                    var sum = 0f;
                    var corners = new float[] { i == 0 || j == 0 ? 0 : _heightmap[i-1, j-1],
                        i == rotatedHeightmapSize.x-1 || j == 0? 0 :_heightmap[i+1, j -1],
                        i == 0 || j == rotatedHeightmapSize.y-1 ? 0 :_heightmap[i-1, j+1],
                        i == rotatedHeightmapSize.x-1 || j == rotatedHeightmapSize.y-1 ? 0 : _heightmap[i+1, j+1] };
                    for (int n = 0; n < 4; ++n)
                    {
                        if (corners[n] < 0.0001) continue;
                        ++count;
                        sum += corners[n];
                    }
                    var neighbors = new float[] { i == 0 ? 0 : _heightmap[i - 1, j],
                        i == rotatedHeightmapSize.x -1 ? 0 :_heightmap[i + 1, j],
                        j == 0 ? 0 :_heightmap[i, j - 1], j == rotatedHeightmapSize.y -1 ? 0 : _heightmap[i, j + 1] };
                    for (int n = 0; n < 4; ++n)
                    {
                        if (neighbors[n] < 0.0001) continue;
                        count += 2;
                        sum += neighbors[n] * 2;
                    }
                    if (count == 0)
                    {
                        smoothMap[i, j] = _heightmap[i, j];
                        continue;
                    }
                    if (!(_heightmap[i, j] < 0.0001 && ((neighbors[0] > 0.0001 && neighbors[1] > 0.0001)
                        || (neighbors[2] > 0.0001 && neighbors[3] > 0.0001))))
                    {
                        sum += _heightmap[i, j] * 3;
                        count += 3;
                    }
                    var avg = sum / count;
                    smoothMap[i, j] = avg;
                }
            }
            _heightmap = smoothMap;
        }
    }

    [System.Serializable]
    public class PinSettings : PaintOnSurfaceToolSettings, IPaintToolSettings
    {
        [SerializeField] private bool _repeat = false;
        [SerializeField] private TerrainFlatteningSettings _flatteningSettings = new TerrainFlatteningSettings();
        [SerializeField] private bool _flattenTerrain = false;
        [SerializeField] private bool _avoidOverlapping = false;
        [SerializeField] private bool _snapRotationToGrid = false;
        public bool repeat
        {
            get => _repeat;
            set
            {
                if (_repeat == value) return;
                _repeat = value;
                OnDataChanged();
            }
        }
        public TerrainFlatteningSettings flatteningSettings => _flatteningSettings;
        public bool flattenTerrain
        {
            get => _flattenTerrain;
            set
            {
                if (_flattenTerrain == value) return;
                _flattenTerrain = value;
                PWBCore.SetSavePending();
            }
        }

        public bool avoidOverlapping
        {
            get => _avoidOverlapping;
            set
            {
                if (_avoidOverlapping == value) return;
                _avoidOverlapping = value;
                OnDataChanged();
            }
        }

        public bool snapRotationToGrid
        {
            get => _snapRotationToGrid;
            set
            {
                if (_snapRotationToGrid == value) return;
                _snapRotationToGrid = value;
                OnDataChanged();
            }
        }
        #region PAINT TOOL
        [SerializeField] private PaintToolSettings _paintTool = new PaintToolSettings();
        public Transform parent { get => _paintTool.parent; set => _paintTool.parent = value; }
        public bool overwritePrefabLayer
        {
            get => _paintTool.overwritePrefabLayer;
            set => _paintTool.overwritePrefabLayer = value;
        }
        public int layer { get => _paintTool.layer; set => _paintTool.layer = value; }
        public bool autoCreateParent { get => _paintTool.autoCreateParent; set => _paintTool.autoCreateParent = value; }
        public bool setSurfaceAsParent { get => _paintTool.setSurfaceAsParent; set => _paintTool.setSurfaceAsParent = value; }
        public bool createSubparentPerPalette
        {
            get => _paintTool.createSubparentPerPalette;
            set => _paintTool.createSubparentPerPalette = value;
        }
        public bool createSubparentPerTool
        {
            get => _paintTool.createSubparentPerTool;
            set => _paintTool.createSubparentPerTool = value;
        }
        public bool createSubparentPerBrush
        {
            get => _paintTool.createSubparentPerBrush;
            set => _paintTool.createSubparentPerBrush = value;
        }
        public bool createSubparentPerPrefab
        {
            get => _paintTool.createSubparentPerPrefab;
            set => _paintTool.createSubparentPerPrefab = value;
        }
        public bool overwriteBrushProperties
        {
            get => _paintTool.overwriteBrushProperties;
            set => _paintTool.overwriteBrushProperties = value;
        }
        public BrushSettings brushSettings => _paintTool.brushSettings;

        public PinSettings() : base() => _paintTool.OnDataChanged += DataChanged;
        #endregion

        public override void Copy(IToolSettings other)
        {
            var otherPinSettings = other as PinSettings;
            if (otherPinSettings == null) return;
            base.Copy(other);
            _paintTool.Copy(otherPinSettings._paintTool);
            _repeat = otherPinSettings._repeat;
            _flattenTerrain = otherPinSettings._flattenTerrain;
            _snapRotationToGrid = otherPinSettings._snapRotationToGrid;
        }
        public override void DataChanged()
        {
            base.DataChanged();
            BrushstrokeManager.UpdateBrushstroke();
        }
    }

    [System.Serializable]
    public class PinManager : ToolManagerBase<PinSettings>
    {
        private static float _rotationSnapValueStatic = 5f;
        [SerializeField] private float _rotationSnapValue = _rotationSnapValueStatic;

        public static float rotationSnapValue
        {
            get => _rotationSnapValueStatic;
            set
            {
                if (_rotationSnapValueStatic == value) return;
                _rotationSnapValueStatic = value;
                PWBCore.staticData.SaveAndUpdateVersion();
            }
        }

        public override void OnBeforeSerialize()
        {
            base.OnBeforeSerialize();
            _rotationSnapValue = _rotationSnapValueStatic;
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            _rotationSnapValueStatic = _rotationSnapValue;
        }
    }
    #endregion

    #region PWBIO
    public static partial class PWBIO
    {
        private static bool _pinned = false;
        private static Vector3 _pinMouse = Vector3.zero;
        private static RaycastHit _pinHit = new RaycastHit();
        private static Vector3 _pinAngle = Vector3.zero;
        private static Vector3 _previousPinAngle = Vector3.zero;
        private static float _pinScale = 1f;
        private static Vector3 _pinOffset = Vector3.zero;
        private static System.Collections.Generic.List<System.Collections.Generic.List<Vector3>> _initialPinBoundPoints
            = new System.Collections.Generic.List<System.Collections.Generic.List<Vector3>>();
        private static System.Collections.Generic.List<System.Collections.Generic.List<Vector3>> _pinBoundPoints
            = new System.Collections.Generic.List<System.Collections.Generic.List<Vector3>>();
        private static int _pinBoundPointIdx = 0;
        private static int _pinBoundLayerIdx = 0;
        private static bool _snapToVertex = false;
        private static float _pinDistanceFromSurface = 0f;

        private static Vector2 _flatteningSize = Vector2.zero;
        private static Vector3 _flatteningCenter = Vector3.zero;

        private static Vector3 _pinProjectionDirection = Vector3.down;

        private static bool _pinFlipX = false;
        private static void UpdatePinScale()
        {
            for (int l = 0; l < _pinBoundPoints.Count; ++l)
                for (int p = 0; p < _pinBoundPoints[l].Count; ++p)
                    _pinBoundPoints[l][p] = _initialPinBoundPoints[l][p] * _pinScale;
            _pinOffset = _pinBoundPoints[_pinBoundLayerIdx][_pinBoundPointIdx];
        }
        private static void UpdatePinScale(float value)
        {
            if (_pinScale == value) return;
            _pinScale = value;
            UpdatePinScale();
            UnityEditor.SceneView.RepaintAll();
        }
        private static Vector3 pivotBoundPoint
        {
            get
            {
                _pinBoundPointIdx = 0;
                return _pinBoundPoints[_pinBoundLayerIdx][_pinBoundPointIdx];
            }
        }
        private static Vector3 nextBoundPoint
        {
            get
            {
                ++_pinBoundPointIdx;
                if (_pinBoundPointIdx >= _pinBoundPoints[_pinBoundLayerIdx].Count) _pinBoundPointIdx = 0;
                return _pinBoundPoints[_pinBoundLayerIdx][_pinBoundPointIdx];
            }
        }

        private static Vector3 prevBoundPoint
        {
            get
            {
                --_pinBoundPointIdx;
                if (_pinBoundPointIdx < 0) _pinBoundPointIdx = _pinBoundPoints[_pinBoundLayerIdx].Count - 1;
                return _pinBoundPoints[_pinBoundLayerIdx][_pinBoundPointIdx];
            }
        }

        private static Vector3 nextBoundLayer
        {
            get
            {
                ++_pinBoundLayerIdx;
                if (_pinBoundLayerIdx >= _pinBoundPoints.Count) _pinBoundLayerIdx = 0;
                return _pinBoundPoints[_pinBoundLayerIdx][_pinBoundPointIdx];
            }
        }

        private static Vector3 prevBoundLayer
        {
            get
            {
                --_pinBoundLayerIdx;
                if (_pinBoundLayerIdx < 0) _pinBoundLayerIdx = _pinBoundPoints.Count - 1;

                return _pinBoundPoints[_pinBoundLayerIdx][_pinBoundPointIdx];
            }
        }

        private static void SetPinValues(Quaternion additionRotation)
        {
            var strokeItem = BrushstrokeManager.brushstroke[0];
            var prefab = strokeItem.settings.prefab;
            if (prefab == null) return;
            var isSprite = prefab.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.enabled && r.sprite != null && r.gameObject.activeSelf).ToArray().Length > 0;

            var bounds = BoundsUtils.GetBoundsRecursive(prefab.transform, prefab.transform.rotation);

            _pinBoundPoints.Clear();
            _initialPinBoundPoints.Clear();

            var centerToPivot = prefab.transform.position - bounds.center;


            var pointRotation = additionRotation;

            var xProjection = Vector3.Project(_pinHit.normal, additionRotation * Vector3.right);
            var yProjection = Vector3.Project(_pinHit.normal, additionRotation * Vector3.up);
            var zProjection = Vector3.Project(_pinHit.normal, additionRotation * Vector3.forward);

            var xProjectionMagnitude = xProjection.magnitude;
            var yProjectionMagnitude = yProjection.magnitude;
            var zProjectionMagnitude = zProjection.magnitude;

            var nearestAxisToSurfaceNormal = AxesUtils.Axis.Y;

            var maxProjectionMagnitude = yProjectionMagnitude;
            if (xProjectionMagnitude > maxProjectionMagnitude)
            {
                nearestAxisToSurfaceNormal = AxesUtils.Axis.X;
                maxProjectionMagnitude = xProjectionMagnitude;
            }
            if (zProjectionMagnitude > maxProjectionMagnitude) nearestAxisToSurfaceNormal = AxesUtils.Axis.Z;
            var halfSize = bounds.size * 0.5f;

            int l = 0;
            var pointsNormalized = new Vector2[] { new Vector2(0,0),
                    new Vector2(-1,0), new Vector2(0,1), new Vector2(1,0),  new Vector2(0,-1),
                    new Vector2(-1,-1), new Vector2(-1,1), new Vector2(1,1), new Vector2(1,-1)};

            if (nearestAxisToSurfaceNormal == AxesUtils.Axis.Y)
            {
                var sign = 1;
                if (!strokeItem.settings.rotateToTheSurface) sign = Vector3.Dot(Vector3.up, yProjection) > 0 ? 1 : -1;
                _pinProjectionDirection = additionRotation * (Vector3.down * sign);
                for (int y = -1; y <= 1; y += 2)
                {
                    _pinBoundPoints.Add(new System.Collections.Generic.List<Vector3>());
                    _initialPinBoundPoints.Add(new System.Collections.Generic.List<Vector3>());
                    _pinBoundPoints[l].Add(Vector3.zero);
                    _initialPinBoundPoints[l].Add(Vector3.zero);
                    foreach (var n in pointsNormalized)
                    {
                        var point = isSprite ? new Vector3(n.x, n.y, -y) : new Vector3(n.x, -y * sign, n.y);
                        point = Vector3.Scale(point, halfSize) + centerToPivot;
                        point = pointRotation * point;
                        _pinBoundPoints[l].Add(point);
                        _initialPinBoundPoints[l].Add(point);
                    }
                    ++l;
                }
            }
            else if (nearestAxisToSurfaceNormal == AxesUtils.Axis.X)
            {
                var sign = 1;
                if (!strokeItem.settings.rotateToTheSurface) sign = Vector3.Dot(Vector3.right, xProjection) > 0 ? 1 : -1;
                _pinProjectionDirection = additionRotation * (Vector3.left * sign);
                for (int x = -1; x <= 1; x += 2)
                {
                    _pinBoundPoints.Add(new System.Collections.Generic.List<Vector3>());
                    _initialPinBoundPoints.Add(new System.Collections.Generic.List<Vector3>());
                    _pinBoundPoints[l].Add(Vector3.zero);
                    _initialPinBoundPoints[l].Add(Vector3.zero);

                    foreach (var n in pointsNormalized)
                    {
                        var point = isSprite ? new Vector3(n.x, n.y, -x) : new Vector3(-x * sign, n.y, n.x);
                        point = Vector3.Scale(point, halfSize) + centerToPivot;
                        point = pointRotation * point;
                        _pinBoundPoints[l].Add(point);
                        _initialPinBoundPoints[l].Add(point);
                    }
                    ++l;
                }
            }
            else if (nearestAxisToSurfaceNormal == AxesUtils.Axis.Z)
            {
                var sign = 1;
                if (!strokeItem.settings.rotateToTheSurface) sign = Vector3.Dot(Vector3.forward, zProjection) > 0 ? 1 : -1;
                _pinProjectionDirection = additionRotation * (Vector3.back * sign);
                for (int z = -1; z <= 1; z += 2)
                {
                    _pinBoundPoints.Add(new System.Collections.Generic.List<Vector3>());
                    _initialPinBoundPoints.Add(new System.Collections.Generic.List<Vector3>());
                    _pinBoundPoints[l].Add(Vector3.zero);
                    _initialPinBoundPoints[l].Add(Vector3.zero);
                    foreach (var n in pointsNormalized)
                    {
                        var point = isSprite ? new Vector3(n.x, n.y, -z) : new Vector3(n.x, n.y, -z * sign);
                        point = Vector3.Scale(point, halfSize) + centerToPivot;
                        point = pointRotation * point;
                        _pinBoundPoints[l].Add(point);
                        _initialPinBoundPoints[l].Add(point);
                    }
                    ++l;
                }
            }
        }
        public static void ResetPinValues()
        {
            _pinned = false;
            _pinMouse = Vector3.zero;
            _pinHit = new RaycastHit();
            _pinAngle = Vector3.zero;
            _pinScale = 1f;
            _pinDistanceFromSurface = 0f;
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var strokeItem = BrushstrokeManager.brushstroke[0];
            SetPinValues(Quaternion.Euler(strokeItem.additionalAngle));
            BrushSettings brushSettings = strokeItem.settings;
            if (PinManager.settings.overwriteBrushProperties) brushSettings = PinManager.settings.brushSettings;
            repaint = true;
            _pinOffset = _pinBoundPoints[_pinBoundLayerIdx][_pinBoundPointIdx];
            UnityEditor.SceneView.RepaintAll();
        }
        public static void UpdatePinValues(GameObject prefab, Quaternion rotation)
        {
            if (prefab == null) return;
            var isSprite = prefab.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.enabled && r.sprite != null && r.gameObject.activeSelf).ToArray().Length > 0;
            var additionalRotation = rotation;
            float RoundToStraightAngle(float angle) => Mathf.Round(angle / 90f) * 90f;
            var up = additionalRotation * Vector3.up;
            var fromUpToNormalRotation = Quaternion.FromToRotation(up, _pinHit.normal);
            Vector3 RoundEulerToStraightAngles(Vector3 euler)
                => new Vector3(RoundToStraightAngle(euler.x), RoundToStraightAngle(euler.y), RoundToStraightAngle(euler.z));
            var fromUpToNormalEulerRounded = RoundEulerToStraightAngles(fromUpToNormalRotation.eulerAngles);
            fromUpToNormalRotation = Quaternion.Euler(fromUpToNormalEulerRounded);
            SetPinValues(additionalRotation);
            var layerIdx = Mathf.Clamp(_pinBoundLayerIdx, 0, _pinBoundPoints.Count - 1);
            var pointIdx = Mathf.Clamp(_pinBoundPointIdx, 0, _pinBoundPoints[layerIdx].Count - 1);
            UpdatePinScale();
            repaint = true;
            UnityEditor.SceneView.RepaintAll();
        }

        private static Transform _pinSurface = null;
        private static void PinDuringSceneGUI(UnityEditor.SceneView sceneView)
        {
            if (PinManager.settings.paintOnMeshesWithoutCollider)
                PWBCore.CreateTempCollidersWithinFrustum(sceneView.camera);

            PinInput(sceneView);
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
            var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            bool snappedToVertex = false;
            var closestVertexInfo = new RaycastHit();
            var settings = PinManager.settings;
            if (_snapToVertex)
                snappedToVertex = SnapToVertex(mouseRay, out closestVertexInfo, sceneView.in2DMode);
            if (snappedToVertex)
                DrawPin(sceneView, closestVertexInfo, false);
            else
            {
                if (settings.mode == PinSettings.PaintMode.ON_SHAPE)
                {
                    if (GridRaycast(mouseRay, out RaycastHit planeHit))
                        DrawPin(sceneView, planeHit, SnapManager.settings.snappingEnabled);
                    else _paintStroke.Clear();
                }
                else
                {
                    if (MouseRaycast(mouseRay, out RaycastHit mouseHit, out GameObject collider, float.MaxValue,
                        -1, settings.paintOnPalettePrefabs, settings.paintOnMeshesWithoutCollider))
                    {
                        DrawPin(sceneView, mouseHit, SnapManager.settings.snappingEnabled);
                        _pinSurface = collider.transform;
                    }
                    else if (_pinned) DrawPin(sceneView, _pinHit, SnapManager.settings.snappingEnabled);
                    else if (settings.mode == PinSettings.PaintMode.AUTO)
                    {
                        if (GridRaycast(mouseRay, out RaycastHit planeHit))
                            DrawPin(sceneView, planeHit, SnapManager.settings.snappingEnabled);
                    }
                    else _paintStroke.Clear();
                }
            }
        }

        private static Vector3 _prevPinHitNormal = Vector3.zero;
        private static void DrawPin(UnityEditor.SceneView sceneView, RaycastHit hit,
            bool snapToGrid)
        {
            if (PaletteManager.selectedBrush == null) return;
            PWBCore.UpdateTempCollidersIfHierarchyChanged();
            if (!_pinned)
            {
                hit.point = SnapAndUpdateGridOrigin(hit.point, snapToGrid,
                   PinManager.settings.paintOnPalettePrefabs, PinManager.settings.paintOnMeshesWithoutCollider,
                   PinManager.settings.mode == PaintOnSurfaceToolSettings.PaintMode.ON_SHAPE, -hit.normal);
                _pinHit = hit;
            }
            PinPreview(sceneView.camera);
        }

        private static bool snapPinRotationToGrid = false;
        private static Vector3 GetPinAngleSnappedToGrid(Vector3 position, Quaternion rotation)
        {
            var gridRotation = Quaternion.identity;
            if (SnapManager.settings.radialGridEnabled)
            {
                var gridLocalNormal = (Vector3)(AxesUtils.SignedAxis)SnapManager.settings.gridAxis;
                var gridNormal = SnapManager.settings.rotation * gridLocalNormal;
                var posOnPlane = Vector3.ProjectOnPlane(position, gridNormal) - SnapManager.settings.origin;
                gridRotation = Quaternion.Inverse(rotation) * Quaternion.LookRotation(posOnPlane, gridNormal);
            }
            else gridRotation = Quaternion.Inverse(rotation) * SnapManager.settings.rotation;
            Vector3 GetSnappedToGrid(Vector3 v)
            {
                var xProj = Vector3.Project(v, gridRotation * Vector3.right);
                var yProj = Vector3.Project(v, gridRotation * Vector3.up);
                var zProj = Vector3.Project(v, gridRotation * Vector3.forward);
                var xMag = xProj.magnitude;
                var yMag = yProj.magnitude;
                var zMag = zProj.magnitude;
                if (xMag >= yMag && xMag >= zMag) return xProj;
                else if (yMag >= xMag && yMag >= zMag) return yProj;
                else return zProj;
            }
            var pinRotation = Quaternion.Euler(_pinAngle);
            var snappedUp = GetSnappedToGrid(pinRotation * Vector3.up);
            var snappedFw = GetSnappedToGrid(pinRotation * Vector3.forward);
            return Quaternion.LookRotation(snappedFw, snappedUp).eulerAngles;
        }
        private static void PinPreview(Camera camera)
        {
            _paintStroke.Clear();
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var strokeItem = BrushstrokeManager.brushstroke[0].Clone();
            var prefab = strokeItem.settings.prefab;
            if (prefab == null) return;
            BrushSettings brushSettings = strokeItem.settings;
            if (PinManager.settings.overwriteBrushProperties) brushSettings = PinManager.settings.brushSettings;


            var itemRotation = Quaternion.identity;
            var itemPosition = _pinHit.point;
            if (brushSettings.rotateToTheSurface && !PinManager.settings.flattenTerrain)
            {
                if (_pinHit.normal == Vector3.zero) _pinHit.normal = Vector3.up;
                var itemTangent = Vector3.Cross(_pinHit.normal, Vector3.left);
                if (itemTangent.sqrMagnitude < 0.000001) itemTangent = Vector3.Cross(_pinHit.normal, Vector3.back);
                itemTangent = itemTangent.normalized;
                if (_pinHit.collider == null)
                    itemRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                else
                {
                    itemRotation = Quaternion.LookRotation(itemTangent, _pinHit.normal);
                    if (strokeItem.settings.isAsset2D) itemRotation *= Quaternion.Euler(90, 0, 0);
                }
            }

            if (_pinHit.collider != null)
            {
                var obj = _pinHit.collider.gameObject;
                var hitParent = _pinHit.collider.transform.parent;
                if (hitParent != null && hitParent.gameObject.GetInstanceID() == PWBCore.parentColliderId)
                    obj = PWBCore.GetGameObjectFromTempColliderId(obj.GetInstanceID());
            }
            GameObject objUnderMouse = null;
            if (_pinHit.collider != null)
            {
                var parentUnderMouse = _pinHit.collider.transform.parent;
                if (parentUnderMouse != null
                    && parentUnderMouse.gameObject.GetInstanceID() == PWBCore.parentColliderId)
                    objUnderMouse = PWBCore.GetGameObjectFromTempColliderId(
                        _pinHit.collider.gameObject.GetInstanceID());
                else objUnderMouse = _pinHit.collider.gameObject;
            }

            if (PinManager.settings.paintOnSelectedOnly && objUnderMouse != null
                && !SelectionManager.selection.Contains(objUnderMouse)) return;
            itemRotation *= Quaternion.Euler(strokeItem.additionalAngle);
            
            var pinAngle = _pinAngle;
            if (PinManager.settings.snapRotationToGrid || snapPinRotationToGrid)
            {
                pinAngle = GetPinAngleSnappedToGrid(itemPosition, itemRotation);
                if (snapPinRotationToGrid)
                {
                    _pinAngle = pinAngle;
                    snapPinRotationToGrid = false;
                }
            }
            itemRotation *= Quaternion.Euler(pinAngle);
            
            if (brushSettings.rotateToTheSurface && brushSettings.alwaysOrientUp && !strokeItem.settings.isAsset2D)
            {
                var fw = (Quaternion.Euler(strokeItem.additionalAngle) * Quaternion.Euler(_pinAngle)) * _pinHit.normal;
                fw.y = 0;
                const float minMag = 1e-6f;
                if (Mathf.Abs(fw.x) > minMag || Mathf.Abs(fw.z) > minMag)
                    itemRotation = Quaternion.LookRotation(fw, Vector3.up);
            }
            itemPosition += itemRotation * brushSettings.localPositionOffset;

            var scaleMult = strokeItem.scaleMultiplier * _pinScale;
            var itemScale = Vector3.Scale(prefab.transform.localScale, scaleMult);

            UpdatePinValues(prefab, itemRotation * prefab.transform.rotation);

            var previewPinOffset = _pinOffset / _pinScale;
            var strokePinOffset = _pinOffset;

            if (brushSettings.embedInSurface && PinManager.settings.mode != PaintOnSurfaceToolSettingsBase.PaintMode.ON_SHAPE)
            {

                if (brushSettings.embedAtPivotHeight)
                    itemPosition += _pinBoundPoints[_pinBoundLayerIdx][0] - _pinOffset;
                else
                {
                    var TRS = Matrix4x4.TRS(itemPosition + _pinOffset, itemRotation,
                        Vector3.Scale(prefab.transform.localScale, scaleMult));
                    float magnitudeInDirection;
                    var localDirection = Quaternion.Inverse(itemRotation) * _pinProjectionDirection;
                    var furthestVertices = strokeItem.settings.GetFurthestVerticesInDirection(localDirection,
                        out magnitudeInDirection);
                    var distanceTosurface = GetDistanceToSurface(furthestVertices, TRS, _pinProjectionDirection,
                        Mathf.Abs(magnitudeInDirection), PinManager.settings.paintOnPalettePrefabs,
                        PinManager.settings.paintOnMeshesWithoutCollider, out Transform surfaceTransform, prefab);
                    itemPosition += _pinProjectionDirection * distanceTosurface;
                }
            }

            itemPosition -= _pinProjectionDirection * (strokeItem.surfaceDistance + _pinDistanceFromSurface);

            var layer = PinManager.settings.overwritePrefabLayer ? PinManager.settings.layer : prefab.layer;
            Transform parentTransform = GetParent(PinManager.settings, prefab.name, false, _pinSurface);

            if (PinManager.settings.avoidOverlapping)
            {
                var itemBounds = BoundsUtils.GetBoundsRecursive(prefab.transform, Quaternion.identity);
                var pivotToCenter = itemBounds.center - prefab.transform.position;
                pivotToCenter = Vector3.Scale(pivotToCenter, scaleMult);
                pivotToCenter = itemRotation * pivotToCenter;
                var itemCenter = itemPosition + pivotToCenter;
                var itemHalfExtends = Vector3.Scale(itemBounds.size * 0.499f, strokeItem.scaleMultiplier);
                var overlaped = Physics.OverlapBox(itemCenter, itemHalfExtends,
                                itemRotation, -1, QueryTriggerInteraction.Ignore).
                                Where(c => c != _pinHit.collider && IsVisible(c.gameObject)).ToArray();
                if (overlaped.Length > 0)
                {
                    DrawPinHandles(new Color(1f, 0f, 0f, 0.7f));
                    return;
                }
            }
            var flipX = strokeItem.flipX ^ _pinFlipX;
            _paintStroke.Add(new PaintStrokeItem(prefab, itemPosition + strokePinOffset,
                itemRotation * prefab.transform.rotation,
                itemScale, layer, parentTransform, _pinSurface, flipX, strokeItem.flipY));

            var translateMatrix = Matrix4x4.Translate(Quaternion.Inverse(itemRotation) * previewPinOffset
               - prefab.transform.position);
            var rootToWorld = Matrix4x4.TRS(itemPosition, itemRotation, scaleMult) * translateMatrix;
            PreviewBrushItem(prefab, rootToWorld, layer, camera, false, false, flipX, strokeItem.flipY);

            if (!brushSettings.isAsset2D && _prevPinHitNormal != _pinHit.normal) _prevPinHitNormal = _pinHit.normal;


            DrawPinHandles(new Color(1f, 1f, 1f, 0.7f));

            _pinSurface = null;
        }
        private static void FlatenTerrain()
        {
            var terrain = _pinHit.collider.GetComponent<Terrain>();
            if (terrain == null) return;
            var terrainData = terrain.terrainData;

            terrainData.SetTerrainLayersRegisterUndo(terrainData.terrainLayers, "Paint");
            var resolution = terrainData.heightmapResolution;

            var heighMap = terrainData.GetHeights(0, 0, resolution, resolution);
            var transformScale = terrain.transform.localScale;
            terrain.transform.localScale = Vector3.one;
            var localCenter = terrain.transform.InverseTransformPoint(_flatteningCenter);
            var localHit = terrain.transform.InverseTransformPoint(_pinHit.point);
            terrain.transform.localScale = transformScale;

            var density = new Vector2(1 / terrainData.heightmapScale.x, 1 / terrainData.heightmapScale.z);
            var mapCenterX = Mathf.RoundToInt(localCenter.x * density.x);
            var mapCenterZ = Mathf.RoundToInt(localCenter.z * density.y);
            var mapHitX = Mathf.RoundToInt(localHit.x * density.x);
            var mapHitZ = Mathf.RoundToInt(localHit.z * density.y);

            var hitHmapVal = heighMap[mapHitZ, mapHitX];
            var flattenSettings = PinManager.settings.flatteningSettings;
            flattenSettings.density = density;
            flattenSettings.angle = -_pinAngle.y;

            var paintItem = _paintStroke[0];
            var itemSize = BoundsUtils.GetBoundsRecursive(paintItem.prefab.transform).size * _pinScale;
            flattenSettings.size = new Vector2(itemSize.x, itemSize.z);

            var itemHeighmap = flattenSettings.heightmap;
            var itemHeighmapH = itemHeighmap.GetLength(0);
            var itemHeighmapW = itemHeighmap.GetLength(1);
            int itemMinX = Mathf.Max(itemHeighmapH / 2 - mapCenterX, 0);
            int itemMinZ = Mathf.Max(itemHeighmapW / 2 - mapCenterZ, 0);
            int itemMaxX = itemHeighmapH;
            if (Mathf.CeilToInt(itemHeighmapH / 2) + mapCenterX > resolution)
                itemMaxX -= (Mathf.CeilToInt(itemHeighmapH / 2) + mapCenterX) - resolution + 1;
            int itemMaxZ = itemHeighmapW;
            if (Mathf.CeilToInt(itemHeighmapW / 2) + mapCenterZ > resolution)
                itemMaxZ -= (Mathf.CeilToInt(itemHeighmapW / 2) + mapCenterZ) - resolution + 1;
            int w = itemMaxZ - itemMinZ;
            int h = itemMaxX - itemMinX;
            var heights = new float[w, h];

            int terrHmapMinX = Mathf.Max(mapCenterX - itemHeighmapH / 2, 0);
            int terrHmapMinZ = Mathf.Max(mapCenterZ - itemHeighmapW / 2, 0);

            for (int x = itemMinX; x < itemMaxX; ++x)
            {
                for (int z = itemMinZ; z < itemMaxZ; ++z)
                {
                    var terrHmapI = Mathf.Clamp(mapCenterZ - Mathf.CeilToInt(itemHeighmapW / 2) + z, 0, resolution - 1);
                    var terrHmapJ = Mathf.Clamp(mapCenterX - Mathf.CeilToInt(itemHeighmapH / 2) + x, 0, resolution - 1);
                    var terrHmapVal = heighMap[terrHmapI, terrHmapJ];

                    var itemI = z - itemMinZ;
                    var itemJ = x - itemMinX;
                    var itemHmapVal = itemHeighmap[x, z];
                    heights[itemI, itemJ] = terrHmapVal * (1 - itemHmapVal) + hitHmapVal * itemHmapVal;
                }
            }

            terrainData.SetHeights(terrHmapMinX, terrHmapMinZ, heights);

            ////////////////////
            if (flattenSettings.clearDetails)
            {
                var heightToDetail = (float)terrainData.detailResolution / terrainData.heightmapResolution;
                var heightToDetailInt = Mathf.CeilToInt(heightToDetail) + 1;
                var terrainDetailLayers = new System.Collections.Generic.List<int[,]>();
                var detailLayers = new System.Collections.Generic.List<int[,]>();
                var densityInt = new Vector2Int(Mathf.CeilToInt(density.x), Mathf.CeilToInt(density.y));
                var detailsW = Mathf.CeilToInt(w * heightToDetail) + 4 * densityInt.y;
                var detailsH = Mathf.CeilToInt(h * heightToDetail) + 4 * densityInt.x;
                var terrDetailMinX = Mathf.RoundToInt((localCenter.x * density.x - itemHeighmapH / 2f) * heightToDetail)
                    - 2 * densityInt.x;
                var terrDetailMinY = Mathf.RoundToInt((localCenter.z * density.y - itemHeighmapW / 2f) * heightToDetail)
                    - 2 * densityInt.y;

                void SetDetailToZero(int layer, int i, int j)
                {
                    detailLayers[layer][i, j] = 0;
                    for (int k = 1; k <= heightToDetailInt; ++k)
                    {
                        if (i - k >= 0)
                        {
                            detailLayers[layer][i - k, j] = 0;
                            if (j - k >= 0) detailLayers[layer][i - k, j - k] = 0;
                            else if (j + k < detailsH - 1) detailLayers[layer][i - k, j + k] = 0;
                        }
                        else if (i + k < detailsW - 1)
                        {
                            detailLayers[layer][i + k, j] = 0;
                            if (j - k >= 0) detailLayers[layer][i + k, j - k] = 0;
                            else if (j + k < detailsH - 1) detailLayers[layer][i + k, j + k] = 0;
                        }
                        else
                        {
                            if (j - k >= 0) detailLayers[layer][i, j - k] = 0;
                            else if (j + k < detailsH - 1) detailLayers[layer][i, j + k] = 0;
                        }
                    }
                }

                for (int k = 0; k < terrainData.detailPrototypes.Length; ++k)
                {

                    terrainDetailLayers.Add(terrainData.GetDetailLayer(0, 0,
                        terrainData.detailWidth, terrainData.detailHeight, k));
                    detailLayers.Add(new int[detailsW, detailsH]);
                    for (int itemDetailI = 0; itemDetailI < detailsW; ++itemDetailI)
                    {
                        for (int itemDetailJ = 0; itemDetailJ < detailsH; ++itemDetailJ)
                        {
                            var terrDetailI = Mathf.Clamp(terrDetailMinY + itemDetailI, 0, terrainData.detailWidth - 1);
                            var terrDetailJ = Mathf.Clamp(terrDetailMinX + itemDetailJ, 0, terrainData.detailHeight - 1);
                            var layerValue = terrainDetailLayers[k][terrDetailI, terrDetailJ];
                            detailLayers[k][itemDetailI, itemDetailJ] = layerValue;

                            var itemHmapX = Mathf.Clamp(Mathf.RoundToInt((itemDetailJ - 2 * densityInt.y)
                                / heightToDetail), 0, itemHeighmapH - 1);
                            var itemHmapZ = Mathf.Clamp(Mathf.RoundToInt((itemDetailI - 2 * densityInt.x)
                                / heightToDetail), 0, itemHeighmapW - 1);
                            var itemHmapVal = itemHeighmap[itemHmapX, itemHmapZ];
                            if (itemHmapVal > 0.9) SetDetailToZero(k, itemDetailI, itemDetailJ);
                        }
                    }
                    terrainData.SetDetailLayer(terrDetailMinX, terrDetailMinY, k, detailLayers[k]);
                }
            }
            if (flattenSettings.clearTrees)
            {
                for (int k = 0; k < terrainData.detailPrototypes.Length; ++k)
                {
                    var treeInstances = new System.Collections.Generic.List<TreeInstance>();
                    foreach (var treeInstance in terrainData.treeInstances)
                    {
                        var hmapX = Mathf.RoundToInt(treeInstance.position.x * resolution);
                        var hmapZ = Mathf.RoundToInt(treeInstance.position.z * resolution);
                        var itemHmapX = hmapX - terrHmapMinX;
                        var itemHmapZ = hmapZ - terrHmapMinZ;
                        if (itemHmapX < 0 || itemHmapX >= itemHeighmapH || itemHmapZ < 0 || itemHmapZ >= itemHeighmapW)
                        {
                            treeInstances.Add(treeInstance);
                            continue;
                        }
                        var itemHmapVal = itemHeighmap[itemHmapX, itemHmapZ];
                        if (itemHmapVal < 0.9) treeInstances.Add(treeInstance);
                    }
                    terrainData.treeInstances = treeInstances.ToArray();
                }
            }
            //////////////////

        }

        private static void PinInput(UnityEditor.SceneView sceneView)
        {
            if (PaletteManager.selectedBrush == null) return;
            var keyCode = Event.current.keyCode;
            if (Event.current.button == 0)
            {
                if (Event.current.type == EventType.MouseUp && !Event.current.alt)
                {
                    if (PinManager.settings.flattenTerrain) FlatenTerrain();
                    Paint(PinManager.settings);
                    _pinned = false;
                    Event.current.Use();
                }
                if (Event.current.type == EventType.KeyDown)
                {
                    if (PWBSettings.shortcuts.pinMoveHandlesUp.Check()) _pinOffset = nextBoundLayer;
                    else if (PWBSettings.shortcuts.pinMoveHandlesDown.Check()) _pinOffset = prevBoundLayer;
                    else if (PWBSettings.shortcuts.pinSelectNextHandle.Check()) _pinOffset = nextBoundPoint;
                    else if (PWBSettings.shortcuts.pinSelectPrevHandle.Check()) _pinOffset = prevBoundPoint;
                    else if (PWBSettings.shortcuts.pinSelectPivotHandle.Check()) _pinOffset = pivotBoundPoint;
                    //add rotation around Y
                    else if (PWBSettings.shortcuts.pinRotate90YCW.Check()) _pinAngle.y = (_pinAngle.y + 90) % 360;
                    else if (PWBSettings.shortcuts.pinRotate90YCCW.Check()) _pinAngle.y = (_pinAngle.y - 90) % 360;
                    else if (PWBSettings.shortcuts.pinRotateAStepYCW.Check()) _pinAngle.y -= PinManager.rotationSnapValue;
                    else if (PWBSettings.shortcuts.pinRotateAStepYCCW.Check()) _pinAngle.y += PinManager.rotationSnapValue;
                    //add rotation around X
                    else if (PWBSettings.shortcuts.pinRotate90XCW.Check()) _pinAngle.x = (_pinAngle.x + 90) % 360;
                    else if (PWBSettings.shortcuts.pinRotate90XCCW.Check()) _pinAngle.x = (_pinAngle.x - 90) % 360;
                    else if (PWBSettings.shortcuts.pinRotateAStepXCW.Check()) _pinAngle.x -= PinManager.rotationSnapValue;
                    else if (PWBSettings.shortcuts.pinRotateAStepXCCW.Check()) _pinAngle.x += PinManager.rotationSnapValue;
                    //add rotation around Z
                    else if (PWBSettings.shortcuts.pinRotate90ZCW.Check()) _pinAngle.z = (_pinAngle.z + 90) % 360;
                    else if (PWBSettings.shortcuts.pinRotate90ZCCW.Check()) _pinAngle.z = (_pinAngle.z - 90) % 360;
                    else if (PWBSettings.shortcuts.pinRotateAStepZCW.Check()) _pinAngle.z -= PinManager.rotationSnapValue;
                    else if (PWBSettings.shortcuts.pinRotateAStepZCCW.Check()) _pinAngle.z += PinManager.rotationSnapValue;
                    //reset rotation
                    else if (PWBSettings.shortcuts.pinResetRotation.Check()) _pinAngle = Vector3.zero;
                    else if (PWBSettings.shortcuts.pinSnapRotationToGrid.Check())
                    {
                        snapPinRotationToGrid = true;
                        sceneView.Repaint();
                        repaint = true;
                    }
                    //distance to surface
                    else if (PWBSettings.shortcuts.pinSubtract1UnitFromSurfDist.Check()) _pinDistanceFromSurface -= 1f;
                    else if (PWBSettings.shortcuts.pinAdd1UnitToSurfDist.Check()) _pinDistanceFromSurface += 1f;
                    else if (PWBSettings.shortcuts.pinSubtract01UnitFromSurfDist.Check()) _pinDistanceFromSurface -= 0.1f;
                    else if (PWBSettings.shortcuts.pinAdd01UnitToSurfDist.Check()) _pinDistanceFromSurface += 0.1f;
                    else if (PWBSettings.shortcuts.pinResetSurfDist.Check()) _pinDistanceFromSurface = 0;
                    else if (PWBSettings.shortcuts.pinResetScale.Check()) UpdatePinScale(1f);
                    //Flip
                    else if (PWBSettings.shortcuts.pinFlipX.Check()) _pinFlipX = !_pinFlipX;

                    else if (PWBSettings.shortcuts.pinToggleRepeatItem.Check())
                    {
                        PinManager.settings.repeat = !PinManager.settings.repeat;
                        ToolProperties.RepainWindow();
                    }
                    else if (PWBSettings.shortcuts.pinSelectPreviousItem.Check())
                    {
                        BrushstrokeManager.SetNextPinBrushstroke(-1);
                        sceneView.Repaint();
                        repaint = true;
                    }
                    else if (PWBSettings.shortcuts.pinSelectNextItem.Check())
                    {
                        BrushstrokeManager.SetNextPinBrushstroke(1);
                        sceneView.Repaint();
                        repaint = true;
                    }
                }
            }
            else
            {
                if (Event.current.type == EventType.MouseDown && Event.current.control)
                {
                    _pinned = true;
                    _pinMouse = Event.current.mousePosition;
                    _previousPinAngle = _pinAngle;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp && !Event.current.control) _pinned = false;
            }
            const float DEG_PER_PIXEL = 1.8f; //180deg/100px

            if (PWBSettings.shortcuts.pinSelectNextItemScroll.Check())
            {
                var scrollSign = Mathf.Sign(Event.current.delta.y);
                Event.current.Use();
                BrushstrokeManager.SetNextPinBrushstroke((int)scrollSign);
                sceneView.Repaint();
                repaint = true;
            }
            else if (PWBSettings.shortcuts.pinRotateAroundY.Check())
            {
                var combi = PWBSettings.shortcuts.pinRotateAroundY.combination;
                if (combi.mouseEvent == PWBMouseCombination.MouseEvents.SCROLL_WHEEL) _pinAngle.y += combi.delta;
                else if (combi.isMouseDragEvent) _pinAngle.y -= combi.delta * DEG_PER_PIXEL;
                _previousPinAngle = _pinAngle;
            }
            else if (PWBSettings.shortcuts.pinRotateAroundYSnaped.Check())
            {
                var combi = PWBSettings.shortcuts.pinRotateAroundYSnaped.combination;
                if (combi.mouseEvent == PWBMouseCombination.MouseEvents.SCROLL_WHEEL)
                {
                    var scrollSign = Mathf.Sign(Event.current.delta.y);
                    _pinAngle.y += scrollSign * PinManager.rotationSnapValue;
                }
                else if (combi.isMouseDragEvent)
                {
                    _pinAngle.y = _previousPinAngle.y - combi.delta * DEG_PER_PIXEL;
                    _previousPinAngle.y = _pinAngle.y;
                    if (PinManager.rotationSnapValue > 0)
                        _pinAngle.y = Mathf.Round(_pinAngle.y / PinManager.rotationSnapValue) * PinManager.rotationSnapValue;
                }
            }
            else if (PWBSettings.shortcuts.pinRotateAroundX.Check())
            {
                var combi = PWBSettings.shortcuts.pinRotateAroundX.combination;
                if (combi.mouseEvent == PWBMouseCombination.MouseEvents.SCROLL_WHEEL) _pinAngle.x += Event.current.delta.y;
                else if (combi.isMouseDragEvent) _pinAngle.x -= combi.delta * DEG_PER_PIXEL;
                _previousPinAngle = _pinAngle;
            }
            else if (PWBSettings.shortcuts.pinRotateAroundXSnaped.Check())
            {
                var combi = PWBSettings.shortcuts.pinRotateAroundXSnaped.combination;
                if (combi.mouseEvent == PWBMouseCombination.MouseEvents.SCROLL_WHEEL)
                {
                    var scrollSign = Mathf.Sign(Event.current.delta.y);
                    _pinAngle.x += scrollSign * PinManager.rotationSnapValue;
                }
                else if (combi.isMouseDragEvent)
                {
                    _pinAngle.x = _previousPinAngle.x + combi.delta * DEG_PER_PIXEL;
                    _previousPinAngle.x = _pinAngle.x;
                    if (PinManager.rotationSnapValue > 0)
                        _pinAngle.x = Mathf.Round(_pinAngle.x / PinManager.rotationSnapValue) * PinManager.rotationSnapValue;
                }
            }
            else if (PWBSettings.shortcuts.pinRotateAroundZ.Check())
            {
                var combi = PWBSettings.shortcuts.pinRotateAroundZ.combination;
                if (combi.mouseEvent == PWBMouseCombination.MouseEvents.SCROLL_WHEEL) _pinAngle.z += Event.current.delta.y;
                else if (combi.isMouseDragEvent) _pinAngle.z -= combi.delta * DEG_PER_PIXEL;
                _previousPinAngle = _pinAngle;
            }
            else if (PWBSettings.shortcuts.pinRotateAroundZSnaped.Check())
            {
                var combi = PWBSettings.shortcuts.pinRotateAroundZSnaped.combination;
                if (combi.mouseEvent == PWBMouseCombination.MouseEvents.SCROLL_WHEEL)
                {
                    var scrollSign = Mathf.Sign(Event.current.delta.y);
                    _pinAngle.z += scrollSign * PinManager.rotationSnapValue;
                }
                else if (combi.isMouseDragEvent)
                {
                    _pinAngle.z = _previousPinAngle.z + combi.delta * DEG_PER_PIXEL;
                    _previousPinAngle.z = _pinAngle.z;
                    if (PinManager.rotationSnapValue > 0)
                        _pinAngle.z = Mathf.Round(_pinAngle.z / PinManager.rotationSnapValue) * PinManager.rotationSnapValue;
                }
            }
            else if (PWBSettings.shortcuts.pinSurfDist.Check())
            {
                var combi = PWBSettings.shortcuts.pinSurfDist.combination;
                if (combi.mouseEvent == PWBMouseCombination.MouseEvents.SCROLL_WHEEL)
                    _pinDistanceFromSurface += Event.current.delta.y * 0.04f;
                else if (combi.isMouseDragEvent) _pinDistanceFromSurface += combi.delta * 0.04f;
            }
            else if (PWBSettings.shortcuts.pinScale.Check())
            {

                if (PWBSettings.shortcuts.pinScale.combination.mouseEvent == PWBMouseCombination.MouseEvents.SCROLL_WHEEL)
                {
                    var scrollSign = Mathf.Sign(Event.current.delta.y);
                    UpdatePinScale(Mathf.Max(_pinScale * (1f + scrollSign * 0.05f), 0.01f));
                    sceneView.Repaint();
                    repaint = true;
                }
                else if (PWBSettings.shortcuts.pinScale.combination.isMouseDragEvent)
                {
                    UpdatePinScale(Mathf.Max(_pinScale * (1f + PWBSettings.shortcuts.pinScale.combination.delta * 0.003f),
                        0.01f));
                    sceneView.Repaint();
                    repaint = true;
                }
            }

            if ((keyCode == KeyCode.LeftControl || keyCode == KeyCode.RightControl)
                && Event.current.type == EventType.KeyUp) _pinned = false;
        }

        private static void DrawPinHandles(Color color)
        {
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var strokeItem = BrushstrokeManager.brushstroke[0];
            var prefab = strokeItem.settings.prefab;
            if (prefab == null) return;
            var pos = Vector3.zero;
            var prevPos = Vector3.zero;
            var pos0 = Vector3.zero;
            var handlePoints = new System.Collections.Generic.List<Vector3>();
            UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            if (_pinBoundPoints.Count == 0) ResetPinValues();
            var flatteningPoints = new System.Collections.Generic.List<Vector3>();
            var layerIdx = Mathf.Clamp(_pinBoundLayerIdx, 0, _pinBoundPoints.Count - 1);
            var pivotPos = Vector3.zero;
            for (int i = 0; i < _pinBoundPoints[layerIdx].Count; ++i)
            {
                prevPos = pos;
                pos = _pinOffset - _pinBoundPoints[layerIdx][i] + _pinHit.point;
                if (i > _pinBoundPoints[layerIdx].Count - 5)
                {
                    if (i == _pinBoundPoints[layerIdx].Count - 4) pos0 = pos;
                    else if (i < _pinBoundPoints[layerIdx].Count)
                    {
                        UnityEditor.Handles.color = new Color(0f, 0f, 0f, 0.7f);
                        UnityEditor.Handles.DrawAAPolyLine(6, new Vector3[] { prevPos, pos });
                        UnityEditor.Handles.color = color;
                        UnityEditor.Handles.DrawAAPolyLine(2, new Vector3[] { prevPos, pos });
                    }
                }
                flatteningPoints.Add(pos);
                if (i == 0) pivotPos = pos;
                if (_pinBoundPointIdx == i) continue;
                handlePoints.Add(pos);
            }
            UnityEditor.Handles.color = new Color(0f, 0f, 0f, 0.7f);
            UnityEditor.Handles.DrawAAPolyLine(6, new Vector3[] { pos, pos0 });
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawAAPolyLine(2, new Vector3[] { pos, pos0 });

            if (PinManager.settings.flattenTerrain && _pinHit.collider != null
                && _pinHit.collider.GetComponent<Terrain>() != null)
            {
                Vector3 p0, p1, p2, p3;
                var n = flatteningPoints.Count;


                var side1_2 = flatteningPoints[n - 3] - flatteningPoints[n - 4];
                var side2_3 = flatteningPoints[n - 2] - flatteningPoints[n - 3];
                var dir1_2 = side1_2.normalized;
                var dir2_3 = side2_3.normalized;
                p0 = flatteningPoints[n - 4] + (-dir1_2 - dir2_3) * PinManager.settings.flatteningSettings.padding;
                p1 = flatteningPoints[n - 3] + (dir1_2 - dir2_3) * PinManager.settings.flatteningSettings.padding;
                p2 = flatteningPoints[n - 2] + (dir1_2 + dir2_3) * PinManager.settings.flatteningSettings.padding;
                p3 = flatteningPoints[n - 1] + (-dir1_2 + dir2_3) * PinManager.settings.flatteningSettings.padding;

                p0.y = p1.y = p2.y = p3.y = _pinHit.point.y;
                _flatteningCenter = (p2 - p0) / 2 + p0;

                UnityEditor.Handles.color = new Color(0.5f, 0f, 1f, 0.7f);
                UnityEditor.Handles.DrawAAPolyLine(6, new Vector3[] { p0, p1, p2, p3, p0 });
                UnityEditor.Handles.color = new Color(0f, 0.5f, 1f, 0.7f);
                UnityEditor.Handles.DrawAAPolyLine(2, new Vector3[] { p0, p1, p2, p3, p0 });
            }

            foreach (var handlePoint in handlePoints)
            {
                UnityEditor.Handles.color = new Color(0f, 0f, 0f, 0.7f);
                UnityEditor.Handles.DotHandleCap(795, handlePoint, Quaternion.identity,
                    UnityEditor.HandleUtility.GetHandleSize(pos) * 0.0325f * PWBCore.staticData.controPointSize,
                    EventType.Repaint);
                UnityEditor.Handles.color = UnityEditor.Handles.preselectionColor;
                UnityEditor.Handles.DotHandleCap(795, handlePoint, Quaternion.identity,
                    UnityEditor.HandleUtility.GetHandleSize(pos) * 0.02f * PWBCore.staticData.controPointSize,
                    EventType.Repaint);
            }

            var pinHitPoint = _pinHit.point;
            UnityEditor.Handles.color = new Color(0f, 0f, 0f, 0.7f);
            UnityEditor.Handles.DotHandleCap(418, pinHitPoint, Quaternion.identity,
                UnityEditor.HandleUtility.GetHandleSize(pinHitPoint) * 0.0425f * PWBCore.staticData.controPointSize,
                EventType.Repaint);
            if (pinHitPoint != pivotPos)
            {
                UnityEditor.Handles.color = UnityEditor.Handles.selectedColor;
                UnityEditor.Handles.DotHandleCap(418, pinHitPoint, Quaternion.identity,
                    UnityEditor.HandleUtility.GetHandleSize(pinHitPoint) * 0.03f * PWBCore.staticData.controPointSize,
                    EventType.Repaint);
            }
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.DotHandleCap(418, pivotPos, Quaternion.identity,
                UnityEditor.HandleUtility.GetHandleSize(pivotPos) * (pinHitPoint == pivotPos ? 0.03f : 0.02f)
                * PWBCore.staticData.controPointSize, EventType.Repaint);
        }
    }
    #endregion
}
