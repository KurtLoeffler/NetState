using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NetState
{
	public class TypeSerializers
	{
	}

	[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
	public sealed class GenerateNetSerializerAttribute : Attribute
	{

	}

	[AttributeUsage(AttributeTargets.Field|AttributeTargets.Class|AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
	sealed class NetMaskableAttribute : Attribute
	{

	}

	public static class NetDataUtilityExtensions
	{
		//Mono allocates garbage when writing floats and doubles so cast to ints.
		//Doesn't preserve endianness but who cares!!!
		public static unsafe void WriteSingle(this BinaryWriter writer, float value)
		{
			uint dummy = *((uint*)&value);
			writer.Write(dummy);
		}
		public static unsafe void WriteDouble(this BinaryWriter writer, double value)
		{
			ulong dummy = *((ulong*)&value);
			writer.Write(dummy);
		}
	}

	public static class NetSerialization
	{
		public static TypeIDManager<INetData> netDataTypeIDManager = new TypeIDManager<INetData>();

		private static Dictionary<Type, MethodInfo> serializerFunctionDict = new Dictionary<Type, MethodInfo>();
		private static Dictionary<Type, MethodInfo> deserializerFunctionDict = new Dictionary<Type, MethodInfo>();

		static NetSerialization()
		{

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					foreach (Type type in assembly.GetTypes())
					{
						if (!typeof(TypeSerializers).IsAssignableFrom(type))
						{
							continue;
						}

						try
						{
							var methodInfos = type.GetMethods(BindingFlags.Static|BindingFlags.Public);

							foreach (var methodInfo in methodInfos)
							{
								if (methodInfo.Name == "Serialize")
								{
									var firstParam = methodInfo.GetParameters()[0];
									var valueType = firstParam.ParameterType;

									serializerFunctionDict.Add(valueType, methodInfo);
								}
								else if (methodInfo.Name == "Deserialize")
								{
									var firstParam = methodInfo.GetParameters()[0];
									var valueType = firstParam.ParameterType.GetElementType();

									deserializerFunctionDict.Add(valueType, methodInfo);
								}
							}
						}
						catch {}
					}
				}
				catch { }
			}
		}

		//private static object[] cachedArgumentArray = new object[2];

		public static void Serialize(object value, BinaryWriter writer)
		{
			var type = value.GetType();
			if (typeof(INetData).IsAssignableFrom(type))
			{
				netDataTypeIDManager.WriteID(writer, type);
			}

			if (serializerFunctionDict.TryGetValue(type, out var serializer))
			{
				object[] cachedArgumentArray = new object[2];
				cachedArgumentArray[0] = value;
				cachedArgumentArray[1] = writer;
				serializer.Invoke(null, cachedArgumentArray);
			}
			else
			{
				Debug.LogWarning($"Cannot write type \"{type}\" because no serializer was found. Make sure generated serializers are up to date, and the type has a {nameof(GenerateNetSerializerAttribute)}.");
			}
		}

		public static T Deserialize<T>(BinaryReader reader)
		{
			return (T)Deserialize(typeof(T), reader);
		}

		public static object Deserialize(Type type, BinaryReader reader)
		{
			object value = null;
			if (typeof(INetData).IsAssignableFrom(type))
			{
				var id = netDataTypeIDManager.PeekID(reader);
				value = netDataTypeIDManager.CreateInstance(id);
			}
			else
			{
				value = Activator.CreateInstance(type);
			}
			
			Deserialize(ref value, reader);
			return value;
		}

		public static object Deserialize(object value, BinaryReader reader)
		{
			Deserialize(ref value, reader);
			return value;
		}

		private static object[] cachedArgumentArray = new object[2];
		public static void Deserialize(ref object value, BinaryReader reader)
		{
			var type = value.GetType();

			if (typeof(INetData).IsAssignableFrom(type))
			{
				netDataTypeIDManager.ReadID(reader);
			}

			if (deserializerFunctionDict.TryGetValue(type, out var deserializer))
			{
				cachedArgumentArray[0] = value;
				cachedArgumentArray[1] = reader;
				deserializer.Invoke(null, cachedArgumentArray);
				value = cachedArgumentArray[0];
			}
			else
			{
				Debug.LogWarning($"Cannot read type \"{type}\" because no deserializer was found. Make sure generated serializers are up to date, and the type has a {nameof(GenerateNetSerializerAttribute)}.");
			}
		}
	}
}
