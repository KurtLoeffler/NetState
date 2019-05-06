using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;
using System.Text;

namespace NetState
{
	[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
	public sealed class GenerateNetSerializerAttribute : Attribute
	{

	}

	public static class SerializationGenerator
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct Field
		{
			public enum DataType
			{
				None,
				Class,
				Struct,
				Primitive
			}
			public Type type => fieldInfo.FieldType;
			public DataType dataType;
			public FieldInfo fieldInfo;
			public int maskIndex;
		}
		//[GenerateNetSerializer]
		public struct TestStruct<A, B, C>
		{
			public int test;
			public A aInstance;
			public List<Vector3> vectors;
		}

		public class TypeInfo
		{
			public Type type;
			public string qualifiedName;
			public List<Field> fields = new List<Field>();
		}

		public static TypeIDManager<INetData> typeIDManager = new TypeIDManager<INetData>();
		private static Dictionary<Type, TypeInfo> fieldLookupDict = new Dictionary<Type, TypeInfo>();

		private static TypeInfo GetTypeInfo(Type type)
		{
			if (!fieldLookupDict.TryGetValue(type, out var typeInfo))
			{
				typeInfo = new TypeInfo();
				typeInfo.type = type;

				typeInfo.qualifiedName = GetCSharpName(type);

				var fieldInfoArray = type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.FlattenHierarchy);
				foreach (var fieldInfo in fieldInfoArray)
				{
					if (fieldInfo.GetCustomAttribute<NonSerializedAttribute>(true) != null)
					{
						continue;
					}
					
					var dataType = Field.DataType.Struct;
					if (fieldInfo.FieldType.IsPrimitive)
					{
						dataType = Field.DataType.Primitive;
					}
					else if (fieldInfo.FieldType.IsClass)
					{
						dataType = Field.DataType.Class;
					}

					var field = new Field
					{
						dataType = dataType,
						fieldInfo = fieldInfo,
						maskIndex = -1
					};

					typeInfo.fields.Add(field);
				}
				fieldLookupDict.Add(type, typeInfo);
			}
			return typeInfo;
		}

		public static string GenerateSerializers(IEnumerable<Type> additionalTypes = null)
		{

			HashSet<Type> allTypes = new HashSet<Type>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					foreach (Type type in assembly.GetTypes())
					{
						try
						{
							if (type.IsInterface)
							{
								continue;
							}
							if (type.IsAbstract)
							{
								continue;
							}
							if (type.GetCustomAttribute<GenerateNetSerializerAttribute>(true) == null)
							{
								if (!typeof(INetData).IsAssignableFrom(type) && !typeof(NetPacket).IsAssignableFrom(type))
								{
									continue;
								}
							}

							allTypes.Add(type);
						}
						catch { }
					}
				}
				catch { }
			}

			if (additionalTypes != null)
			{
				foreach (var type in additionalTypes)
				{
					if (!allTypes.Contains(type))
					{
						allTypes.Add(type);
					}
				}
			}

			var referencedTypes = new HashSet<Type>();
			foreach (var type in allTypes)
			{
				FindReferencedTypes(type, referencedTypes);
			}

			foreach (var type in referencedTypes)
			{
				if (!allTypes.Contains(type))
				{
					allTypes.Add(type);
				}
			}

			//Create collection variants of all non collection types.
			var allTypesCopy = allTypes.ToArray();
			foreach (var type in allTypesCopy)
			{
				if (typeof(IList).IsAssignableFrom(type))
				{
					continue;
				}
				var listType = typeof(List<>).MakeGenericType(type);
				if (!allTypes.Contains(listType))
				{
					allTypes.Add(listType);
				}
			}

			allTypes.Remove(typeof(string));

			var writer = new StringWriter();
			writer.Write(@"
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

");
			

			int indent = 0;
			WriteIndent(indent, writer);
			writer.WriteLine("namespace NetState.Generated {");

			indent++;

			WriteIndent(indent, writer);
			writer.WriteLine("public class TypeSerializers : NetState.TypeSerializers {");

			indent++;

			foreach (var type in allTypes)
			{
				CreateSerializerFunctions(GetTypeInfo(type), indent, writer);
			}

			indent--;

			WriteIndent(indent, writer);
			writer.WriteLine("}");

			indent--;

			WriteIndent(indent, writer);
			writer.WriteLine("}");

			var output = writer.ToString();
			output = output.Replace("\r\n", "\n");

			return output;
		}

		public static void FindReferencedTypes(Type type, HashSet<Type> referencedTypes)
		{
			if (type.IsPrimitive)
			{
				return;
			}

			if (!referencedTypes.Contains(type))
			{
				referencedTypes.Add(type);
			}

			var typeInfo = GetTypeInfo(type);
			foreach (var field in typeInfo.fields)
			{
				if (referencedTypes.Contains(field.type))
				{
					continue;
				}
				FindReferencedTypes(field.type, referencedTypes);
			}
		}

		public static void CreateSerializerFunctions(TypeInfo typeInfo, int indent, StringWriter writer)
		{
			WriteIndent(indent, writer);
			writer.WriteLine($"public static void Serialize({typeInfo.qualifiedName} value, BinaryWriter writer) {{");

			indent++;

			if (typeof(INetData).IsAssignableFrom(typeInfo.type))
			{
				WriteIndent(indent, writer);
				writer.WriteLine($"if (value.GetType() != typeof({typeInfo.qualifiedName})) {{");
				indent++;

				WriteIndent(indent, writer);
				writer.WriteLine("NetSerialization.Serialize((object)value, writer);");
				WriteIndent(indent, writer);
				writer.WriteLine($"return;");

				indent--;
				WriteIndent(indent, writer);
				writer.WriteLine($"}}");

				
			}

			foreach (var field in typeInfo.fields)
			{
				WriteSerializeCode(field, indent, writer);
			}
			if (typeof(IList).IsAssignableFrom(typeInfo.type))
			{
				var elementType = typeInfo.type.GenericTypeArguments[0];
				var elementTypeName = GetCSharpName(elementType);
				//WriteIndent(indent, writer);
				//writer.WriteLine($"var list = (IList<{elementTypeName}>)value;");
				WriteIndent(indent, writer);
				writer.WriteLine("writer.Write((ushort)value.Count);");
				WriteIndent(indent, writer);
				writer.WriteLine("foreach (var item in value) {");
				indent++;

				if (elementType.IsPrimitive || elementType == typeof(string))
				{
					WriteIndent(indent, writer);
					string functionName = TypeToWriteFunctionName(elementType);
					writer.WriteLine($"writer.{functionName}(item);");
				}
				else
				{
					WriteIndent(indent, writer);
					writer.WriteLine($"Serialize(item, writer);");
				}

				//WriteIndent(indent, writer);
				//writer.WriteLine("Serialize(item, writer);");

				indent--;
				WriteIndent(indent, writer);
				writer.WriteLine("}");
			}

			indent--;

			WriteIndent(indent, writer);
			writer.WriteLine("}");

			WriteIndent(indent, writer);
			writer.WriteLine($"public static void Deserialize(ref {typeInfo.qualifiedName} value, BinaryReader reader) {{");
			
			indent++;

			if (typeof(INetData).IsAssignableFrom(typeInfo.type))
			{
				WriteIndent(indent, writer);
				writer.WriteLine($"if (value.GetType() != typeof({typeInfo.qualifiedName})) {{");
				indent++;

				WriteIndent(indent, writer);
				writer.WriteLine($"value = ({typeInfo.qualifiedName})NetSerialization.Deserialize((object)value, reader);");
				WriteIndent(indent, writer);
				writer.WriteLine($"return;");

				indent--;
				WriteIndent(indent, writer);
				writer.WriteLine($"}}");
			}

			foreach (var field in typeInfo.fields)
			{
				WriteDeserializeCode(field, indent, writer);
			}
			if (typeof(IList).IsAssignableFrom(typeInfo.type))
			{
				var elementType = typeInfo.type.GenericTypeArguments[0];
				var elementTypeName = GetCSharpName(elementType);

				WriteIndent(indent, writer);
				writer.WriteLine("var count = reader.ReadUInt16();");

				WriteIndent(indent, writer);
				writer.WriteLine("for (int i = 0; i < count; i++) {");
				indent++;

				if (elementType.IsPrimitive || elementType == typeof(string))
				{
					WriteIndent(indent, writer);
					writer.WriteLine($"var element = reader.Read{elementType.Name}();");
				}
				else
				{
					if (typeof(INetData).IsAssignableFrom(elementType))
					{
						WriteIndent(indent, writer);
						writer.WriteLine($"var element = NetSerialization.Deserialize<{elementTypeName}>(reader);");
					}
					else
					{
						WriteIndent(indent, writer);
						writer.WriteLine($"var element = new {elementTypeName}();");

						WriteIndent(indent, writer);
						writer.WriteLine($"Deserialize(ref element, reader);");
					}
				}

				WriteIndent(indent, writer);
				writer.WriteLine($"value.Add(element);");
				indent--;
				WriteIndent(indent, writer);
				writer.WriteLine("}");
			}

			indent--;

			WriteIndent(indent, writer);
			writer.WriteLine("}");
			writer.WriteLine("");
		}

		private static void WriteSerializeCode(Field field, int indent, StringWriter writer)
		{
			if (field.dataType == Field.DataType.Primitive || field.type == typeof(string))
			{
				WriteIndent(indent, writer);
				string functionName = TypeToWriteFunctionName(field.type);
				writer.WriteLine($"writer.{functionName}(value.{field.fieldInfo.Name});");
			}
			else
			{
				WriteIndent(indent, writer);
				writer.WriteLine($"Serialize(value.{field.fieldInfo.Name}, writer);");
			}
		}
		
		private static void WriteDeserializeCode(Field field, int indent, StringWriter writer)
		{
			if (field.dataType == Field.DataType.Primitive || field.type == typeof(string))
			{
				WriteIndent(indent, writer);
				writer.WriteLine($"value.{field.fieldInfo.Name} = reader.Read{field.type.Name}();");
			}
			else
			{
				var fieldTypeName = GetCSharpName(field.type);

				if (typeof(INetData).IsAssignableFrom(field.type))
				{
					WriteIndent(indent, writer);
					writer.WriteLine($"value.{field.fieldInfo.Name} = NetSerialization.Deserialize<{fieldTypeName}>(reader);");
				}
				else
				{
					WriteIndent(indent, writer);
					writer.WriteLine($"value.{field.fieldInfo.Name} = new {fieldTypeName}();");

					WriteIndent(indent, writer);
					writer.WriteLine($"Deserialize(ref value.{field.fieldInfo.Name}, reader);");
				}
			}
		}

		private static string TypeToWriteFunctionName(Type type)
		{
			string functionName = "Write";
			if (type == typeof(float))
			{
				functionName = "WriteSingle";
			}
			else if (type == typeof(double))
			{
				functionName = "WriteDouble";
			}
			return functionName;
		}

		private static void WriteIndent(int indent, StringWriter writer)
		{
			for (int i = 0; i < indent; i++)
			{
				writer.Write("\t");
			}
		}

		public static string GetCSharpName(Type type)
		{
			StringBuilder sb = new StringBuilder();
			sb.Insert(0, GetCSharpTypeName(type));
			while (type.IsNested)
			{
				type = type.DeclaringType;
				sb.Insert(0, GetCSharpTypeName(type) + ".");
			}
			if (!string.IsNullOrEmpty(type.Namespace))
			{
				sb.Insert(0, type.Namespace + ".");
			}
			return sb.ToString();
		}

		private static string GetCSharpTypeName(Type type)
		{

			if (type.IsGenericTypeDefinition || type.IsGenericType)
			{
				StringBuilder sb = new StringBuilder();
				int cut = type.Name.IndexOf('`');
				sb.Append(cut > 0 ? type.Name.Substring(0, cut) : type.Name);

				Type[] genArgs = type.GetGenericArguments();
				if (genArgs.Length > 0)
				{
					sb.Append('<');
					for (int i = 0; i < genArgs.Length; i++)
					{
						//Debug.Log(genArgs[i].generic);
						//throw new Exception();
						//if (!type.IsGenericTypeDefinition)
						{
							sb.Append(GetCSharpName(genArgs[i]));
						}
						if (i < genArgs.Length-1)
						{
							sb.Append(", ");
						}
					}
					sb.Append('>');
				}
				return sb.ToString();
			}
			else
			{
				return type.Name;
			}
		}
	}

}
