﻿/*
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
    public static class BoundsUtils
    {
        public static readonly Vector3 MIN_VECTOR3 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        public static readonly Vector3 MAX_VECTOR3 = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

        public enum ObjectProperty
        {
            BOUNDING_BOX,
            CENTER,
            PIVOT
        }

        public static Vector3 GetMaxVector(Vector3[] values)
        {
            var max = MIN_VECTOR3;
            foreach (var value in values) max = Vector3.Max(max, value);
            return max;
        }

        public static Vector3 GetMaxSize(GameObject[] objs)
        {
            var max = MIN_VECTOR3;
            foreach (var obj in objs)
            {
                var size = Vector3.zero;
                if (obj != null) size = GetBoundsRecursive(obj.transform).size;
                max = Vector3.Max(max, size);
            }
            return max;
        }

        private static System.Collections.Generic.Dictionary<(int, ObjectProperty), Bounds> _boundsDictionary
            = new System.Collections.Generic.Dictionary<(int, ObjectProperty), Bounds>();

        public static Bounds GetBounds(Transform transform, ObjectProperty property = ObjectProperty.BOUNDING_BOX,
            bool useDictionary = true)
        {
            var key = (transform.gameObject.GetInstanceID(), property);
            if (useDictionary && _boundsDictionary.ContainsKey(key)) return _boundsDictionary[key];
            var terrain = transform.GetComponent<Terrain>();
            var renderer = transform.GetComponent<Renderer>();
            var rectTransform = transform.GetComponent<RectTransform>();
            var lodGroup = renderer == null ? transform.GetComponent<LODGroup>() : null;
            Bounds DoGetBounds()
            {
                if (lodGroup != null && property == ObjectProperty.BOUNDING_BOX)
                {
                    var lods = lodGroup.GetLODs();
                    if (lods != null && lods.Length > 0)
                    {
                        lods = lods.Where(l => l.renderers != null).ToArray();
                        if (lods.Length > 0)
                        {
                            var lodGameObjects = lods[0].renderers.Where(r => r != null).Select(r => r.gameObject)
                                .Where(o => o.GetComponent<LODGroup>() == null).ToArray();
                            return GetSelectionBounds(lodGameObjects, false);
                        }
                    }
                }

                if (rectTransform == null && terrain == null)
                {
                    if (renderer == null || !renderer.enabled || property == ObjectProperty.PIVOT)
                        return new Bounds(transform.position, Vector3.zero);
                    if (property == ObjectProperty.CENTER) return new Bounds(renderer.bounds.center, Vector3.zero);
                    return renderer.bounds;
                }

                if (property == ObjectProperty.PIVOT) return new Bounds(transform.position, Vector3.zero);
                if (terrain != null)
                {
                    var bounds = terrain.terrainData.bounds;
                    bounds.center += transform.position;
                    return bounds;
                }
                return new Bounds(rectTransform.TransformPoint(rectTransform.rect.center),
                        rectTransform.TransformVector(rectTransform.rect.size));
            }
            var result = DoGetBounds();
            if (useDictionary) _boundsDictionary.Add(key, result);
            return result;
        }

        private static System.Collections.Generic.Dictionary<(int, ObjectProperty, Vector2), Bounds>
            _boundsRecursiveDictionary = new System.Collections.Generic.Dictionary<(int, ObjectProperty, Vector2), Bounds>();

        public static Bounds GetBoundsRecursive(Transform transform, bool recursive = true,
            ObjectProperty property = ObjectProperty.BOUNDING_BOX, bool useDictionary = true)
        {
            if (!recursive) return GetBounds(transform, property, useDictionary);
            var pivot2D = Vector2.zero;
            var spriteRenderer = transform.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.enabled && spriteRenderer.sprite != null)
                pivot2D = spriteRenderer.sprite.pivot;

            var key = (transform.gameObject.GetInstanceID(), property, pivot2D);
            if (useDictionary && _boundsRecursiveDictionary.ContainsKey(key))
                return _boundsRecursiveDictionary[key];

            var children = transform.GetComponentsInChildren<Transform>(true);
            var min = MAX_VECTOR3;
            var max = MIN_VECTOR3;
            var emptyHierarchy = true;
            bool IsActiveInHierarchy(Transform obj)
            {
                var parent = obj;
                do
                {
                    if (!parent.gameObject.activeSelf) return false;
                    parent = parent.parent;
                }
                while (parent != null);
                return true;
            }
            foreach (var child in children)
            {
                var notActive = !IsActiveInHierarchy(child);
                if (notActive) continue;
                var renderer = child.GetComponent<Renderer>();
                var rectTransform = child.GetComponent<RectTransform>();
                var terrain = child.GetComponent<Terrain>();
                var lodGroup = child.GetComponent<LODGroup>();
                if ((renderer == null || !renderer.enabled) && rectTransform == null && terrain == null && lodGroup == null)
                    continue;
                var bounds = GetBounds(child, property, useDictionary);
                if (bounds.size == Vector3.zero) continue;
                emptyHierarchy = false;
                min = Vector3.Min(bounds.min, min);
                max = Vector3.Max(bounds.max, max);
            }
            if (emptyHierarchy) return new Bounds(transform.position, Vector3.zero);
            var size = max - min;
            var center = min + size / 2f;
            var result = new Bounds(center, size);
            if (useDictionary) _boundsRecursiveDictionary.Add(key, result);
            return result;
        }

        public static Bounds GetSelectionBounds(GameObject[] selection, bool recursive = true,
            BoundsUtils.ObjectProperty property = BoundsUtils.ObjectProperty.BOUNDING_BOX)
        {
            var max = MIN_VECTOR3;
            var min = MAX_VECTOR3;
            if (selection.Length == 0) return new Bounds();
            foreach (var obj in selection)
            {
                if (obj == null) continue;
                var bounds = GetBoundsRecursive(obj.transform, recursive, property);
                max = Vector3.Max(bounds.max, max);
                min = Vector3.Min(bounds.min, min);
            }
            var size = max - min;
            var center = min + size / 2f;
            return new Bounds(center, size);
        }

        public static Bounds GetBounds(Transform transform, Quaternion rotation, bool useDictionary = true)
        {
            var rectTransform = transform.GetComponent<RectTransform>();
            if (rectTransform != null)
                return new Bounds(rectTransform.TransformPoint(rectTransform.rect.center),
                    rectTransform.TransformVector(rectTransform.rect.size));
            var renderer = transform.GetComponent<Renderer>();
            var meshFilter = transform.GetComponent<MeshFilter>();
            if (renderer == null || meshFilter == null || meshFilter.sharedMesh == null || !renderer.enabled)
                return new Bounds(transform.position, Vector3.zero);
            var maxSqrDistance = MIN_VECTOR3;
            var minSqrDistance = MAX_VECTOR3;
            var center = GetBounds(transform, ObjectProperty.BOUNDING_BOX, true).center;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;
            foreach (var vertex in meshFilter.sharedMesh.vertices)
            {
                var centerToVertex = transform.TransformPoint(vertex) - center;
                var rightProjection = Vector3.Project(centerToVertex, right);
                var upProjection = Vector3.Project(centerToVertex, up);
                var forwardProjection = Vector3.Project(centerToVertex, forward);
                var rightSqrDistance = rightProjection.sqrMagnitude * (rightProjection.normalized != right ? -1 : 1);
                var upSqrDistance = upProjection.sqrMagnitude * (upProjection.normalized != up ? -1 : 1);
                var forwardSqrDistance = forwardProjection.sqrMagnitude
                    * (forwardProjection.normalized != forward ? -1 : 1);
                maxSqrDistance.x = Mathf.Max(maxSqrDistance.x, rightSqrDistance);
                maxSqrDistance.y = Mathf.Max(maxSqrDistance.y, upSqrDistance);
                maxSqrDistance.z = Mathf.Max(maxSqrDistance.z, forwardSqrDistance);
                minSqrDistance.x = Mathf.Min(minSqrDistance.x, rightSqrDistance);
                minSqrDistance.y = Mathf.Min(minSqrDistance.y, upSqrDistance);
                minSqrDistance.z = Mathf.Min(minSqrDistance.z, forwardSqrDistance);
            }
            var size = new Vector3(
                Mathf.Sqrt(Mathf.Abs(maxSqrDistance.x)) * Mathf.Sign(maxSqrDistance.x)
                - Mathf.Sqrt(Mathf.Abs(minSqrDistance.x)) * Mathf.Sign(minSqrDistance.x),
                Mathf.Sqrt(Mathf.Abs(maxSqrDistance.y)) * Mathf.Sign(maxSqrDistance.y)
                - Mathf.Sqrt(Mathf.Abs(minSqrDistance.y)) * Mathf.Sign(minSqrDistance.y),
                Mathf.Sqrt(Mathf.Abs(maxSqrDistance.z)) * Mathf.Sign(maxSqrDistance.z)
                - Mathf.Sqrt(Mathf.Abs(minSqrDistance.z)) * Mathf.Sign(minSqrDistance.z));
            return new Bounds(center, size);
        }

        private static void GetDistanceFromCenter(Transform transform, Quaternion rotation,
            Vector3 center, out Vector3 min, out Vector3 max, bool ignoreDisabled = true)
        {
            min = max = Vector3.zero;
            if (ignoreDisabled && !transform.gameObject.activeSelf) return;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var rectTransform = transform.GetComponent<RectTransform>();
            var terrain = transform.GetComponent<Terrain>();
            if (rectTransform != null)
            {
                vertices.Add(rectTransform.rect.min);
                vertices.Add(rectTransform.rect.max);
                vertices.Add(new Vector2(rectTransform.rect.min.x, rectTransform.rect.max.y));
                vertices.Add(new Vector2(rectTransform.rect.max.x, rectTransform.rect.min.y));
            }
            else if (terrain != null) vertices.AddRange(TerrainUtils.GetCorners(terrain, Space.Self));
            else
            {
                var renderer = transform.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled) return;
                if (renderer is SpriteRenderer)
                {
                    var sprite = (renderer as SpriteRenderer).sprite;
                    if (sprite == null) return;
                    var rot = renderer.transform.rotation;
                    renderer.transform.rotation = Quaternion.Euler(0, 0, 0);
                    var spriteMin = renderer.transform.InverseTransformPoint(renderer.bounds.min);
                    var spriteMax = renderer.transform.InverseTransformPoint(renderer.bounds.max);
                    renderer.transform.rotation = rot;

                    vertices.Add(new Vector3(spriteMin.x, spriteMin.y, 0));
                    vertices.Add(new Vector3(spriteMin.x, spriteMax.y, 0));
                    vertices.Add(new Vector3(spriteMax.x, spriteMin.y, 0));
                    vertices.Add(new Vector3(spriteMax.x, spriteMax.y, 0));
                }
                else if (renderer is MeshRenderer)
                {
                    var meshFilter = transform.GetComponent<MeshFilter>();
                    if (meshFilter == null || meshFilter.sharedMesh == null) return;
                    vertices.AddRange(meshFilter.sharedMesh.vertices);
                }
                else if (renderer is SkinnedMeshRenderer)
                {
                    var mesh = (renderer as SkinnedMeshRenderer).sharedMesh;
                    if (mesh == null) return;
                    vertices.AddRange(mesh.vertices);
                }
            }
            if (vertices.Count == 0)
            {
                min = max = Vector3.zero;
                return;
            }
            var maxDistance = MIN_VECTOR3;
            var minDistance = MAX_VECTOR3;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;

            foreach (var vertex in vertices)
            {
                var centerToVertex = transform.TransformPoint(vertex) - center;
                var rightProjection = Vector3.Project(centerToVertex, right);
                var upProjection = Vector3.Project(centerToVertex, up);
                var forwardProjection = Vector3.Project(centerToVertex, forward);
                var rightDistance = rightProjection.magnitude * (rightProjection.normalized != right ? -1 : 1);
                var upDistance = upProjection.magnitude * (upProjection.normalized != up ? -1 : 1);
                var forwardDistance = forwardProjection.magnitude
                    * (forwardProjection.normalized != forward ? -1 : 1);

                maxDistance.x = Mathf.Max(maxDistance.x, rightDistance);
                maxDistance.y = Mathf.Max(maxDistance.y, upDistance);
                maxDistance.z = Mathf.Max(maxDistance.z, forwardDistance);

                minDistance.x = Mathf.Min(minDistance.x, rightDistance);
                minDistance.y = Mathf.Min(minDistance.y, upDistance);
                minDistance.z = Mathf.Min(minDistance.z, forwardDistance);
            }

            min = new Vector3(
                Mathf.Abs(minDistance.x) * Mathf.Sign(minDistance.x),
                Mathf.Abs(minDistance.y) * Mathf.Sign(minDistance.y),
                Mathf.Abs(minDistance.z) * Mathf.Sign(minDistance.z));
            max = new Vector3(
               Mathf.Abs(maxDistance.x) * Mathf.Sign(maxDistance.x),
               Mathf.Abs(maxDistance.y) * Mathf.Sign(maxDistance.y),
               Mathf.Abs(maxDistance.z) * Mathf.Sign(maxDistance.z));
        }


        private static void GetDistanceFromCenterRecursive(Transform transform, Quaternion rotation,
            Vector3 center, out Vector3 minDistance, out Vector3 maxDistance, bool ignoreDissabled = true, bool recursive = true)
        {
            var children = recursive ? transform.GetComponentsInChildren<Transform>(true) : new Transform[] { transform };
            var emptyHierarchy = true;
            maxDistance = MIN_VECTOR3;
            minDistance = MAX_VECTOR3;
            foreach (var child in children)
            {
                var renderer = child.GetComponent<Renderer>();
                var rectTransform = child.GetComponent<RectTransform>();
                var terrain = child.GetComponent<Terrain>();
                if ((renderer == null || !renderer.enabled) && rectTransform == null && terrain == null) continue;
                emptyHierarchy = false;

                Vector3 min, max;
                GetDistanceFromCenter(child, rotation, center, out min, out max, ignoreDissabled);
                minDistance = Vector3.Min(min, minDistance);
                maxDistance = Vector3.Max(max, maxDistance);
            }
            if (emptyHierarchy) minDistance = maxDistance = Vector3.zero;
        }
        private struct BoundsRotKey
        {
            int id;
            Vector3 position;
            Quaternion rotation;
            Vector3 scale;
            Quaternion boundsRotation;
            Vector2 pivot2D;

            public BoundsRotKey(int id, Vector3 position, Quaternion rotation,
                Vector3 scale, Quaternion boundsRotation, Vector2 pivot2D)
            {
                this.id = id;
                this.position = position;
                this.rotation = rotation;
                this.scale = scale;
                this.boundsRotation = boundsRotation;
                this.pivot2D = pivot2D;
            }
        }

        private static System.Collections.Generic
            .Dictionary<BoundsRotKey, Bounds> _boundsRotDictionary
            = new System.Collections.Generic.Dictionary<BoundsRotKey, Bounds>();
        public static Bounds GetBoundsRecursive(Transform transform, Quaternion rotation, bool ignoreDissabled = true,
            ObjectProperty property = ObjectProperty.BOUNDING_BOX, bool recursive = true, bool useDictionary = true)
        {
            if (property == ObjectProperty.PIVOT) return new Bounds(transform.position, Vector3.zero);
            var pivot2D = Vector2.zero;
            var spriteRenderer = transform.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.enabled && spriteRenderer.sprite != null)
                pivot2D = spriteRenderer.sprite.pivot;
            var key = new BoundsRotKey(transform.gameObject.GetInstanceID(), transform.position,
                transform.rotation, transform.lossyScale, rotation, pivot2D);
            useDictionary = false;
            if (useDictionary && _boundsRotDictionary.ContainsKey(key)) return _boundsRotDictionary[key];

            var center = GetBoundsRecursive(transform, recursive, property, useDictionary).center;
            if (property == ObjectProperty.CENTER) return new Bounds(center, Vector3.zero);

            Vector3 maxDistance, minDistance;
            GetDistanceFromCenterRecursive(transform, rotation, center,
                out minDistance, out maxDistance, ignoreDissabled, recursive);
            var size = maxDistance - minDistance;
            center += rotation * (minDistance + size / 2);
            var bounds = new Bounds(center, size);
            if (useDictionary) _boundsRotDictionary.Add(key, bounds);
            return new Bounds(center, size);
        }
        public static Bounds GetBoundsRecursive(Transform transform, Quaternion rotation, Vector3 scale,
            bool ignoreDissabled = true)
        {
            var obj = Object.Instantiate(transform.gameObject);
            obj.transform.localScale = Vector3.Scale(obj.transform.localScale, scale);
            var bounds = GetBoundsRecursive(obj.transform, rotation, ignoreDissabled);
            Object.DestroyImmediate(obj);
            return bounds;
        }

        public static Bounds GetSelectionBounds(GameObject[] selection, Quaternion rotation, bool ignoreDissabled = true)
        {
            var max = MIN_VECTOR3;
            var min = MAX_VECTOR3;
            var center = GetSelectionBounds(selection).center;
            bool empty = true;
            foreach (var obj in selection)
            {
                if (obj == null) continue;
                var objMagnitude = GetMagnitude(obj.transform);
                if (objMagnitude == 0) continue;
                Vector3 minDistance, maxDistance;
                GetDistanceFromCenterRecursive(obj.transform, rotation, center,
                    out minDistance, out maxDistance, ignoreDissabled);
                max = Vector3.Max(maxDistance, max);
                min = Vector3.Min(minDistance, min);
                empty = false;
            }
            if (empty) return new Bounds(center, Vector3.zero);
            var size = max - min;
            center += rotation * (min + size / 2);
            return new Bounds(center, size);
        }

        public static Vector3[] GetVertices(Transform transform)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var meshFilters = transform.GetComponentsInChildren<MeshFilter>();
            foreach (var filter in meshFilters)
            {
                if (filter.sharedMesh == null) continue;
                vertices.AddRange(filter.sharedMesh.vertices);
            }
            var skinnedMeshRenderers = transform.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer.sharedMesh == null) continue;
                vertices.AddRange(renderer.sharedMesh.vertices);
            }
            return vertices.ToArray();
        }
        public static Vector3[] GetFurthestVertices(Transform transform, Vector3 direction, out float magnitude)
        {
            var vertices = new System.Collections.Generic.HashSet<Vector3>();
            var allLocalVertices = new System.Collections.Generic.HashSet<(Vector3 position, float distance)>();
            var maxDistance = float.MinValue;
            var meshFilters = transform.GetComponentsInChildren<MeshFilter>();
            void UpdateLastVertex(Vector3 vertex, Transform child)
            {
                var worldVertex = child.TransformPoint(vertex);
                var localVertex = transform.InverseTransformPoint(worldVertex);
                var projection = Vector3.Project(localVertex, direction);
                var projectionDistance = Mathf.Sign(Vector3.Dot(direction, projection))
                    * Vector3.Project(localVertex, direction).magnitude;
                allLocalVertices.Add((localVertex, projectionDistance));
                maxDistance = Mathf.Max(maxDistance, projectionDistance);
            }
            foreach (var filter in meshFilters)
            {
                if (filter.sharedMesh == null) continue;
                foreach (var vertex in filter.sharedMesh.vertices) UpdateLastVertex(vertex, filter.transform);
            }
            var skinnedMeshRenderers = transform.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer.sharedMesh == null) continue;
                foreach (var vertex in renderer.sharedMesh.vertices) UpdateLastVertex(vertex, renderer.transform);
            }

            var threshold = maxDistance * 0.01f;
            magnitude = float.MinValue;
            foreach (var vertex in allLocalVertices)
            {
                if (vertex.distance >= maxDistance - threshold)
                {
                    vertices.Add(vertex.position);

                    var projection = Vector3.Scale(Vector3.Project(vertex.position, direction), transform.localScale);
                    var projectionDistance = Mathf.Sign(Vector3.Dot(direction, projection))
                        * Vector3.Project(vertex.position, direction).magnitude;
                    magnitude = Mathf.Max(magnitude, projectionDistance);
                }
            }
            if (vertices.Count == 0) magnitude = 0f;

            return vertices.ToArray();
        }

        public static float GetMagnitudeInDirection(Transform transform, Vector3 direction)
        {
            float magnitude;
            var vertices = GetFurthestVertices(transform, direction, out magnitude);
            return magnitude;
        }

        public static Vector3[] GetBottomVertices(Transform transform)
        {
            var vertices = new System.Collections.Generic.HashSet<Vector3>();
            var allLocalVertices = new System.Collections.Generic.HashSet<Vector3>();
            var minY = float.MaxValue;
            var meshFilters = transform.GetComponentsInChildren<MeshFilter>();
            void UpdateMinVertex(Vector3 vertex, Transform child)
            {
                var worldVertex = child.TransformPoint(vertex);
                var localVertex = transform.InverseTransformPoint(worldVertex);
                allLocalVertices.Add(localVertex);
                minY = Mathf.Min(localVertex.y, minY);
            }
            foreach (var filter in meshFilters)
            {
                if (filter.sharedMesh == null) continue;
                foreach (var vertex in filter.sharedMesh.vertices) UpdateMinVertex(vertex, filter.transform);
            }
            var skinnedMeshRenderers = transform.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer.sharedMesh == null) continue;
                foreach (var vertex in renderer.sharedMesh.vertices) UpdateMinVertex(vertex, renderer.transform);
            }

            var threshold = 0.01f;
            minY += threshold;
            foreach (var vertex in allLocalVertices) if (vertex.y < minY) vertices.Add(vertex);
            return vertices.ToArray();
        }

        public static float GetBottomMagnitude(Transform transform)
        {
            var vertices = GetBottomVertices(transform);
            var magnitude = float.MinValue;
            foreach (var vertex in vertices)
                magnitude = Mathf.Max(magnitude, vertex.y);
            return magnitude * transform.localScale.y;
        }
        public static float GetMagnitude(Transform transform)
        {
            var size = GetBoundsRecursive(transform).size;
            return Mathf.Max(size.x, size.y, size.z);
        }

        public static float GetAverageMagnitude(Transform transform)
        {
            var size = GetBoundsRecursive(transform).size;
            return (size.x + size.y + size.z) / 3;
        }

        public static void ClearBoundsDictionaries()
        {
            _boundsDictionary.Clear();
            _boundsRecursiveDictionary.Clear();
            _boundsRotDictionary.Clear();
        }
    }
}