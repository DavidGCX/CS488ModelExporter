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

using UnityEngine;
using System.Linq;

namespace PluginMaster
{
    #region DATA & SETTINGS
    [System.Serializable]
    public class ReplacerSettings : CircleToolBase, ISelectionBrushTool, IModifierTool, IPaintToolSettings
    {
        [SerializeField] private bool _keepTargetSize = false;
        [SerializeField] private bool _maintainProportions = false;

        public enum PositionMode { CENTER, PIVOT, ON_SURFACE }
        [SerializeField] private PositionMode _positionMode = PositionMode.CENTER;

        [SerializeField] private bool _sameParentasTarget = true;
        public bool keepTargetSize
        {
            get => _keepTargetSize;
            set
            {
                if (_keepTargetSize == value) return;
                _keepTargetSize = value;
                DataChanged();
            }
        }
        public bool maintainProportions
        {
            get => _maintainProportions;
            set
            {
                if (_maintainProportions == value) return;
                _maintainProportions = value;
                DataChanged();
            }
        }

        public PositionMode positionMode
        {
            get => _positionMode;
            set
            {
                if (_positionMode == value) return;
                _positionMode = value;
                DataChanged();
            }
        }
        public bool sameParentAsTarget
        {
            get => _sameParentasTarget;
            set
            {
                if (_sameParentasTarget == value) return;
                _sameParentasTarget = value;
                DataChanged();
            }
        }

        #region MODIFIER TOOL
        [SerializeField] private ModifierToolSettings _modifierTool = new ModifierToolSettings();
        public ReplacerSettings() => _modifierTool.OnDataChanged += DataChanged;
        public ModifierToolSettings.Command command { get => _modifierTool.command; set => _modifierTool.command = value; }
        public bool modifyAllButSelected
        {
            get => _modifierTool.modifyAllButSelected;
            set => _modifierTool.modifyAllButSelected = value;
        }

        public bool onlyTheClosest
        {
            get => _modifierTool.onlyTheClosest;
            set => _modifierTool.onlyTheClosest = value;
        }
        public bool outermostPrefabFilter
        {
            get => _modifierTool.outermostPrefabFilter;
            set => _modifierTool.outermostPrefabFilter = value;
        }
        #endregion

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

        #endregion

        public override void Copy(IToolSettings other)
        {
            var otherReplacerSettings = other as ReplacerSettings;
            if (otherReplacerSettings == null) return;
            base.Copy(other);
            _modifierTool.Copy(otherReplacerSettings);
            _paintTool.Copy(otherReplacerSettings._paintTool);
            _keepTargetSize = otherReplacerSettings._keepTargetSize;
            _maintainProportions = otherReplacerSettings._maintainProportions;
            _positionMode = otherReplacerSettings._positionMode;
            _sameParentasTarget = otherReplacerSettings._sameParentasTarget;
        }

        public override void DataChanged()
        {
            base.DataChanged();
            BrushstrokeManager.UpdateBrushstroke();
        }
    }

    [System.Serializable]
    public class ReplacerManager : ToolManagerBase<ReplacerSettings> { }
    #endregion

    #region PWBIO
    public static partial class PWBIO
    {
        private static System.Collections.Generic.List<GameObject> _toReplace
            = new System.Collections.Generic.List<GameObject>();
        private static System.Collections.Generic.List<Renderer> _replaceRenderers
            = new System.Collections.Generic.List<Renderer>();
        private static bool _replaceAllSelected = false;
        private static void ReplacerMouseEvents()
        {
            var settings = ReplacerManager.settings;
            if (Event.current.button == 0 && !Event.current.alt
                && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag))
            {
                Replace();
                Event.current.Use();
            }
            if (Event.current.button == 1)
            {
                if (Event.current.type == EventType.MouseDown && (Event.current.control || Event.current.shift))
                {
                    _pinned = true;
                    _pinMouse = Event.current.mousePosition;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp) _pinned = false;
            }
        }

        private static void ReplacerDuringSceneGUI(UnityEditor.SceneView sceneView)
        {
            if (PaletteManager.selectedBrushIdx < 0) return;
            if (_replaceAllSelected)
            {
                BrushstrokeManager.UpdateBrushstroke();
                _paintStroke.Clear();
                _toReplace.Clear();
                _replaceAllSelected = false;
                _toReplace.AddRange(SelectionManager.topLevelSelection);
                foreach (var selected in _toReplace)
                    ReplacePreview(sceneView.camera, selected.transform);
                Replace();
                return;
            }
            ReplacerMouseEvents();

            var mousePos = Event.current.mousePosition;
            if (_pinned) mousePos = _pinMouse;
            var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePos);

            var center = mouseRay.GetPoint(_lastHitDistance);
            if (MouseRaycast(mouseRay, out RaycastHit mouseHit, out GameObject collider,
                float.MaxValue, -1, true, true))
            {
                _lastHitDistance = mouseHit.distance;
                center = mouseHit.point;
                PWBCore.UpdateTempCollidersIfHierarchyChanged();
            }
            DrawCircleTool(center, sceneView.camera, new Color(0f, 0f, 0, 1f), ReplacerManager.settings.radius);
            GetReplacerTargetsAndDrawPreview(center, mouseRay, sceneView.camera);
        }
        public static void ResetReplacer()
        {
            foreach (var renderer in _replaceRenderers)
            {
                if (renderer == null) continue;
                renderer.enabled = true;
            }
            _toReplace.Clear();
            _replaceRenderers.Clear();
            _paintStroke.Clear();
        }

        private static Transform _replaceSurface = null;


        private static void ReplacePreview(Camera camera, Transform target)
        {
            if (BrushstrokeManager.brushstroke.Length == 0) return;
            var strokeItem = BrushstrokeManager.brushstroke[0];
            var prefab = strokeItem.settings.prefab;
            if (prefab == null) return;
            var itemRotation = target.rotation;
            var targetBounds = BoundsUtils.GetBoundsRecursive(target, target.rotation);
            var strokeRotation = Quaternion.Euler(strokeItem.additionalAngle);
            var scaleMult = strokeItem.scaleMultiplier;
            var settings = ReplacerManager.settings;
            if (settings.overwriteBrushProperties)
            {
                var brushSettings = settings.brushSettings;
                var additonalAngle = brushSettings.addRandomRotation
                    ? brushSettings.randomEulerOffset.randomVector : brushSettings.eulerOffset;
                strokeRotation *= Quaternion.Euler(additonalAngle);
                scaleMult = brushSettings.randomScaleMultiplier
                    ? brushSettings.randomScaleMultiplierRange.randomVector : brushSettings.scaleMultiplier;
            }
            var inverseStrokeRotation = Quaternion.Inverse(strokeRotation);
            itemRotation *= strokeRotation;
            var itemBounds = BoundsUtils.GetBoundsRecursive(prefab.transform, prefab.transform.rotation * strokeRotation);

            if (settings.keepTargetSize)
            {
                var targetSize = targetBounds.size;
                var itemSize = itemBounds.size;

                if (settings.maintainProportions)
                {
                    var targetMagnitude = Mathf.Max(targetSize.x, targetSize.y, targetSize.z);
                    var itemMagnitude = Mathf.Max(itemSize.x, itemSize.y, itemSize.z);
                    scaleMult = inverseStrokeRotation * (Vector3.one * (targetMagnitude / itemMagnitude));
                }
                else scaleMult = inverseStrokeRotation
                        * new Vector3(targetSize.x / itemSize.x, targetSize.y / itemSize.y, targetSize.z / itemSize.z);
                scaleMult = new Vector3(Mathf.Abs(scaleMult.x), Mathf.Abs(scaleMult.y), Mathf.Abs(scaleMult.z));
            }
            var itemScale = Vector3.Scale(prefab.transform.localScale, scaleMult);
            var itemPosition = targetBounds.center;
            _replaceSurface = null;
            if (settings.positionMode == ReplacerSettings.PositionMode.ON_SURFACE)
            {
                var TRS = Matrix4x4.TRS(itemPosition, itemRotation, itemScale);
                var bottomDistanceToSurfce = GetBottomDistanceToSurface(strokeItem.settings.bottomVertices,
                    TRS, Mathf.Abs(strokeItem.settings.bottomMagnitude), paintOnPalettePrefabs: true,
                    castOnMeshesWithoutCollider: true, out _replaceSurface,
                    new System.Collections.Generic.HashSet<GameObject> { target.gameObject });
                itemPosition += itemRotation * new Vector3(0f, -bottomDistanceToSurfce, 0f);
            }
            else
            {
                if (settings.positionMode == ReplacerSettings.PositionMode.PIVOT)
                    itemPosition = target.position;
                itemPosition -= itemRotation * Vector3.Scale(itemBounds.center - prefab.transform.position, scaleMult);
            }

            var layer = settings.overwritePrefabLayer ? settings.layer : target.gameObject.layer;
            Transform parentTransform = target.parent;

            if (!settings.sameParentAsTarget)
                parentTransform = GetParent(settings, prefab.name, create: false, _replaceSurface);

            _paintStroke.Add(new PaintStrokeItem(prefab, itemPosition,
                itemRotation * prefab.transform.rotation,
                itemScale, layer, parentTransform, null, false, false));
            var rootToWorld = Matrix4x4.TRS(itemPosition, itemRotation, scaleMult)
                * Matrix4x4.Translate(-prefab.transform.position);
            PreviewBrushItem(prefab, rootToWorld, layer, camera, false, false, strokeItem.flipX, strokeItem.flipY);
        }

        private static void Replace()
        {
            if (_toReplace.Count == 0) return;
            if (_paintStroke.Count != _toReplace.Count) return;
            const string COMMAND_NAME = "Replace";
            foreach (var renderer in _replaceRenderers) renderer.enabled = true;
            _replaceRenderers.Clear();
            var settings = ReplacerManager.settings;
            for (int i = 0; i < _toReplace.Count; ++i)
            {
                var target = _toReplace[i];
                if (target == null) continue;
                var item = _paintStroke[i];
                if (item.prefab == null) continue;
                if (settings.outermostPrefabFilter)
                {
                    var nearestRoot = UnityEditor.PrefabUtility.GetNearestPrefabInstanceRoot(target);
                    if (nearestRoot != null) target = nearestRoot;
                }
                else
                {
                    var parent = target.transform.parent.gameObject;
                    if (parent != null)
                    {
                        GameObject outermost = null;
                        do
                        {
                            outermost = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(target);
                            if (outermost == null) break;
                            if (outermost == target) break;
                            UnityEditor.PrefabUtility.UnpackPrefabInstance(outermost,
                                UnityEditor.PrefabUnpackMode.OutermostRoot, UnityEditor.InteractionMode.UserAction);
                        } while (outermost != parent);
                    }
                }
                var type = UnityEditor.PrefabUtility.GetPrefabAssetType(item.prefab);
                GameObject obj = type == UnityEditor.PrefabAssetType.NotAPrefab ? GameObject.Instantiate(item.prefab)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab
                    (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(item.prefab)
                    ? item.prefab : UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(item.prefab));
                if (settings.overwritePrefabLayer) obj.layer = settings.layer;
                obj.transform.SetPositionAndRotation(item.position, item.rotation);
                obj.transform.localScale = item.scale;
                var root = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
                PWBCore.AddTempCollider(obj);
                AddPaintedObject(obj);

                if (!LineManager.instance.ReplaceObject(target, obj))
                    if (!ShapeManager.instance.ReplaceObject(target, obj))
                        TilingManager.instance.ReplaceObject(target, obj);

                BrushstrokeManager.UpdateBrushstroke();
                var tempColliders = PWBCore.GetTempColliders(target);
                if (tempColliders != null)
                    foreach (var tempCollider in tempColliders) UnityEditor.Undo.DestroyObjectImmediate(tempCollider);
                UnityEditor.Undo.DestroyObjectImmediate(target);
                UnityEditor.Undo.RegisterCreatedObjectUndo(obj, COMMAND_NAME);
                Transform parentTransform = item.parent;
                if (settings.sameParentAsTarget)
                {
                    if (root != null) UnityEditor.Undo.SetTransformParent(root.transform, parentTransform, COMMAND_NAME);
                    else UnityEditor.Undo.SetTransformParent(obj.transform, parentTransform, COMMAND_NAME);
                }
                else
                {
                    parentTransform = GetParent(settings, item.prefab.name, create: true, _replaceSurface);
                    UnityEditor.Undo.SetTransformParent(obj.transform, parentTransform, COMMAND_NAME);
                }
            }
            _paintStroke.Clear();
            _toReplace.Clear();
        }

        public static void ReplaceAllSelected() => _replaceAllSelected = true;

        private static void GetReplacerTargetsAndDrawPreview(Vector3 center, Ray mouseRay, Camera camera)
        {
            var targetsSet = new System.Collections.Generic.HashSet<GameObject>(); 
            GetCircleToolTargets(mouseRay, camera, ReplacerManager.settings, ReplacerManager.settings.radius, targetsSet);
            var toReplace = targetsSet.ToArray();
            _toReplace.Clear();
            _paintStroke.Clear();

            foreach (var renderer in _replaceRenderers)
            {
                if (renderer == null) continue;
                renderer.enabled = true;
            }
            _replaceRenderers.Clear();
            for (int i = 0; i < toReplace.Length; ++i)
            {
                var obj = toReplace[i];
                var isChild = false;
                foreach (var listed in toReplace)
                {
                    if (obj.transform.IsChildOf(listed.transform) && listed != obj)
                    {
                        isChild = true;
                        break;
                    }
                }
                if (isChild) continue;
                _toReplace.Add(obj);
                _replaceRenderers.AddRange(obj.GetComponentsInChildren<Renderer>().Where(r => r.enabled == true));
                ReplacePreview(camera, obj.transform);
                foreach (var renderer in _replaceRenderers) renderer.enabled = false;
                BrushstrokeManager.UpdateBrushstroke();
            }
        }
    }

    #endregion
}