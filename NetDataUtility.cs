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
		public static TypeIDManager<INetData> typeIDManager = new TypeIDManager<INetData>();
		private static Dictionary<Type, List<FieldInfo>> typeToFieldInfoDict = new Dictionary<Type, List<FieldInfo>>();

		private static List<FieldInfo> GetSerializedFieldInfos(Type type)
		{
			if (!typeToFieldInfoDict.TryGetValue(type, out var fieldInfos))
			{
				fieldInfos = new List<FieldInfo>();
				var fieldInfoArray = type.GetFields(BindingFlags.Instance|BindingFlags.Public);
				foreach (var field in fieldInfoArray)
				{
					if (field.GetCustomAttribute<NonSerializedAttribute>(true) == null)
					{
						fieldInfos.Add(field);
					}
				}
				typeToFieldInfoDict.Add(type, fieldInfos);
			}
			return fieldInfos;
		}
		public static void Serialize(INetData netData, BinaryWriter writer)
		{
			Type type = netData.GetType();
			ushort typeID = (ushort)typeIDManager.TypeToTypeID(type);
			writer.Write(typeID);

			SerializeObject(netData, writer);
		}

		public static void SerializeObject(object obj, BinaryWriter writer)
		{
			Type type = obj.GetType();
			var fieldInfos = GetSerializedFieldInfos(type);

			foreach (var field in fieldInfos)
			{
				var value = field.GetValue(obj);
				WriteValue(writer, value);
			}
		}

		public static INetData Deserialize(BinaryReader reader)
		{
			ushort readTypeID = reader.ReadUInt16();
			reader.BaseStream.Position -= 2;
			INetData data = typeIDManager.CreateInstance(readTypeID);
			Deserialize(ref data, reader);
			return data;
		}

		public static void Deserialize(INetData netData, BinaryReader reader)
		{
			Deserialize(ref netData, reader);
		}

		public static void Deserialize(ref INetData netData, BinaryReader reader)
		{
			ushort readTypeID = reader.ReadUInt16();
			Type readType = typeIDManager.TypeIDToType(readTypeID);
			Type expectedType = netData.GetType();

			if (expectedType != readType)
			{
				throw new Exception($"Expected type {expectedType} but got type id {readTypeID} ({readType})");
			}

			DeserializeObject(netData, reader);
		}

		private static object DeserializeObject(Type type, BinaryReader reader)
		{
			object result = Activator.CreateInstance(type);
			DeserializeObject(result, reader);
			return result;
		}

		private static void DeserializeObject(object obj, BinaryReader reader)
		{
			Type type = obj.GetType();
			var fieldInfos = GetSerializedFieldInfos(type);
			foreach (var field in fieldInfos)
			{
				field.SetValue(obj, ReadValue(reader, field.FieldType));
			}
		}

		public static object ReadValue(BinaryReader reader, Type type)
		{
			object output = null;

			if (typeof(INetData).IsAssignableFrom(type))
			{
				output = Deserialize(reader);
			}
			else if (type.IsPrimitive)
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
			else if (type == typeof(Vector2Int))
			{
				output = new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
			}
			else if (type == typeof(Vector3Int))
			{
				output = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
			}
			else if (typeof(IList).IsAssignableFrom(type))
			{
				int count = reader.ReadInt32();

				IList list = (IList)Activator.CreateInstance(type);
				Type elementType = type.GetGenericArguments().Single();

				for (int i = 0; i < count; i++)
				{
					list.Add(ReadValue(reader, elementType));
				}
				output = list;
			}
			else if (type.IsClass || type.IsValueType)
			{
				output = DeserializeObject(type, reader);
			}

			return output;
		}

		public static void WriteValue(BinaryWriter writer, object value)
		{
			Type type = value.GetType();
			if (typeof(INetData).IsAssignableFrom(type))
			{
				Serialize((INetData)value, writer);
			}
			else if (type.IsPrimitive)
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
			else if (type == typeof(Vector2Int))
			{
				var v = (Vector2Int)value;
				writer.Write(v.x);
				writer.Write(v.y);
			}
			else if (type == typeof(Vector3Int))
			{
				var v = (Vector3Int)value;
				writer.Write(v.x);
				writer.Write(v.y);
				writer.Write(v.z);
			}
			else if (typeof(IList).IsAssignableFrom(type))
			{
				IList list = (IList)value;
				writer.Write(list.Count);
				foreach (var item in list)
				{
					WriteValue(writer, item);
				}
			}
			else if (type.IsClass || type.IsValueType)
			{
				SerializeObject(value, writer);
			}
		}
	}
}
