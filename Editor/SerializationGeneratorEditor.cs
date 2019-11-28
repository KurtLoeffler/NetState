using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace NetState
{

	public class EditorSerializerGeneratorManager
	{
		public const string path = "Assets/NetState.Generated/TypeSerializers.cs";
		public const string compileFailDummyPath = "Assets/NetState.Generated/compilefailed";
		public static bool autoGenerate
		{
			get
			{
				return EditorPrefs.GetBool("NetState.GenerateSerializersEditor.autoGenerate", true);
			}
			set
			{
				EditorPrefs.SetBool("NetState.GenerateSerializersEditor.autoGenerate", value);
			}
		}

		public static bool deleteOnError
		{
			get
			{
				return EditorPrefs.GetBool("NetState.GenerateSerializersEditor.deleteOnError", true);
			}
			set
			{
				EditorPrefs.SetBool("NetState.GenerateSerializersEditor.deleteOnError", value);
			}
		}

		[MenuItem("NetState/Auto Generate")]
		public static void AutoGenerateMenuItem()
		{
			autoGenerate = !autoGenerate;
			Menu.SetChecked("NetState/Auto Generate", autoGenerate);
		}

		[MenuItem("NetState/Delete On Error")]
		public static void DeleteOnErrorMenuItem()
		{
			deleteOnError = !deleteOnError;
			Menu.SetChecked("NetState/Delete On Error", deleteOnError);
		}

		[MenuItem("NetState/Regenerate Serializers")]
		public static void ForceGenerate()
		{
			if (File.Exists(path))
			{
				AssetDatabase.DeleteAsset(path);
			}
			Generate();
		}

		public static void Generate()
		{

			string source = SerializationGenerator.GenerateSerializers();
			/*
			bool hasChanged = true;
			if (File.Exists(path))
			{
				string existingSource = File.ReadAllText(path);
				if (source != existingSource)
				{
					hasChanged = true;
				}
			}

			if (hasChanged)
			{
				if (File.Exists(path))
				{

				}
			}
			*/
			string basePath = new DirectoryInfo(path).Parent.FullName;
			if (!Directory.Exists(basePath))
			{
				Directory.CreateDirectory(basePath);
			}

			File.WriteAllText(path, source);

			/*
			List<string> compilationErrors = new List<string>();
			void HandleLog(string logString, string stackTrace, LogType type)
			{
				if (type == LogType.Error)
				{
					if (logString.Replace("\\", "/").ToLower().StartsWith(path.ToLower()))
					{
						compilationErrors.Add(logString);
					}
				}
			}
			
			void EditorUpdate()
			{
				if (EditorApplication.isCompiling)
				{
					return;
				}

				//Application.logMessageReceived -= HandleLog;
				EditorApplication.update -= EditorUpdate;

				if (compilationErrors.Count > 0)
				{
					foreach (var error in compilationErrors)
					{
						Debug.LogError(error);
					}
					Debug.LogWarning($"Deleting generated netstate serializers \"{path}\" because it contained compilation errors.");
					AssetDatabase.DeleteAsset(path);
					AssetDatabase.Refresh();
					File.WriteAllText(compileFailDummyPath, "");
				}
			}

			EditorApplication.update += EditorUpdate;
			//Application.logMessageReceived += HandleLog;
			*/
			AssetDatabase.Refresh();
		}

		[InitializeOnLoadMethod]
		private static void StaticInitialize()
		{
			EditorApplication.delayCall += () =>
			{
				Menu.SetChecked("NetState/Auto Generate", autoGenerate);
			};

			EditorApplication.delayCall += () =>
			{
				Menu.SetChecked("NetState/Delete On Error", deleteOnError);
			};

			Application.logMessageReceived -= OnlogMessageReceived;
			Application.logMessageReceived += OnlogMessageReceived;
		}

		private static void OnlogMessageReceived(string condition, string stackTrace, LogType type)
		{
			if (type == LogType.Error)
			{
				if (File.Exists(path) && condition.Replace("\\", "/").ToLower().StartsWith(path.ToLower()))
				{
					if (deleteOnError)
					{
						Debug.LogWarning($"Deleting generated netstate serializers ({path}) because it contained compilation errors.");
						AssetDatabase.DeleteAsset(path);
					}
					
					if (autoGenerate)
					{
						AssetDatabase.Refresh();

						//File.WriteAllText(compileFailDummyPath, "");
					}
				}
			}
		}

		[UnityEditor.Callbacks.DidReloadScripts]
		private static void OnScriptsReloaded()
		{
			if (autoGenerate)
			{
				Generate();
			}
			/*
			if (File.Exists(compileFailDummyPath))
			{
				File.Delete(compileFailDummyPath);
			}
			else
			{
				if (autoGenerate)
				{
					Generate();
				}
			}
			*/
		}
	}

	public class SerializationGeneratorEditor : EditorWindow
	{
		//[MenuItem("Window/My Window")]
		static void Init()
		{
			var window = GetWindow<SerializationGeneratorEditor>();
			window.Show();
		}
	}
}
