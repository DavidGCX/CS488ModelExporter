using UnityEngine;
using System.Collections.Generic;

public class SceneImporter : MonoBehaviour
{
    void Start()
    {
        Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
        Transform rootNode = new GameObject("rootNode").transform;
        nodes["rootNode"] = rootNode;

        // Materials
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        materials["red"] = CreateMaterial(new Color(1.0f, 0.0f, 0.0f));
        materials["blue"] = CreateMaterial(new Color(0.0f, 0.0f, 1.0f));
        materials["green"] = CreateMaterial(new Color(0.0f, 1.0f, 0.0f));
        materials["white"] = CreateMaterial(new Color(1.0f, 1.0f, 1.0f));
        materials["black"] = CreateMaterial(new Color(0.0f, 0.0f, 0.0f));
        materials["brown"] = CreateMaterial(new Color(0.8f, 0.5f, 0.25f));
        materials["darkbrown"] = CreateMaterial(new Color(0.21f, 0.14f, 0.02f));
        materials["slightDarkbrown"] = CreateMaterial(new Color(0.70f, 0.47f, 0.23f));
        materials["yellow"] = CreateMaterial(new Color(1.0f, 1.0f, 0.0f));

        // Torso
        Transform torso = CreateMesh("torso", nodes["rootNode"], materials["black"], new Vector3(0.8f, 0.9f, 0.5f), new Vector3(0.0f, -0.5f, -5.0f), PrimitiveType.Sphere);
        nodes["torso"] = torso;

        // Neck
        Transform neckNode = CreateNode("neckNode", torso);
        Transform neckJoint = CreateJoint("neckJoint", neckNode, new Vector3(-20, 0, 70), new Vector3(-110, 0, 110));
        Transform head = CreateMesh("head", neckJoint, materials["brown"], new Vector3(1.05f, 0.9f, 0.5f), new Vector3(0.0f, 0.7f, 0.0f), PrimitiveType.Sphere);
        nodes["head"] = head;

        // Facial Features
        CreateMesh("leftEye", head, materials["black"], new Vector3(0.08f, 0.08f, 0.01f), new Vector3(-0.17f, 0.15f, 0.98f), PrimitiveType.Sphere);
        CreateMesh("rightEye", head, materials["black"], new Vector3(0.08f, 0.08f, 0.01f), new Vector3(0.17f, 0.15f, 0.98f), PrimitiveType.Sphere);
        CreateMesh("nose", head, materials["black"], new Vector3(0.08f, 0.09f, 0.01f), new Vector3(0, -0.1f, 1.0f), PrimitiveType.Sphere);

        // Ears
        Transform leftEarNode = CreateNode("leftEarNode", head);
        Transform leftEarJoint = CreateJoint("leftEarJoint", leftEarNode, new Vector3(-30, 0, 30), new Vector3(0, 0, 0));
        CreateMesh("leftEar", leftEarJoint, materials["brown"], new Vector3(0.32f, 0.3f, 0.1f), Vector3.zero, PrimitiveType.Sphere);

        Transform rightEarNode = CreateNode("rightEarNode", head);
        Transform rightEarJoint = CreateJoint("rightEarJoint", rightEarNode, new Vector3(-30, 0, 30), new Vector3(0, 0, 0));
        CreateMesh("rightEar", rightEarJoint, materials["brown"], new Vector3(0.32f, 0.3f, 0.1f), Vector3.zero, PrimitiveType.Sphere);

        // Arms
        Transform leftUpperArmNode = CreateNode("leftUpperArmNode", torso);
        Transform leftUpperArmJoint = CreateJoint("leftUpperArmJoint", leftUpperArmNode, new Vector3(-60, 0, 60), new Vector3(-30, 0, 30));
        CreateMesh("leftUpperArm", leftUpperArmJoint, materials["blue"], new Vector3(0.3f, 0.4f, 0.2f), new Vector3(-0.4f, 0.0f, 0.0f), PrimitiveType.Sphere);

        // Hands
        Transform leftHandNode = CreateNode("leftHandNode", leftUpperArmJoint);
        Transform leftHandJoint = CreateJoint("leftHandJoint", leftHandNode, new Vector3(-15, 0, 15), new Vector3(0, 0, 0));
        CreateMesh("leftHand", leftHandJoint, materials["brown"], new Vector3(0.3f, 0.3f, 0.2f), new Vector3(0.0f, -0.2f, 0.0f), PrimitiveType.Sphere);
    }

    Transform CreateNode(string name, Transform parent)
    {
        Transform node = new GameObject(name).transform;
        node.SetParent(parent);
        return node;
    }

    Transform CreateJoint(string name, Transform parent, Vector3 jointX, Vector3 jointY)
    {
        Transform joint = new GameObject(name).transform;
        joint.SetParent(parent);
        JointNode jointComponent = joint.gameObject.AddComponent<JointNode>();
        jointComponent.joint_X = jointX;
        jointComponent.joint_Y = jointY;
        return joint;
    }

    Transform CreateMesh(string name, Transform parent, Material material, Vector3 scale, Vector3 position, PrimitiveType meshType)
    {
        Transform mesh = GameObject.CreatePrimitive(meshType).transform;
        mesh.name = name;
        mesh.SetParent(parent);
        mesh.localScale = scale * 2;
        mesh.localPosition = new Vector3(-position.x, position.y, position.z);
        mesh.GetComponent<Renderer>().material = material;
        return mesh;
    }

    Material CreateMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        return mat;
    }
}
