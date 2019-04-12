using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NetState
{
	public class GenerateSerializersEditor : EditorWindow
	{
		[MenuItem("NetState/GenerateSerializers")]
		static void Generate()
		{
			string path = "Assets/NetStateGenerated/GeneratedSerializers.cs";
			SerializationGenerator.GenerateSerializers(path, new[] { typeof(Vector3), typeof(SerializationGenerator.TestStruct<int, string, Vector3>) });
			AssetDatabase.ImportAsset(path);
		}
		//[MenuItem("Window/My Window")]
		static void Init()
		{
			var window = GetWindow<GenerateSerializersEditor>();
			window.Show();
		}
	}
}
