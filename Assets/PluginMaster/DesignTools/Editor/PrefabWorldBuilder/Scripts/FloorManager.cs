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
    public class FloorSettings : ModularToolBase { }

    [System.Serializable]
    public class FloorManager : ToolManagerBase<FloorSettings>
    {
        public enum ToolState
        {
            FIRST_CORNER,
            SECOND_CORNER
        }
        public static ToolState state { get; set; } = ToolState.FIRST_CORNER;
        public static Vector3 firstCorner { get; set; } = Vector3.zero;
        public static Vector3 secondCorner { get; set; } = Vector3.zero;
    }
    #endregion
    #region PWBIO
    public static partial class PWBIO
    {
        #region HANDLERS
        private static void FloorInitializeOnLoad()
        {
            FloorManager.settings.OnDataChanged += OnFloorSettingsChanged;
            BrushSettings.OnBrushSettingsChanged += UpdateFloorSettingsOnBrushChanged;
        }

        private static void SetSnapStepToFloorCellSize()
        {
            SnapManager.settings.step = FloorManager.settings.moduleSize + FloorManager.settings.spacing;
            UnityEditor.SceneView.RepaintAll();
        }

        private static void OnFloorSettingsChanged()
        {
            repaint = true;
            BrushstrokeManager.UpdateFloorBrushstroke();
            SetSnapStepToFloorCellSize();
        }
        
        public static void UpdateFloorSettingsOnBrushChanged()
        {
            if (ToolManager.tool != ToolManager.PaintTool.FLOOR) return;
            FloorManager.settings.UpdateCellSize();
            SetSnapStepToFloorCellSize();
            FloorManager.state = FloorManager.ToolState.FIRST_CORNER;
        }
        #endregion
        public static void OnFloorEnabled()
        {
            SnapManager.settings.radialGridEnabled = false;
            SnapManager.settings.gridOnY = true;
            SnapManager.settings.visibleGrid = true;
            SnapManager.settings.lockedGrid = true;
            SnapManager.settings.snappingOnX = true;
            SnapManager.settings.snappingOnZ = true;
            SnapManager.settings.snappingEnabled = true;
            UpdateFloorSettingsOnBrushChanged();
            SnapManager.settings.DataChanged(repaint: true);
            FloorManager.state = FloorManager.ToolState.FIRST_CORNER;
        }
        private static Vector3 _floorSecondCorner = Vector3.zero;
        private static bool _modularDeleteMode = false;
        private static void FloorToolDuringSceneGUI(UnityEditor.SceneView sceneView)
        {
            var mousePos2D = Event.current.mousePosition;
            var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePos2D);
            var mousePos3D = Vector3.zero;
            if (GridRaycast(mouseRay, out RaycastHit gridHit)) mousePos3D = SnapFloorTilePosition(gridHit.point);
            else return;
            if (Event.current.control && Event.current.type == EventType.KeyDown) _modularDeleteMode = true;
            else if (_modularDeleteMode && !Event.current.control && Event.current.type == EventType.KeyUp)
            {
                _modularDeleteMode = false;
                FloorManager.state = FloorManager.ToolState.FIRST_CORNER;
                return;
            }
            if (PaletteManager.selectedBrush == null) return;
            if (Event.current.button == 0)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    FloorManager.state = FloorManager.ToolState.SECOND_CORNER;
                    FloorManager.secondCorner = FloorManager.firstCorner = mousePos3D;
                    BrushstrokeManager.UpdateFloorBrushstroke(_modularDeleteMode);
                }
                if (FloorManager.state == FloorManager.ToolState.SECOND_CORNER)
                {
                    if (Event.current.type == EventType.MouseDrag)
                    {
                        FloorManager.secondCorner = mousePos3D;
                        if (_floorSecondCorner != FloorManager.secondCorner)
                            BrushstrokeManager.UpdateFloorBrushstroke(_modularDeleteMode);
                    }
                    if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseMove)
                    {
                        FloorManager.secondCorner = mousePos3D;
                        if (_modularDeleteMode)
                        {
                            BrushstrokeManager.UpdateFloorBrushstroke(_modularDeleteMode);
                            DeleteFloor();
                        }
                        else
                        {
                            _paintStroke.Clear();
                            BrushstrokeManager.UpdateFloorBrushstroke();
                            PreviewFloorRectangle(sceneView.camera);
                            Paint(FloorManager.settings);
                        }
                        FloorManager.state = FloorManager.ToolState.FIRST_CORNER;
                        BrushstrokeManager.UpdateFloorBrushstroke();
                    }
                }
                _floorSecondCorner = FloorManager.secondCorner;
            }
            if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape)
            {
                FloorManager.state = FloorManager.ToolState.FIRST_CORNER;
                BrushstrokeManager.UpdateFloorBrushstroke();
            }
            switch (FloorManager.state)
            {
                case FloorManager.ToolState.FIRST_CORNER:
                    if (_modularDeleteMode) PreviewFloorDeleteSingleTile(sceneView.camera, mousePos3D);
                    else PreviewFloorSingleTile(sceneView.camera, mousePos3D);
                    break;
                case FloorManager.ToolState.SECOND_CORNER:
                    if (_modularDeleteMode)
                        PreviewFloorDeleteRectangle(sceneView.camera, mousePos3D);
                    else PreviewFloorRectangle(sceneView.camera);
                    break;
            }
        }
        private static Vector3 GetCenterToPivot(GameObject prefab, Vector3 scaleMult, Quaternion rotation)
        {
            var itemBounds = BoundsUtils.GetBoundsRecursive(prefab.transform, Quaternion.identity);
            var centerToPivot = prefab.transform.position - itemBounds.center;
            centerToPivot = Vector3.Scale(centerToPivot, scaleMult);
            centerToPivot = rotation * centerToPivot;
            return centerToPivot;
        }
        private static void PreviewFloorSingleTile(Camera camera, Vector3 mousePos3D)
        {
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var strokeItem = BrushstrokeManager.brushstroke[0].Clone();
            if (strokeItem.settings == null)
            {
                BrushstrokeManager.UpdateFloorBrushstroke();
                return;
            }
            var prefab = strokeItem.settings.prefab;
            if (prefab == null) return;
            var itemRotation = Quaternion.Euler(strokeItem.additionalAngle);
            var scaleMult = strokeItem.scaleMultiplier;

            var cellCenter = mousePos3D;
            var centerToPivot = GetCenterToPivot(prefab, scaleMult, itemRotation);
            var itemPosition = cellCenter + centerToPivot;
            var translateMatrix = Matrix4x4.Translate(Quaternion.Inverse(itemRotation) * -prefab.transform.position);
            var rootToWorld = Matrix4x4.TRS(itemPosition, itemRotation, scaleMult) * translateMatrix;
            var layer = FloorManager.settings.overwritePrefabLayer ? FloorManager.settings.layer : prefab.layer;

            PreviewBrushItem(prefab, rootToWorld, layer, camera,
                redMaterial: false, reverseTriangles: false, flipX: false, flipY: false);
        }

        private static void PreviewFloorRectangle(Camera camera)
        {
            BrushstrokeItem[] brushstroke = null;
            if (PreviewIfBrushtrokestaysTheSame(out brushstroke, camera, forceUpdate: _paintStroke.Count == 0)) return;
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            _paintStroke.Clear();
            var toolSettings = FloorManager.settings;
            var halfCellSize = toolSettings.moduleSize / 2;
            for (int i = 0; i < brushstroke.Length; ++i)
            {
                var strokeItem = brushstroke[i];
                if (strokeItem.settings == null) return;
                var prefab = strokeItem.settings.prefab;
                if (prefab == null) return;
                //BrushSettings brushSettings = strokeItem.settings;
                //if (toolSettings.overwriteBrushProperties) brushSettings = toolSettings.brushSettings;
                var scaleMult = strokeItem.scaleMultiplier;
                var itemRotation = Quaternion.Euler(strokeItem.additionalAngle);

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
                        if (!obj.activeInHierarchy) continue;
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
        private static Mesh _cubeMesh = null;
        private static Mesh cubeMesh
        {
            get
            {
                if (_cubeMesh == null) _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                return _cubeMesh;
            }
        }
        private static void PreviewFloorDeleteSingleTile(Camera camera, Vector3 mousePos3D)
        {
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var TRS = Matrix4x4.TRS(mousePos3D, SnapManager.settings.rotation, FloorManager.settings.moduleSize);
            Graphics.DrawMesh(cubeMesh, TRS, transparentRedMaterial2, 0, camera);
        }

        private static System.Collections.Generic.HashSet<Pose> _floorDeleteStroke
            = new System.Collections.Generic.HashSet<Pose>();
        private static void PreviewFloorDeleteRectangle(Camera camera, Vector3 mousePos3D)
        {
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var brushstroke = BrushstrokeManager.brushstroke;
            var toolSettings = FloorManager.settings;
            _floorDeleteStroke.Clear();
            for (int i = 0; i < brushstroke.Length; ++i)
            {
                var strokeItem = brushstroke[i];
                var itemPosition = strokeItem.tangentPosition;
                var rootToWorld = Matrix4x4.TRS(itemPosition, SnapManager.settings.rotation, FloorManager.settings.moduleSize);
                Graphics.DrawMesh(cubeMesh, rootToWorld, transparentRedMaterial2, layer: 0, camera);
                _floorDeleteStroke.Add(new Pose(itemPosition, Quaternion.Euler(strokeItem.additionalAngle)));
            }
        }
        private static void DeleteFloor()
        {
            if (_floorDeleteStroke.Count == 0) return;
            var toolSettings = FloorManager.settings;
            var toBeDeleted = new System.Collections.Generic.HashSet<GameObject>();
            var halfCellSize = toolSettings.moduleSize / 2;
            foreach (var cellPose in _floorDeleteStroke)
            {
                var nearbyObjects = new System.Collections.Generic.List<GameObject>();
                boundsOctree.GetColliding(cellPose.position, halfCellSize,
                    SnapManager.settings.rotation, cellPose.rotation, nearbyObjects);
                if (nearbyObjects.Count == 0) continue;

                foreach (var obj in nearbyObjects)
                {
                    if (obj == null) continue;
                    if (!obj.activeInHierarchy) continue;
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
    #endregion
}
