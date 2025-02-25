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
    [UnityEditor.InitializeOnLoad]
    public class RenderPipelineDefine
    {
        static RenderPipelineDefine()
        {
            void SetDefineSymbol(string define)
            {
                var target = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
                var buildTargetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup(target);
#if UNITY_2022_2_OR_NEWER
                var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
                var definesSCSV = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
                var definesSCSV = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
#endif

                if (definesSCSV.Contains(define)) return;
                definesSCSV += ";" + define;
#if UNITY_2022_2_OR_NEWER
                UnityEditor.PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, definesSCSV);
#else
                UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, definesSCSV);
#endif
            }

            var currentRenderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (currentRenderPipeline == null) SetDefineSymbol("PWB_BIRP");
            else if (currentRenderPipeline.GetType().ToString().Contains("HighDefinition")) SetDefineSymbol("PWB_HDRP");
            else if (currentRenderPipeline.GetType().ToString().Contains("Universal")) SetDefineSymbol("PWB_URP");
        }
    }

    public class ThumbnailUtils
    {
        private static LayerMask layerMask => 1 << PWBCore.staticData.thumbnailLayer;
        public const int SIZE = 256;
        private const int MIN_SIZE = 24;
        private static Texture2D _emptyTexture = null;
        private static bool _savingImage = false;
        public static bool savingImage => _savingImage;
        private class ThumbnailEditor
        {
            public ThumbnailSettings settings = null;
            public GameObject root = null;
            public Camera camera = null;
            public RenderTexture renderTexture = null;
            public Light light = null;
            public Transform pivot = null;
            public GameObject target = null;
        }

        public static void RenderTextureToTexture2D(RenderTexture renderTexture, Texture2D texture)
        {
            var prevActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0);
            texture.Apply();
            RenderTexture.active = prevActive;
        }

        private static Texture2D emptyTexture
        {
            get
            {
                if (_emptyTexture == null) _emptyTexture = Resources.Load<Texture2D>("Sprites/Empty");
                return _emptyTexture;
            }
        }
        public static void SavePngResource(Texture2D texture, string thumbnailPath)
        {
            if (texture == null || string.IsNullOrEmpty(thumbnailPath)) return;
            _savingImage = true;
            byte[] buffer = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(thumbnailPath, buffer);
            UnityEditor.AssetDatabase.Refresh();
            _savingImage = false;
        }

        public static Texture2D ScaleImage(string imagePath)
        {
            if (!System.IO.File.Exists(imagePath)) return null;
            var rawData = System.IO.File.ReadAllBytes(imagePath);
            Texture2D source = new Texture2D(2, 2);
            ImageConversion.LoadImage(source, rawData);
            RenderTexture renderTexture = RenderTexture.GetTemporary(SIZE, SIZE);
            Graphics.Blit(source, renderTexture);
            Texture2D scaledTexture = new Texture2D(SIZE, SIZE);
            RenderTextureToTexture2D(renderTexture, scaledTexture);
            return scaledTexture;
        }

        public static void CopyTexture(Texture2D from, out Texture2D to)
        {
            if (from == null)
            {
                to = null;
                return;
            }
            to = new Texture2D(from.width, from.height);
            to.SetPixels(from.GetPixels());
            to.Apply();
        }

        private static Material _bgMaterial = null;
        private static Cubemap _defaultCubemap = null;
        public static void UpdateThumbnail(ThumbnailSettings settings,
            Texture2D thumbnailTexture, GameObject prefab, string thumbnailPath, bool savePng)
        {
            var magnitude = BoundsUtils.GetMagnitude(prefab.transform);
            var thumbnailEditor = new ThumbnailEditor();
            thumbnailEditor.settings = new ThumbnailSettings(settings);

            if (magnitude == 0)
            {
                if (_emptyTexture == null) _emptyTexture = Resources.Load<Texture2D>("Sprites/Empty");
                var pixels = _emptyTexture.GetPixels32();
                for (int i = 0; i < pixels.Length; ++i)
                {
                    if (pixels[i].a == 0) pixels[i] = thumbnailEditor.settings.backgroudColor;
                }
                thumbnailTexture.SetPixels32(pixels);
                thumbnailTexture.Apply();
                return;
            }
#if UNITY_2022_2_OR_NEWER
            var sceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .ToDictionary(comp => comp, light => light.cullingMask);
#else
            var sceneLights = Object.FindObjectsOfType<Light>().ToDictionary(comp => comp, light => light.cullingMask);
#endif

            const string rootName = "PWBThumbnailEditor";

            do
            {
                var obj = GameObject.Find(rootName);
                if (obj == null) break;
                else GameObject.DestroyImmediate(obj);
            } while (true);

            thumbnailEditor.root = new GameObject(rootName);

            var camObj = new GameObject("PWBThumbnailEditorCam");
            thumbnailEditor.camera = camObj.AddComponent<Camera>();
            thumbnailEditor.camera.transform.SetParent(thumbnailEditor.root.transform);
            thumbnailEditor.camera.transform.localPosition = new Vector3(0f, 1.2f, -4f);
            thumbnailEditor.camera.transform.localRotation = Quaternion.Euler(17.5f, 0f, 0f);
            thumbnailEditor.camera.fieldOfView = 20f;
            thumbnailEditor.camera.clearFlags = CameraClearFlags.SolidColor;
            thumbnailEditor.camera.backgroundColor = thumbnailEditor.settings.backgroudColor;
            thumbnailEditor.camera.cullingMask = layerMask;
            thumbnailEditor.renderTexture = new RenderTexture(SIZE, SIZE, 24);
            thumbnailEditor.camera.targetTexture = thumbnailEditor.renderTexture;

            var originalAmbientMode = RenderSettings.ambientMode;
            var originalAmbientLight = RenderSettings.ambientLight;
            var originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            var originalAmbientGroundColor = RenderSettings.ambientGroundColor;
            var originalAmbientSkyColor = RenderSettings.ambientSkyColor;
            var originalAmbientIntensity = RenderSettings.ambientIntensity;
            var originalAmbientProbe = RenderSettings.ambientProbe;
            var originalReflectionMode = RenderSettings.defaultReflectionMode;
            var originalSkybox = RenderSettings.skybox;
            var originalFog = RenderSettings.fog;
            var originalFogColor = RenderSettings.fogColor;
            var originalFogStartDistance = RenderSettings.fogStartDistance;
            var originalFogEndDistance = RenderSettings.fogEndDistance;
            var originalFogDensity = RenderSettings.fogDensity;
            var originalFogMode = RenderSettings.fogMode;
            var originalHaloStrength = RenderSettings.haloStrength;
            var originalFlareFadeSpeed = RenderSettings.flareFadeSpeed;
            var originalFlareStrength = RenderSettings.flareStrength;
            var originalReflectionIntensity = RenderSettings.reflectionIntensity;
            var originalReflectionBounces = RenderSettings.reflectionBounces;
            var originalDefaultReflectionResolution = RenderSettings.defaultReflectionResolution;
            var originalSubtractiveShadowColor = RenderSettings.subtractiveShadowColor;
            var originalSun = RenderSettings.sun;
#if UNITY_2022_2_OR_NEWER
            var originalReflectionTexture = RenderSettings.customReflectionTexture;
#else
            var originalReflectionTexture = RenderSettings.customReflection;
#endif
            float intensityMultiplier = 0.7f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = thumbnailEditor.settings.lightColor;
            RenderSettings.ambientEquatorColor = thumbnailEditor.settings.backgroudColor;
            RenderSettings.ambientGroundColor = thumbnailEditor.settings.backgroudColor;
            RenderSettings.ambientSkyColor = thumbnailEditor.settings.backgroudColor;
            RenderSettings.ambientIntensity = thumbnailEditor.settings.lightIntensity * intensityMultiplier;
            RenderSettings.ambientProbe = new UnityEngine.Rendering.SphericalHarmonicsL2();
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.fogColor = Color.clear;
            RenderSettings.fogStartDistance = 0f;
            RenderSettings.fogEndDistance = 1f;
            RenderSettings.fogDensity = 0f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.haloStrength = 0f;
            RenderSettings.flareFadeSpeed = 1f;
            RenderSettings.flareStrength = 0f;
            RenderSettings.reflectionIntensity = thumbnailEditor.settings.lightIntensity * intensityMultiplier;
            RenderSettings.reflectionBounces = 1;
            RenderSettings.defaultReflectionResolution = 128;
            RenderSettings.subtractiveShadowColor = Color.black;
            RenderSettings.sun = null;
            if (_defaultCubemap == null)
            {
                _defaultCubemap = new Cubemap(1, TextureFormat.RGB24, false);
                Color[] colors = { Color.white };
                for (int face = 0; face < 6; face++)
                {
                    _defaultCubemap.SetPixels(colors, (CubemapFace)face);
                }
                _defaultCubemap.Apply();
            }
#if UNITY_2022_2_OR_NEWER
            RenderSettings.customReflectionTexture = _defaultCubemap;
#else
            RenderSettings.customReflection = _defaultCubemap;
#endif


            var lightObj = new GameObject("PWBThumbnailEditorLight");
            thumbnailEditor.light = lightObj.AddComponent<Light>();
            thumbnailEditor.light.type = LightType.Directional;
            thumbnailEditor.light.transform.SetParent(thumbnailEditor.root.transform);
            thumbnailEditor.light.transform.localRotation = Quaternion.Euler(thumbnailEditor.settings.lightEuler);
            thumbnailEditor.light.color = thumbnailEditor.settings.lightColor;
            thumbnailEditor.light.intensity = thumbnailEditor.settings.lightIntensity;
            thumbnailEditor.light.cullingMask = layerMask;

            var pivotObj = new GameObject("PWBThumbnailEditorPivot");
            pivotObj.layer = PWBCore.staticData.thumbnailLayer;
            thumbnailEditor.pivot = pivotObj.transform;
            thumbnailEditor.pivot.transform.SetParent(thumbnailEditor.root.transform);
            thumbnailEditor.pivot.localPosition = thumbnailEditor.settings.targetOffset;
            thumbnailEditor.pivot.transform.localRotation = Quaternion.identity;
            thumbnailEditor.pivot.transform.localScale = Vector3.one;

            Transform InstantiateBones(Transform source, Transform parent)
            {
                var obj = new GameObject();
                obj.name = source.name;
                obj.transform.SetParent(parent);
                obj.transform.position = source.position;
                obj.transform.rotation = source.rotation;
                obj.transform.localScale = source.localScale;
                foreach (Transform child in source) InstantiateBones(child, obj.transform);
                return obj.transform;
            }

            bool Requires(System.Type obj, System.Type requirement)
            {
                return System.Attribute.IsDefined(obj, typeof(RequireComponent))
                    && System.Attribute.GetCustomAttributes(obj, typeof(RequireComponent)).OfType<RequireComponent>()
                       .Any(rc => rc.m_Type0.IsAssignableFrom(requirement));
            }

            bool CanDestroy(GameObject go, System.Type t)
                => !go.GetComponents<Component>().Any(c => Requires(c.GetType(), t));

            void CopyComponents(GameObject source, GameObject destination)
            {
                var srcComps = source.GetComponentsInChildren<Component>();
                foreach (var srcComp in srcComps)
                {
                    if (srcComp is MonoBehaviour) continue;
                    var destComp = srcComp is Transform ? destination.transform : destination.AddComponent(srcComp.GetType());
                    UnityEditor.EditorUtility.CopySerialized(srcComp, destComp);
                }
                foreach (Transform srcChild in source.transform)
                {
                    var destChild = new GameObject();
                    destChild.transform.SetParent(destination.transform);
                    CopyComponents(srcChild.gameObject, destChild);
                }
            }

            GameObject InstantiateAndRemoveMonoBehaviours()
            {
                var obj = Object.Instantiate(prefab);
                var toBeDestroyed = new System.Collections.Generic.List<Component>(obj.GetComponentsInChildren<Component>());

                while (toBeDestroyed.Count > 0)
                {
                    var components = toBeDestroyed.ToArray();
                    int compCount = components.Length;
                    toBeDestroyed.Clear();
                    foreach (var comp in components)
                    {
                        if (comp is MonoBehaviour)
                        {
                            var monoBehaviour = comp as MonoBehaviour;
                            monoBehaviour.enabled = false;
                            monoBehaviour.runInEditMode = false;
                            if (CanDestroy(comp.gameObject, comp.GetType())) Object.DestroyImmediate(comp);
                            else toBeDestroyed.Add(comp);
                        }
                    }
                    if (compCount == toBeDestroyed.Count) break;
                }
                if (toBeDestroyed.Count > 0)
                {
                    var noMonoBehaviourObj = new GameObject();
                    CopyComponents(noMonoBehaviourObj, obj);
                    Object.DestroyImmediate(obj);
                    obj = noMonoBehaviourObj;
                }
                return obj;
            }

            thumbnailEditor.target = InstantiateAndRemoveMonoBehaviours();

            var monoBehaviours = thumbnailEditor.target.GetComponentsInChildren<MonoBehaviour>();
            foreach (var monoBehaviour in monoBehaviours)
                if (monoBehaviour != null) monoBehaviour.enabled = false;

            magnitude = BoundsUtils.GetMagnitude(thumbnailEditor.target.transform);
            var targetScale = magnitude > 0 ? 1f / magnitude : 1f;
            var targetBounds = BoundsUtils.GetBoundsRecursive(thumbnailEditor.target.transform);
            var localPosition = (thumbnailEditor.target.transform.localPosition - targetBounds.center) * targetScale;
            thumbnailEditor.target.transform.SetParent(thumbnailEditor.pivot);
            thumbnailEditor.target.transform.localPosition = localPosition;
            thumbnailEditor.target.transform.localRotation = Quaternion.identity;
            thumbnailEditor.target.transform.localScale = prefab.transform.localScale * targetScale;
            thumbnailEditor.pivot.localScale = Vector3.one * thumbnailEditor.settings.zoom;
            thumbnailEditor.pivot.localRotation = Quaternion.Euler(thumbnailEditor.settings.targetEuler);

            var bgObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bgObject.name = "PWBThumbnailEditorBg";
            if (_bgMaterial == null)
            {
#if PWB_HDRP
                _bgMaterial = new Material(Shader.Find("HDRP/Unlit"));
#else
                _bgMaterial = new Material(Shader.Find("Unlit/Color"));
#endif
            }
            _bgMaterial.color = thumbnailEditor.settings.backgroudColor;

            var bgRenderer = bgObject.GetComponent<MeshRenderer>();
            bgRenderer.sharedMaterial = _bgMaterial;
            bgObject.transform.SetParent(thumbnailEditor.root.transform);
            bgObject.transform.localPosition = new Vector3(0, -3, 10);
            bgObject.transform.localScale = new Vector3(30, 30, 0.1f);


#if PWB_HDRP || PWB_URP
#if UNITY_2022_2_OR_NEWER
            var sceneVolumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)
                .ToDictionary(comp => comp, vol => vol.isActiveAndEnabled);
#else
            var sceneVolumes = Object.FindObjectsOfType<UnityEngine.Rendering.Volume>()
                .ToDictionary(comp => comp, vol => vol.isActiveAndEnabled);
#endif
            foreach (var vol in sceneVolumes.Keys) vol.gameObject.SetActive(false);

            var meshRenderers = thumbnailEditor.target.GetComponentsInChildren<MeshRenderer>()
                .ToDictionary(comp => comp, r => r.lightProbeUsage);
            foreach (var meshRenderer in meshRenderers.Keys)
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            var skinnedMeshRenderers = thumbnailEditor.target.GetComponentsInChildren<SkinnedMeshRenderer>()
                 .ToDictionary(comp => comp, r => r.lightProbeUsage);
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers.Keys)
                skinnedMeshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif

#if PWB_HDRP
            var HDCamData = camObj.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            HDCamData.volumeLayerMask = layerMask | 1;
            HDCamData.probeLayerMask = 0;
            HDCamData.clearColorMode = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.ClearColorMode.Color;
            HDCamData.backgroundColorHDR = thumbnailEditor.settings.backgroudColor;
            HDCamData.antialiasing
                = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;

            var volumeObj = new GameObject("PWBThumbnailEditorVolume");
            var volume = volumeObj.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = false;
            volume.priority = 1;
            volume.profile = Resources.Load<UnityEngine.Rendering.VolumeProfile>("ThumbnailVolume");
            UnityEngine.Rendering.HighDefinition.Exposure exposure = null;
            if (!volume.profile.Has<UnityEngine.Rendering.HighDefinition.Exposure>())
                exposure = volume.profile.Add<UnityEngine.Rendering.HighDefinition.Exposure>(true);
            else volume.profile.TryGet<UnityEngine.Rendering.HighDefinition.Exposure>(out exposure);
            if (exposure != null)
            {
                exposure.mode.value = UnityEngine.Rendering.HighDefinition.ExposureMode.AutomaticHistogram;
                exposure.meteringMode.value = UnityEngine.Rendering.HighDefinition.MeteringMode.CenterWeighted;
                exposure.limitMin.Override(13f);
                exposure.limitMax.Override(15f);
                exposure.compensation.Override(thumbnailEditor.light.intensity);
            }
            var volumeCollider = volumeObj.AddComponent<BoxCollider>();
            volumeCollider.size = new Vector3(50, 50, 50);
            volumeObj.transform.SetParent(thumbnailEditor.root.transform);
#endif

            thumbnailEditor.root.transform.position = new Vector3(10000, 10000, 10000);

            var children = thumbnailEditor.root.GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                child.gameObject.layer = PWBCore.staticData.thumbnailLayer;
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            foreach (var light in sceneLights.Keys) light.cullingMask = light.cullingMask & ~layerMask;

            for (int i = 0; i < 9; ++i) thumbnailEditor.camera.Render();

            foreach (var light in sceneLights.Keys) light.cullingMask = sceneLights[light];
#if PWB_HDRP  || PWB_URP
            foreach (var vol in sceneVolumes) vol.Key.gameObject.SetActive(vol.Value);
            foreach (var meshRenderer in meshRenderers) meshRenderer.Key.lightProbeUsage = meshRenderer.Value;
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                skinnedMeshRenderer.Key.lightProbeUsage = skinnedMeshRenderer.Value;
#endif

            RenderTextureToTexture2D(thumbnailEditor.camera.targetTexture, thumbnailTexture);

           RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbientLight;
            RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
            RenderSettings.ambientGroundColor = originalAmbientGroundColor;
            RenderSettings.ambientSkyColor = originalAmbientSkyColor;
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            RenderSettings.ambientProbe = originalAmbientProbe;
            RenderSettings.defaultReflectionMode = originalReflectionMode;
            RenderSettings.skybox = originalSkybox;
            RenderSettings.fog = originalFog;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogStartDistance = originalFogStartDistance;
            RenderSettings.fogEndDistance = originalFogEndDistance;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.haloStrength = originalHaloStrength;
            RenderSettings.flareFadeSpeed = originalFlareFadeSpeed;
            RenderSettings.flareStrength = originalFlareStrength;
            RenderSettings.reflectionIntensity = originalReflectionIntensity;
            RenderSettings.reflectionBounces = originalReflectionBounces;
            RenderSettings.defaultReflectionResolution = originalDefaultReflectionResolution;
            RenderSettings.subtractiveShadowColor = originalSubtractiveShadowColor;
            RenderSettings.sun = originalSun;

#if UNITY_2022_2_OR_NEWER
            RenderSettings.customReflectionTexture = originalReflectionTexture;
#else
            RenderSettings.customReflection = originalReflectionTexture;
#endif
            Object.DestroyImmediate(thumbnailEditor.root);
            if (savePng) SavePngResource(thumbnailTexture, thumbnailPath);
        }

        public static void UpdateThumbnail(ThumbnailSettings settings,
            Texture2D thumbnailTexture, Texture2D[] subThumbnails, string thumbnailPath, bool savePng)
        {
            if (subThumbnails.Length == 0)
            {
                thumbnailTexture.SetPixels(new Color[SIZE * SIZE]);
                thumbnailTexture.Apply();
                return;
            }
            var sqrt = Mathf.Sqrt(subThumbnails.Length);
            var sideCellsCount = Mathf.FloorToInt(sqrt);
            if (Mathf.CeilToInt(sqrt) != sideCellsCount) ++sideCellsCount;
            var spacing = (SIZE * sideCellsCount) / MIN_SIZE;
            var bigSize = SIZE * sideCellsCount + spacing * (sideCellsCount - 1);
            var texture = new Texture2D(bigSize, bigSize);
            var pixelCount = bigSize * bigSize;
            var pixels = new Color32[pixelCount];
            texture.SetPixels32(pixels);
            int subIdx = 0;
            for (int i = sideCellsCount - 1; i >= 0; --i)
            {
                for (int j = 0; j < sideCellsCount; ++j)
                {
                    var x = j * (SIZE + spacing);
                    var y = i * (SIZE + spacing);
                    if (subThumbnails[subIdx] == null) continue;
                    var subPixels = subThumbnails[subIdx].GetPixels32();
                    texture.SetPixels32(x, y, SIZE, SIZE, subPixels);
                    ++subIdx;
                    if (subIdx == subThumbnails.Length) goto Resize;
                }
            }
        Resize:
            texture.filterMode = FilterMode.Trilinear;
            texture.Apply();
            var renderTexture = new RenderTexture(SIZE, SIZE, 24);
            var prevActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Graphics.Blit(texture, renderTexture);
            thumbnailTexture.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0);
            thumbnailTexture.Apply();
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(texture);
            if (savePng) SavePngResource(thumbnailTexture, thumbnailPath);
        }

        public static void UpdateThumbnail(MultibrushItemSettings brushItem, bool savePng, bool updateParent)
        {
            if (brushItem.thumbnailSettings.useCustomImage)
            {
                brushItem.LoadThumbnailFromFile();
                return;
            }
            if (brushItem.prefab == null) return;
            UpdateThumbnail(brushItem.thumbnailSettings, brushItem.thumbnailTexture,
                brushItem.prefab, brushItem.thumbnailPath, savePng);
            if (updateParent)
                UpdateThumbnail(brushItem.parentSettings, updateItemThumbnails: false, savePng);
        }

        public static void UpdateThumbnail(MultibrushSettings brushSettings, bool updateItemThumbnails, bool savePng)
        {
            if (brushSettings.thumbnailSettings.useCustomImage) return;
            var brushItems = brushSettings.items;
            var subThumbnails = new System.Collections.Generic.List<Texture2D>();
            foreach (var item in brushItems)
            {
                if (updateItemThumbnails) UpdateThumbnail(item, savePng, updateParent: false);
                if (item.includeInThumbnail) subThumbnails.Add(item.thumbnail);
            }
            UpdateThumbnail(brushSettings.thumbnailSettings, brushSettings.thumbnailTexture,
                subThumbnails.ToArray(), brushSettings.thumbnailPath, savePng);
        }

        public static void UpdateThumbnail(BrushSettings brushItem, bool updateItemThumbnails, bool savePng)
        {
            if (brushItem is MultibrushItemSettings)
                UpdateThumbnail(brushItem as MultibrushItemSettings, savePng, updateParent: true);
            else if (brushItem is MultibrushSettings)
                UpdateThumbnail(brushItem as MultibrushSettings, updateItemThumbnails, savePng);
        }

        public static void DeleteUnusedThumbnails()
        {
            var palettes = PaletteManager.paletteData;
            bool CheckThumbnailPath(string thumbnailPath)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(thumbnailPath);
                var ids = fileName.Split('_');
                if (ids.Length > 2) return false;
                long itemId = -1;
                long brushId = -1;
                var provider = new System.Globalization.CultureInfo("en-US");
                if (!long.TryParse(ids[0], System.Globalization.NumberStyles.HexNumber, provider, out brushId)) return false;
                var brush = PaletteManager.GetBrushById(brushId);
                if (brush == null) return false;
                if (ids.Length == 1) return true;
                if (!long.TryParse(ids[1], System.Globalization.NumberStyles.HexNumber, provider, out itemId)) return false;
                return brush.ItemExist(itemId);
            }

            var folderPaths = PaletteManager.GetPaletteThumbnailFolderPaths();
            foreach (var folderPath in folderPaths)
            {
                var thumbnailPaths = System.IO.Directory.GetFiles(folderPath, "*.png");
                foreach (var thumbnailPath in thumbnailPaths)
                {
                    if (!CheckThumbnailPath(thumbnailPath))
                    {
                        System.IO.File.Delete(thumbnailPath);
                        var metapath = thumbnailPath + ".meta";
                        if (System.IO.File.Exists(metapath)) System.IO.File.Delete(metapath);
                        PWBCore.refreshDatabase = true;
                    }
                }
            }
        }
    }
}
