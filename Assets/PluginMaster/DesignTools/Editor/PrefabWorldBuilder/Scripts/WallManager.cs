/*
Copyright(c) 2021 Omar Duarte
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
using UnityEngine;

namespace PluginMaster
{
    #region DATA & SETTINGS
    [System.Serializable]
    public class WallSettings : ModularToolBase
    {
        [SerializeField] private bool _autoCalculateAxes = true;
        public bool autoCalculateAxes
        {
            get => _autoCalculateAxes;
            set
            {
                if(_autoCalculateAxes == value) return;
                _autoCalculateAxes = value;
                OnDataChanged();
            }
        }

        public override void Copy(IToolSettings other)
        {
            base.Copy(other);
            var otherWallSettings = other as WallSettings;
            if (otherWallSettings == null) return;
            _autoCalculateAxes = otherWallSettings.autoCalculateAxes;
        }
    }
    [System.Serializable]
    public class WallManager : ToolManagerBase<WallSettings>
    {
        public enum ToolState
        {
            FIRST_WALL_PREVIEW,
            EDITING
        }
        public static ToolState state { get; set; } = ToolState.FIRST_WALL_PREVIEW;
        public static float wallThickness { get; set; } = 0.1f;
        public static float wallLength { get; set; } = 1f;
        public static AxesUtils.Axis wallLenghtAxis { get; set; } = AxesUtils.Axis.X;
        public static Vector3 firstPoint { get; set; } = Vector3.zero;
        public static Vector3 secondPoint { get; set; } = Vector3.zero;
    }
    #endregion
    public static partial class PWBIO
    {
        #region HANDLERS
        private static void WallInitializeOnLoad()
        {
            WallManager.settings.OnDataChanged += OnWallSettingsChanged;
            BrushSettings.OnBrushSettingsChanged += UpdateWallSettingsOnBrushChanged;
        }

        private static void SetSnapStepToWallCellSize()
        {
            var cellSize = WallManager.settings.moduleSize + WallManager.settings.spacing;
            if (WallManager.settings.autoCalculateAxes)
            {
                WallManager.settings.SetUpwardAxis(AxesUtils.SignedAxis.UP);
                if (cellSize.x >= cellSize.z)
                {
                    WallManager.wallLenghtAxis = AxesUtils.Axis.X;
                    WallManager.settings.SetForwardAxis(AxesUtils.SignedAxis.FORWARD);
                }
                else
                {
                    WallManager.wallLenghtAxis = AxesUtils.Axis.Z;
                    WallManager.settings.SetForwardAxis(AxesUtils.SignedAxis.RIGHT);
                }
                WallManager.wallThickness = Mathf.Min(cellSize.x, cellSize.z);
                WallManager.wallLength = Mathf.Max(cellSize.x, cellSize.z);
            }
            else
            {
                WallManager.wallLenghtAxis = AxesUtils.Axis.X;
                WallManager.wallThickness = cellSize.z;
                WallManager.wallLength = cellSize.x;
            }
            cellSize.x = cellSize.z = WallManager.wallLength;
            SnapManager.settings.step = cellSize;
            UnityEditor.SceneView.RepaintAll();
        }

        private static void OnWallSettingsChanged()
        {
            repaint = true;
            BrushstrokeManager.UpdateWallBrushstroke(WallManager.wallLenghtAxis, cellsCount: 1);
            SetSnapStepToWallCellSize();
        }

        public static void UpdateWallSettingsOnBrushChanged()
        {
            if (ToolManager.tool != ToolManager.PaintTool.WALL) return;
            WallManager.settings.UpdateCellSize();
            SetSnapStepToWallCellSize();
            WallManager.state = WallManager.ToolState.FIRST_WALL_PREVIEW;
        }
        #endregion

        public static void OnWallEnabled()
        {
            SnapManager.settings.radialGridEnabled = false;
            SnapManager.settings.gridOnY = true;
            SnapManager.settings.visibleGrid = true;
            SnapManager.settings.lockedGrid = true;
            SnapManager.settings.snappingOnX = true;
            SnapManager.settings.snappingOnZ = true;
            SnapManager.settings.snappingEnabled = true;
            UpdateWallSettingsOnBrushChanged();
            SnapManager.settings.DataChanged(repaint: true);
            WallManager.state = WallManager.ToolState.FIRST_WALL_PREVIEW;
        }

        private static Vector3 _wallSecondCorner = Vector3.zero;
        private static void WallToolDuringSceneGUI(UnityEditor.SceneView sceneView)
        {
            var mousePos2D = Event.current.mousePosition;
            var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePos2D);
            var mousePos3D = Vector3.zero;
            AxesUtils.Axis axis;
            int cellsCount = 1;
            bool rotateHalfTurn;
            if (GridRaycast(mouseRay, out RaycastHit gridHit))
            {
                mousePos3D = WallManager.state == WallManager.ToolState.FIRST_WALL_PREVIEW
                    ? SnapWallPosition(gridHit.point, out axis, out rotateHalfTurn)
                    : SnapWallPosition(WallManager.firstPoint, gridHit.point, out axis, out cellsCount, out rotateHalfTurn);
            }
            else return;
            if (Event.current.control && Event.current.type == EventType.KeyDown) _modularDeleteMode = true;
            else if (_modularDeleteMode && !Event.current.control && Event.current.type == EventType.KeyUp)
            {
                _modularDeleteMode = false;
                WallManager.state = WallManager.ToolState.FIRST_WALL_PREVIEW;
                return;
            }
            if (PaletteManager.selectedBrush == null) return;
            if (Event.current.button == 0)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    WallManager.state = WallManager.ToolState.EDITING;
                    WallManager.secondPoint = WallManager.firstPoint = mousePos3D;
                    BrushstrokeManager.UpdateWallBrushstroke(axis, cellsCount: 1, _modularDeleteMode);
                }
                if (WallManager.state == WallManager.ToolState.EDITING)
                {
                    if (Event.current.type == EventType.MouseDrag)
                    {
                        WallManager.secondPoint = mousePos3D;
                        BrushstrokeManager.UpdateWallBrushstroke(axis, cellsCount, _modularDeleteMode);
                    }
                    else if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseMove)
                    {
                        WallManager.secondPoint = mousePos3D;
                        if (_modularDeleteMode)
                        {
                            BrushstrokeManager.UpdateWallBrushstroke(axis, cellsCount, _modularDeleteMode);
                            PreviewDeleteWall(sceneView.camera, axis, mousePos3D);
                            DeleteWall();
                        }
                        else Paint(WallManager.settings);
                        WallManager.state = WallManager.ToolState.FIRST_WALL_PREVIEW;
                        BrushstrokeManager.UpdateWallBrushstroke(axis, cellsCount: 1);
                    }
                }
                _wallSecondCorner = WallManager.secondPoint;
            }
            if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape)
            {
                WallManager.state = WallManager.ToolState.FIRST_WALL_PREVIEW;
                BrushstrokeManager.UpdateWallBrushstroke(axis, cellsCount: 1);
            }

            switch (WallManager.state)
            {
                case WallManager.ToolState.FIRST_WALL_PREVIEW:
                    if (_modularDeleteMode) PreviewDeleteSingleWall(sceneView.camera, axis, mousePos3D);
                    else PreviewFirstWall(sceneView.camera, mousePos3D, axis, rotateHalfTurn);
                    break;
                case WallManager.ToolState.EDITING:
                    if (_modularDeleteMode)
                    {
                        if (cellsCount == 1) PreviewDeleteSingleWall(sceneView.camera, axis, mousePos3D);
                        else PreviewDeleteWall(sceneView.camera, axis, mousePos3D);
                    }
                    else
                    {
                        if (cellsCount == 1) PreviewFirstWall(sceneView.camera, mousePos3D, axis, rotateHalfTurn);
                        else PreviewWall(sceneView.camera, axis, rotateHalfTurn);
                    }
                    break;
            }
        }

        private static void PreviewFirstWall(Camera camera, Vector3 mousePos3D,
            AxesUtils.Axis axis, bool rotateHalfTurn)
        {
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var strokeItem = BrushstrokeManager.brushstroke[0].Clone();
            if (strokeItem.settings == null)
            {
                BrushstrokeManager.UpdateWallBrushstroke(axis, cellsCount: 1);
                return;
            }
            var prefab = strokeItem.settings.prefab;
            if (prefab == null) return;
            var toolSettings = WallManager.settings;
            var itemRotation = Quaternion.Euler(strokeItem.additionalAngle);
            itemRotation *= Quaternion.Inverse(prefab.transform.rotation);
            if (axis != WallManager.wallLenghtAxis) itemRotation *= Quaternion.AngleAxis(90, toolSettings.upwardAxis);
            if(rotateHalfTurn) itemRotation *= Quaternion.AngleAxis(180, toolSettings.upwardAxis);
            var scaleMult = strokeItem.scaleMultiplier;
            var cellCenter = mousePos3D;
            var centerToPivot = GetCenterToPivot(prefab, scaleMult, itemRotation);
            var itemPosition = cellCenter + centerToPivot;
            var translateMatrix = Matrix4x4.Translate(Quaternion.Inverse(itemRotation) * -prefab.transform.position);
            var rootToWorld = Matrix4x4.TRS(itemPosition, itemRotation, scaleMult) * translateMatrix;
            var layer = toolSettings.overwritePrefabLayer ? toolSettings.layer : prefab.layer;

            PreviewBrushItem(prefab, rootToWorld, layer, camera,
                redMaterial: false, reverseTriangles: false, flipX: false, flipY: false);
            _paintStroke.Clear();
            _previewData.Add(new PreviewData(prefab, rootToWorld, layer, flipX: false, flipY: false));
            var itemScale = Vector3.Scale(prefab.transform.localScale, scaleMult);
            Transform parentTransform = toolSettings.parent;
            var paintItem = new PaintStrokeItem(prefab, itemPosition, itemRotation,
                itemScale, layer, parentTransform, surface: null, flipX: false, flipY: false);
            _paintStroke.Add(paintItem);
        }

        private static void PreviewWall(Camera camera, AxesUtils.Axis axis, bool rotateHalfTurn)
        {
            BrushstrokeItem[] brushstroke = null;
            if (PreviewIfBrushtrokestaysTheSame(out brushstroke, camera, forceUpdate: _paintStroke.Count == 0)) return;
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            _paintStroke.Clear();
            var toolSettings = WallManager.settings;
            var halfCellSize = toolSettings.moduleSize / 2;
            for (int i = 0; i < brushstroke.Length; ++i)
            {
                var strokeItem = brushstroke[i];
                if (strokeItem.settings == null) return;
                var prefab = strokeItem.settings.prefab;
                if (prefab == null) return;
                var scaleMult = strokeItem.scaleMultiplier;
                var itemRotation = Quaternion.Euler(strokeItem.additionalAngle);
                if (rotateHalfTurn) itemRotation *= Quaternion.AngleAxis(180, toolSettings.upwardAxis);

                var cellCenter = strokeItem.tangentPosition;
                var centerToPivot = GetCenterToPivot(prefab, scaleMult, itemRotation);
                var itemPosition = cellCenter + centerToPivot;

                var nearbyObjects = new System.Collections.Generic.List<GameObject>();
                boundsOctree.GetColliding(cellCenter, halfCellSize, SnapManager.settings.rotation,
                    itemRotation, nearbyObjects);
                if (nearbyObjects.Count > 0)
                {
                    bool checkNextItem = false;
                    foreach (var obj in nearbyObjects)
                    {
                        if(obj == null) continue;
                        if (!obj.activeInHierarchy) continue;
                        if (obj.transform.position != itemPosition) continue;
                        if (PaletteManager.selectedBrush.ContainsSceneObject(obj))
                        {
                            checkNextItem = true;
                            break;
                        }
                    }
                    if (checkNextItem) continue;
                }

                var translateMatrix = Matrix4x4.Translate(Quaternion.Inverse(itemRotation) * -prefab.transform.position);
                var rootToWorld = Matrix4x4.TRS(itemPosition, itemRotation, scaleMult) * translateMatrix;
                var layer = toolSettings.overwritePrefabLayer ? toolSettings.layer : prefab.layer;
                PreviewBrushItem(prefab, rootToWorld, layer, camera,
                    redMaterial: false, reverseTriangles: false, flipX: false, flipY: false);
                _previewData.Add(new PreviewData(prefab, rootToWorld, layer, flipX: false, flipY: false));
                var itemScale = Vector3.Scale(prefab.transform.localScale, scaleMult);
                Transform parentTransform = toolSettings.parent;
                var paintItem = new PaintStrokeItem(prefab, itemPosition, itemRotation,
                    itemScale, layer, parentTransform, surface: null, flipX: false, flipY: false);
                _paintStroke.Add(paintItem);
            }
        }

        private static void PreviewDeleteSingleWall(Camera camera, AxesUtils.Axis axis, Vector3 position)
        {
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var strokeItem = BrushstrokeManager.brushstroke[0].Clone();
            if (strokeItem.settings == null)
            {
                BrushstrokeManager.UpdateWallBrushstroke(axis, cellsCount: 1, deleteMode: true);
                return;
            }
            var itemRotation = Quaternion.Euler(strokeItem.additionalAngle);
            if(axis == AxesUtils.Axis.Z) itemRotation *= Quaternion.Euler(0f, 90f, 0f);
            var TRS = Matrix4x4.TRS(position, itemRotation, WallManager.settings.moduleSize);
            Graphics.DrawMesh(cubeMesh, TRS, transparentRedMaterial2, 0, camera);
        }
        private static System.Collections.Generic.HashSet<Pose> _wallDeleteStroke
            = new System.Collections.Generic.HashSet<Pose>();
        private static void PreviewDeleteWall(Camera camera, AxesUtils.Axis axis, Vector3 position)
        {
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var brushstroke = BrushstrokeManager.brushstroke;
            var toolSettings = WallManager.settings;
            _wallDeleteStroke.Clear();
            for (int i = 0; i < brushstroke.Length; ++i)
            {
                var strokeItem = brushstroke[i];
                var itemPosition = strokeItem.tangentPosition;
                var itemRotation = Quaternion.Euler(strokeItem.additionalAngle);
                var rootToWorld = Matrix4x4.TRS(itemPosition, itemRotation, WallManager.settings.moduleSize);
                Graphics.DrawMesh(cubeMesh, rootToWorld, transparentRedMaterial2, layer: 0, camera);
                _wallDeleteStroke.Add(new Pose(itemPosition, Quaternion.Euler(strokeItem.additionalAngle)));
            }
        }

        private static void DeleteWall()
        {
            if (_wallDeleteStroke.Count == 0) return;
            var toolSettings = WallManager.settings;
            var toBeDeleted = new System.Collections.Generic.HashSet<GameObject>();
            var halfCellSize = toolSettings.moduleSize / 2;
            foreach (var cellPose in _wallDeleteStroke)
            {
                var nearbyObjects = new System.Collections.Generic.List<GameObject>();
                boundsOctree.GetColliding(cellPose.position, halfCellSize,
                    SnapManager.settings.rotation, cellPose.rotation, nearbyObjects);
                if (nearbyObjects.Count == 0) continue;
                foreach (var obj in nearbyObjects)
                {
                    if (obj == null) continue;
                    if (!obj.activeInHierarchy) continue;
                    var objCenter = BoundsUtils.GetBoundsRecursive(obj.transform).center;
                    var centerDistance = (objCenter - cellPose.position).magnitude;
                    if (centerDistance > WallManager.wallThickness / 2) continue;
                    if (PaletteManager.selectedBrush.ContainsSceneObject(obj)) toBeDeleted.Add(obj);
                }
            }
            void EraseObject(GameObject obj)
            {
                if (obj == null) return;
                var root = UnityEditor.PrefabUtility.GetNearestPrefabInstanceRoot(obj);
                if (root != null) obj = root;
                PWBCore.DestroyTempCollider(obj.GetInstanceID());
                UnityEditor.Undo.DestroyObjectImmediate(obj);
            }
            foreach (var obj in toBeDeleted) EraseObject(obj);
        }
    }
}