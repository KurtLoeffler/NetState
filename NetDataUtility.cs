using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NetState
{
	public static class NetDataUtility
	{
		[AttributeUsage(AttributeTargets.Field|AttributeTargets.Class|AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
		sealed class NetMaskableAttribute : Attribute
		{

		}

		public class Field
		{
			public FieldInfo fieldInfo;
			public int maskOffset;
		}
		public class TypeInfo
		{
			public Type type;
			public List<Field> fields = new List<Field>();
			public int maskableFieldCount;
		}
		public static TypeIDManager<INetData> typeIDManager = new TypeIDManager<INetData>();
		private static Dictionary<Type, TypeInfo> typeInfoDict = new Dictionary<Type, TypeInfo>();

		public delegate void SerializeFunctionDelegate(BinaryWriter writer, object value);
		public delegate object DeserializeFunctionDelegate(BinaryReader reader);
		private static Dictionary<Type, (SerializeFunctionDelegate serializer, DeserializeFunctionDelegate deserializer)> customSerializersDict = new Dictionary<Type, (SerializeFunctionDelegate serializer, DeserializeFunctionDelegate deserializer)>();

		private static Dictionary<Type, (SerializeFunctionDelegate serializer, DeserializeFunctionDelegate deserializer)> defaultCustomSerializers = new Dictionary<Type, (SerializeFunctionDelegate serializer, DeserializeFunctionDelegate deserializer)>
		{
			{ typeof(Vector2), ((writer, value) => { var v = (Vector2)value; writer.Write(v.x); writer.Write(v.y); }, (reader) => { return new Vector2(reader.ReadSingle(), reader.ReadSingle());}) },
			{ typeof(Vector3), ((writer, value) => { var v = (Vector3)value; writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }, (reader) => { return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());}) },
			{ typeof(Vector4), ((writer, value) => { var v = (Vector4)value; writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); writer.Write(v.w); }, (reader) => { return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());}) },

			{ typeof(Vector2Int), ((writer, value) => { var v = (Vector2Int)value; writer.Write(v.x); writer.Write(v.y); }, (reader) => { return new Vector2Int(reader.ReadInt32(), reader.ReadInt32());}) },
			{ typeof(Vector3Int), ((writer, value) => { var v = (Vector3Int)value; writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }, (reader) => { return new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());}) },

			{ typeof(Quaternion), ((writer, value) => { var v = (Quaternion)value; writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); writer.Write(v.w); }, (reader) => { return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());}) },
			{ typeof(Plane), ((writer, value) => { var v = (Plane)value; writer.Write(v.normal.x); writer.Write(v.normal.y); writer.Write(v.normal.z); writer.Write(v.distance); }, (reader) => { return new Plane(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle());}) },

			{ typeof(Color), ((writer, value) => { var v = (Color)value; writer.Write(v.r); writer.Write(v.g); writer.Write(v.b); writer.Write(v.a); }, (reader) => { return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());}) },
			{ typeof(Color32), ((writer, value) => { var v = (Color32)value; writer.Write(v.r); writer.Write(v.g); writer.Write(v.b); writer.Write(v.a); }, (reader) => { return new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());}) },
		};

		static NetDataUtility()
		{
			AddDefaultCustomSerializers();
		}

		private static TypeInfo GetTypeInfo(Type type)
		{
			if (!typeInfoDict.TryGetValue(type, out var typeInfo))
			{
				typeInfo = new TypeInfo();
				typeInfo.type = type;
				var fieldInfoArray = type.GetFields(BindingFlags.Instance|BindingFlags.Public);
				
				foreach (var fieldInfo in fieldInfoArray)
				{
					if (fieldInfo.GetCustomAttribute<NonSerializedAttribute>(true) != null)
					{
						continue;
					}
					bool isMaskable = fieldInfo.GetCustomAttribute<NetMaskableAttribute>(true) != null;

					int maskOffset = -1;
					if (isMaskable)
					{
						maskOffset = typeInfo.maskableFieldCount;
						typeInfo.maskableFieldCount++;
					}
					var field = new Field
					{
						fieldInfo = fieldInfo,
						maskOffset = maskOffset
					};
					typeInfo.fields.Add(field);
				}
				typeInfoDict.Add(type, typeInfo);
			}
			return typeInfo;
		}

		public static void AddCustomSerializer(Type type, SerializeFunctionDelegate serializer, DeserializeFunctionDelegate deserializer)
		{
			customSerializersDict[type] = (serializer, deserializer);
		}

		public static void RemoveCustomSerializer(Type type)
		{
			if (customSerializersDict.ContainsKey(type))
			{
				customSerializersDict.Remove(type);
			}
		}

		public static void AddDefaultCustomSerializers()
		{
			foreach (var pair in defaultCustomSerializers)
			{
				RemoveCustomSerializer(pair.Key);
				AddCustomSerializer(pair.Key, pair.Value.serializer, pair.Value.deserializer);
			}
		}

		public static void ClearCustomSerializers()
		{
			customSerializersDict.Clear();
		}

		/*
		public static INetData DeserializeNetData(BinaryReader reader)
		{
			int readTypeID = typeIDManager.PeekTypeID(reader);
			INetData data = typeIDManager.CreateInstance(readTypeID);
			DeserializeNetData(data, reader);
			return data;
		}

		public static void DeserializeNetData(INetData netData, BinaryReader reader)
		{
			int readTypeID = typeIDManager.ReadTypeID(reader);
			Type readType = typeIDManager.TypeIDToType(readTypeID);
			Type expectedType = netData.GetType();

			if (expectedType != readType)
			{
				throw new Exception($"Expected type {expectedType} but got type id {readTypeID} ({readType})");
			}

			DeserializeObject(netData, reader);
		}

		public static T DeserializeObject<T>(BinaryReader reader)
		{
			return (T)DeserializeObject(typeof(T), reader);
		}

		public static object DeserializeObject(Type type, BinaryReader reader)
		{
			object result = Activator.CreateInstance(type);
			DeserializeObject(result, reader);
			return result;
		}

		public static void DeserializeObject(object obj, BinaryReader reader)
		{
			Type type = obj.GetType();
			var typeInfo = GetTypeInfo(type);
			foreach (var field in typeInfo.fields)
			{
				field.fieldInfo.SetValue(obj, ReadValue(reader, field.fieldInfo.FieldType));
			}
		}
		*/

		public static void Serialize(object value, BinaryWriter writer)
		{
			Type type = value.GetType();
			
			if (type.IsPrimitive)
			{
				if (type == typeof(bool))
				{
					writer.Write((bool)value);
				}
				else if (type == typeof(byte))
				{
					writer.Write((byte)value);
				}
				else if (type == typeof(char))
				{
					writer.Write((char)value);
				}
				else if (type == typeof(decimal))
				{
					writer.Write((decimal)value);
				}
				else if (type == typeof(double))
				{
					writer.Write((double)value);
				}
				else if (type == typeof(short))
				{
					writer.Write((short)value);
				}
				else if (type == typeof(int))
				{
					writer.Write((int)value);
				}
				else if (type == typeof(long))
				{
					writer.Write((long)value);
				}
				else if (type == typeof(sbyte))
				{
					writer.Write((sbyte)value);
				}
				else if (type == typeof(float))
				{
					writer.Write((float)value);
				}
				else if (type == typeof(ushort))
				{
					writer.Write((ushort)value);
				}
				else if (type == typeof(uint))
				{
					writer.Write((uint)value);
				}
				else if (type == typeof(ulong))
				{
					writer.Write((ulong)value);
				}
			}
			else if (type == typeof(string))
			{
				writer.Write((string)value);
			}
			else if (customSerializersDict.TryGetValue(type, out var customSerializers))
			{
				customSerializers.serializer(writer, value);
			}
			else if (typeof(IList).IsAssignableFrom(type))
			{
				IList list = (IList)value;
				writer.Write(list.Count);
				foreach (var item in list)
				{
					Serialize(item, writer);
				}
			}
			else if (type.IsClass || type.IsValueType)
			{
				if (typeof(INetData).IsAssignableFrom(type))
				{
					typeIDManager.WriteID(writer, type);
				}

				WriteFields(value, writer);
			}
		}

		private static void WriteFields(object value, BinaryWriter writer)
		{
			var typeInfo = GetTypeInfo(value.GetType());

			foreach (var field in typeInfo.fields)
			{
				var fieldValue = field.fieldInfo.GetValue(value);
				Serialize(fieldValue, writer);
			}
		}

		public static void DeserializeOverwrite(INetData netData, BinaryReader reader)
		{
			int readTypeID = typeIDManager.ReadID(reader);
			Type readType = typeIDManager.IDToType(readTypeID);
			Type expectedType = netData.GetType();

			if (expectedType != readType)
			{
				throw new Exception($"Expected type {expectedType} but got type id {readTypeID} ({readType})");
			}

			DeserializeOverwrite(netData, reader);
		}

		public static void DeserializeOverwrite(object obj, BinaryReader reader)
		{
			Type type = obj.GetType();
			var typeInfo = GetTypeInfo(type);
			foreach (var field in typeInfo.fields)
			{
				field.fieldInfo.SetValue(obj, Deserialize(field.fieldInfo.FieldType, reader));
			}
		}

		public static T Deserialize<T>(BinaryReader reader)
		{
			return (T)Deserialize(typeof(T), reader);
		}

		public static object Deserialize(Type type, BinaryReader reader)
		{
			object output = null;

			if (type.IsPrimitive)
			{
				if (type == typeof(bool))
				{
					output = reader.ReadBoolean();
				}
				else if (type == typeof(byte))
				{
					output = reader.ReadByte();
				}
				else if (type == typeof(char))
				{
					output = reader.ReadChar();
				}
				else if (type == typeof(decimal))
				{
					output = reader.ReadDecimal();
				}
				else if (type == typeof(double))
				{
					output = reader.ReadDouble();
				}
				else if (type == typeof(short))
				{
					output = reader.ReadInt16();
				}
				else if (type == typeof(int))
				{
					output = reader.ReadInt32();
				}
				else if (type == typeof(long))
				{
					output = reader.ReadInt64();
				}
				else if (type == typeof(sbyte))
				{
					output = reader.ReadSByte();
				}
				else if (type == typeof(float))
				{
					output = reader.ReadSingle();
				}
				else if (type == typeof(ushort))
				{
					output = reader.ReadUInt16();
				}
				else if (type == typeof(uint))
				{
					output = reader.ReadUInt32();
				}
				else if (type == typeof(ulong))
				{
					output = reader.ReadUInt64();
				}
			}
			else if (type == typeof(string))
			{
				output = reader.ReadString();
			}
			else if (customSerializersDict.TryGetValue(type, out var customSerializers))
			{
				output = customSerializers.deserializer(reader);
			}
			else if (typeof(IList).IsAssignableFrom(type))
			{
				int count = reader.ReadInt32();

				IList list = (IList)Activator.CreateInstance(type);
				Type elementType = type.GetGenericArguments().Single();

				for (int i = 0; i < count; i++)
				{
					list.Add(Deserialize(elementType, reader));
				}
				output = list;
			}
			else if (type.IsClass || type.IsValueType)
			{
				
				if (typeof(INetData).IsAssignableFrom(type))
				{
					int typeID = typeIDManager.ReadID(reader);
					output = typeIDManager.CreateInstance(typeID);
				}
				else
				{
					output = Activator.CreateInstance(type);
				}

				DeserializeOverwrite(output, reader);
			}

			return output;
		}
	}
}
