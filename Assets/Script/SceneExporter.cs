using UnityEngine;
using System.Text;

public class SceneExporter : MonoBehaviour
{
    void Start()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("rootNode = gr.node('root')");

        Transform root = transform;
        foreach (Transform child in root)
        {
            AppendNode(sb, child, "rootNode");
        }
        Vector3 pos = root.localPosition;
        sb.AppendLine($"rootNode:translate({-pos.x:F3}, {pos.y:F3}, {pos.z:F3})");

        sb.AppendLine("\nreturn rootNode");

        HandleTextFile.I.OutputModelInformation("model.lua", sb.ToString());
        Debug.Log(sb.ToString());
    }

    void AppendNode(StringBuilder sb, Transform obj, string parentName)
    {
        string nodeName = obj.name;
        Renderer renderer = obj.GetComponent<Renderer>();
        JointNode jointNode = obj.GetComponent<JointNode>();
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        string meshType = "sphere";
        sb.AppendLine("--------------------" + nodeName + "--------------------");
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            if (meshFilter.sharedMesh.name.ToLower().Contains("cube"))
            {
                meshType = "cube";
            }
        }
        // Scale
        Vector3 scale = obj.localScale;
        if (renderer != null)
        {
            // Mesh Node
            string materialName = nodeName + "_mat";
            Color color = renderer.material.color;
            sb.AppendLine($"{materialName} = gr.material({{ {color.r:F3}, {color.g:F3}, {color.b:F3} }}, {{ 0.1, 0.1, 0.1 }}, 10)");
            sb.AppendLine($"{nodeName} = gr.mesh('{meshType}', '{nodeName}')");
            sb.AppendLine($"{nodeName}:set_material({materialName})");
            if (meshType == "sphere") {
                scale *= 0.5f;
            }
        }
        else if (jointNode != null)
        {
            // Joint Node
            sb.AppendLine($"{nodeName} = gr.joint('{nodeName}', {{ {jointNode.joint_X.x:F3}, {jointNode.joint_X.y:F3}, {jointNode.joint_X.z:F3} }}, {{ {jointNode.joint_Y.x:F3}, {jointNode.joint_Y.y:F3}, {jointNode.joint_Y.z:F3} }})");
        }
        else
        {
            // Generic Node
            sb.AppendLine($"{nodeName} = gr.node('{nodeName}')");
        }

        sb.AppendLine($"{parentName}:add_child({nodeName})");



        sb.AppendLine($"{nodeName}:scale({scale.x:F3}, {scale.y:F3}, {scale.z:F3})");

        // Rotation
        Vector3 rotation = obj.localRotation.eulerAngles;
        if (Mathf.Abs(rotation.x) > 0.01f)
            sb.AppendLine($"{nodeName}:rotate('x', {rotation.x:F3})");
        if (Mathf.Abs(rotation.y) > 0.01f)
            sb.AppendLine($"{nodeName}:rotate('y', {-rotation.y:F3})");
        if (Mathf.Abs(rotation.z) > 0.01f)
            sb.AppendLine($"{nodeName}:rotate('z', {-rotation.z:F3})");

        // Translation (Invert X-axis)
        Vector3 pos = obj.localPosition;
        sb.AppendLine($"{nodeName}:translate({-pos.x:F3}, {pos.y:F3}, {pos.z:F3})");
        sb.AppendLine("----------------------------------------\n");

        // Recursively process children
        foreach (Transform child in obj)
        {
            AppendNode(sb, child, nodeName);
        }
    }
}
