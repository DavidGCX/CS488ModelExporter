using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.IO;

public class SceneTreeVisualizer : MonoBehaviour
{
    void Start()
    {
        Transform rootNode = this.transform;
        StringBuilder sb = new StringBuilder();

        // Generate the tree structure
        BuildTree(sb, rootNode, "", true);

        HandleTextFile.I.OutputModelInformation("tree", sb.ToString());
    }

    void BuildTree(StringBuilder sb, Transform node, string prefix, bool isLast)
    {
        sb.Append(prefix);
        sb.Append(isLast ? "└── " : "├── ");
        sb.Append(node.name);

        List<Transform> children = new List<Transform>();
        bool hasMeshChild = false;

        foreach (Transform child in node)
        {
            if (IsMeshNode(child))
            {
                hasMeshChild = true;
            }
            else
            {
                children.Add(child);
            }
        }
        sb.AppendLine();

        if (hasMeshChild && children.Count > 0)
        {
            sb.Append(prefix + (isLast ? "    " : "│   ") + "├── (related geometry)\n");
        }
        if (hasMeshChild && children.Count == 0)
        {
            sb.Append(prefix + (isLast ? "    " : "│   ") + "└── (related geometry)\n");
        }

        for (int i = 0; i < children.Count; i++)
        {
            BuildTree(sb, children[i], prefix + (isLast ? "    " : "│   "), i == children.Count - 1);
        }
    }

    bool IsMeshNode(Transform node)
    {
        return node.GetComponent<MeshRenderer>() != null || node.GetComponent<MeshFilter>() != null;
    }
}
