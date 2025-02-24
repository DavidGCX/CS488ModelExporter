using System;
using UnityEngine;
using UnityEditor;
using System.IO;
public class HandleTextFile : MonoBehaviour{
    public static HandleTextFile I;

    private void Awake() {
        I = this;
    }

    public void OutputModelInformation(string modelName, string modelInformation){
        string path = "Assets/Resources/" + modelName + ".txt";
        //Write some text to the test.txt file
        StreamWriter writer = new StreamWriter(path);
        writer.WriteLine(modelInformation);
        writer.Close();
    }

}
