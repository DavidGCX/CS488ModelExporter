using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.IO;

public class SceneExporterMatrixPBR : MonoBehaviour {
    [Header("Export Settings")]
    public string outputFileName = "exported_scene.lua";
    public int imageWidth = 800;
    public int imageHeight = 600;
    public Color ambientColor = new Color(0.4f, 0.4f, 0.4f);

    // Folder where exported .obj mesh files reside.
    public string meshFolder = "Meshes/";
    // New folder for textures (URP/Lit materials use _BaseMap as the main texture).
    public string textureFolder = "Textures/";

    // Threshold to decide if a material is “metallic” enough to use reflective shading.
    public float metallicThreshold = 0.1f;
    Dictionary<Material, string> materialDict = new Dictionary<Material, string>();
    public void ExportScene() {
        StringBuilder lua = new StringBuilder();

        // --- Export Materials ---
        Dictionary<Material, string> materialDict = new Dictionary<Material, string>();
        int matCounter = 0;
        MeshRenderer[] renderers = GameObject.FindObjectsOfType<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers) {
            Material mat = renderer.sharedMaterial;
            if (mat != null && !materialDict.ContainsKey(mat)) {
                string matName = "mat" + matCounter;
                materialDict[mat] = matName;

                // For URP/Lit materials, the main texture is stored in _BaseMap.
                Color albedo = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;

                // Convert to Phong-style parameters.
                Color diffuse = albedo;
                Color specular = Color.Lerp(new Color(0.2f, 0.2f, 0.2f), albedo, metallic);
                float shininess = Mathf.Lerp(10f, 100f, smoothness);

                // Check for main texture in URP/Lit: _BaseMap is the property.
                Texture mainTex = null;
                if(mat.HasProperty("_BaseMap"))
                    mainTex = mat.GetTexture("_BaseMap");
                // Also check _MainTex as a fallback.
                else if(mat.HasProperty("_MainTex"))
                    mainTex = mat.GetTexture("_MainTex");

                // Check for normal map (URP/Lit typically uses _BumpMap).
                Texture normalTex = null;
                if(mat.HasProperty("_BumpMap"))
                    normalTex = mat.GetTexture("_BumpMap");
                else if(mat.HasProperty("_NormalMap"))
                    normalTex = mat.GetTexture("_NormalMap");

                bool isTextureMaterial = (mainTex != null);

                // Debug log for verification.
                Debug.Log(string.Format("Material {0} - Diffuse: {1:F2}, {2:F2}, {3:F2}; Specular: {4:F2}, {5:F2}, {6:F2}; Shininess: {7:F2}; Texture: {8}; NormalMap: {9}",
                    matName,
                    diffuse.r, diffuse.g, diffuse.b,
                    specular.r, specular.g, specular.b,
                    shininess,
                    isTextureMaterial ? mainTex.name : "None",
                    normalTex != null ? normalTex.name : "None"));

                if (isTextureMaterial) {
                    // Build filenames from textureFolder. We assume textures are stored as PNG.
                    string texFileName = textureFolder + mainTex.name + ".png";
                    string normalFileName = (normalTex != null) ? textureFolder + normalTex.name + ".png" : "";
                    lua.AppendFormat("{0} = gr.texture_material({{{1:F2}, {2:F2}, {3:F2} }}, {{{4:F2}, {5:F2}, {6:F2} }}, {7:F2}, '{8}', '{9}')\n",
                        matName,
                        diffuse.r, diffuse.g, diffuse.b,
                        specular.r, specular.g, specular.b,
                        shininess,
                        texFileName,
                        normalFileName);
                } else {
                    // Fallback to non-textured material.
                    if (metallic > metallicThreshold) {
                        lua.AppendFormat("{0} = gr.materialR({{{1:F2}, {2:F2}, {3:F2} }}, {{{4:F2}, {5:F2}, {6:F2} }}, {7:F2}, {8:F2})\n",
                            matName,
                            diffuse.r, diffuse.g, diffuse.b,
                            specular.r, specular.g, specular.b,
                            shininess,
                            metallic);
                    } else {
                        lua.AppendFormat("{0} = gr.material({{{1:F2}, {2:F2}, {3:F2} }}, {{{4:F2}, {5:F2}, {6:F2} }}, {7:F2})\n",
                            matName,
                            diffuse.r, diffuse.g, diffuse.b,
                            specular.r, specular.g, specular.b,
                            shininess);
                    }
                }
                matCounter++;
            }
        }
        lua.Append("\n");

        // --- Create the Root Scene Node ---
        lua.Append("scene = gr.node('scene')\n\n");

        // --- Export Objects ---
        // Create a flip matrix for mirroring on the x-axis.

        foreach (MeshRenderer renderer in renderers) {
            GameObject go = renderer.gameObject;
            string nodeName = go.name.Replace(" ", "_");
            nodeName = nodeName.Replace("(", "_");
            nodeName = nodeName.Replace(")", "_");
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) {
                string meshName = mf.sharedMesh.name;
                string objFileName = meshFolder + meshName + ".obj";
                lua.AppendFormat("{0} = gr.mesh('{0}', '{1}')\n", nodeName, objFileName);
            } else {
                lua.AppendFormat("{0} = gr.node('{0}')\n", nodeName);
            }
            Material mat = renderer.sharedMaterial;
            if (mat != null && materialDict.ContainsKey(mat)) {
                string matName = materialDict[mat];
                lua.AppendFormat("{0}:set_material({1})\n", nodeName, matName);
            }

            // Export the object's full transformation matrix (with x-axis flip).
            go.transform.localScale = new Vector3(-go.transform.localScale.x, go.transform.localScale.y, go.transform.localScale.z);
            Matrix4x4 m =  go.transform.localToWorldMatrix;
            go.transform.localScale = new Vector3(-go.transform.localScale.x, go.transform.localScale.y, go.transform.localScale.z);
            lua.AppendFormat("{0}:set_matrix({{", nodeName);
            for (int col = 0; col < 4; col++) {
                for (int row = 0; row < 4; row++) {
                    lua.AppendFormat("{0:F4}", m[row, col]);
                    if (!(col == 3 && row == 3))
                        lua.Append(", ");
                }
            }
            lua.Append("})\n");

            lua.AppendFormat("scene:add_child({0})\n\n", nodeName);
        }

        // --- Export Lights ---
        lua.Append("lights = {\n");
        Light[] lights = GameObject.FindObjectsOfType<Light>();
        foreach (Light light in lights) {
            if (light.type != LightType.Point)
                continue;
            Vector3 lpos = light.transform.position;
            Color lcol = light.color * light.intensity;
            lua.AppendFormat("  gr.light({{{0:F2}, {1:F2}, {2:F2} }}, {{{3:F2}, {4:F2}, {5:F2} }}, {{{6}, {7}, {8}}}),\n",
                lpos.x, lpos.y, lpos.z,
                lcol.r, lcol.g, lcol.b,
                1, 0, 1);
        }
        lua.Append("}\n\n");

        // --- Export Camera & Render Call ---
        Camera cam = Camera.main;
        if (cam != null) {
            Vector3 eye = cam.transform.position;
            Vector3 view = cam.transform.forward;
            Vector3 up = cam.transform.up;
            float fov = cam.fieldOfView;
            lua.AppendFormat("gr.render(scene, 'render.png', {0}, {1}, {{{2:F2}, {3:F2}, {4:F2} }}, {{{5:F2}, {6:F2}, {7:F2} }}, {{{8:F2}, {9:F2}, {10:F2} }}, {11:F2}, {{{12:F2}, {13:F2}, {14:F2} }}, lights)\n",
                imageWidth, imageHeight,
                eye.x, eye.y, eye.z,
                view.x, view.y, view.z,
                up.x, up.y, up.z,
                fov,
                ambientColor.r, ambientColor.g, ambientColor.b);
        } else {
            Debug.LogWarning("No main camera found. Camera parameters not exported.");
        }

        // --- Write the Lua file ---
        // Export directly to C:\Users\GCX\Desktop\cs488
        string filePath = Path.Combine(@"C:\Users\GCX\Desktop\cs488\A5Project", outputFileName);
        File.WriteAllText(filePath, lua.ToString());
        Debug.Log("Scene exported to " + filePath);
    }

    public void RecordTransforms() {

    }

    // Export on Start (or attach ExportScene() to a UI button)
    void Start() {
        ExportScene();
    }
}
