using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace NetState
{
	public class TypeSerializers
	{
	}

	[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
	public sealed class NetStateSerializeAttribute : Attribute
	{

	}

	[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
	public sealed class NetStateCustomAllocatorAttribute : Attribute
	{

	}

	[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
	public sealed class NetStateMaskableAttribute : Attribute
	{

	}

	public static class NetDataUtilityExtensions
	{
		//Mono allocates garbage when writing floats and doubles so cast to ints.
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
		public static TypeIDManager<INetStatePolymorphic> polymorphicTypeIDManager = new TypeIDManager<INetStatePolymorphic>();

		public delegate void SerializeDelegate(object value, BinaryWriter writer, object deltaReference);
		public delegate void DeserializeDelegate(ref object value, BinaryReader reader, object deltaReference);
		public delegate object CustomAllocatorDelegate(Type type);
		private static Dictionary<Type, SerializeDelegate> serializerFunctionDict = new Dictionary<Type, SerializeDelegate>();
		private static Dictionary<Type, DeserializeDelegate> deserializerFunctionDict = new Dictionary<Type, DeserializeDelegate>();
		private static Dictionary<Type, CustomAllocatorDelegate> customAllocatorDict = new Dictionary<Type, CustomAllocatorDelegate>();

		static NetSerialization()
		{

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					foreach (Type type in assembly.GetTypes())
					{
						InitializeSerializerFunctions(type);
						InitializeCustomAllocator(type);
					}
				}
				catch
				{

				}
			}
		}

		private static void InitializeSerializerFunctions(Type type)
		{
			if (!typeof(TypeSerializers).IsAssignableFrom(type))
			{
				return;
			}

			try
			{
				var methodInfos = type.GetMethods(BindingFlags.Static|BindingFlags.Public);

				for (int i = 0; i < methodInfos.Length; i++)
				{
					var methodInfo = methodInfos[i];
					MethodInfo nextMethod = null;
					if (i < methodInfos.Length-1)
					{
						nextMethod = methodInfos[i+1];
					}
					if (methodInfo.Name == "Serialize")
					{
						var firstParam = methodInfo.GetParameters()[0];
						var valueType = firstParam.ParameterType;
						var func = (SerializeDelegate)Delegate.CreateDelegate(typeof(SerializeDelegate), null, nextMethod);
						serializerFunctionDict.Add(valueType, func);
					}
					else if (methodInfo.Name == "Deserialize")
					{
						var firstParam = methodInfo.GetParameters()[0];
						var valueType = firstParam.ParameterType.GetElementType();
						var func = (DeserializeDelegate)Delegate.CreateDelegate(typeof(DeserializeDelegate), null, nextMethod);
						deserializerFunctionDict.Add(valueType, func);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
		}

		private static void InitializeCustomAllocator(Type type)
		{
			var methodInfos = type.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy);

			foreach (var methodInfo in methodInfos)
			{
				if (methodInfo.GetCustomAttributes<NetStateCustomAllocatorAttribute>(true).Any())
				{
					var func = (CustomAllocatorDelegate)Delegate.CreateDelegate(typeof(CustomAllocatorDelegate), null, methodInfo);
					customAllocatorDict.Add(type, func);
				}
			}
		}

		public static CustomAllocatorDelegate GetCustomAllocator(Type type)
		{
			customAllocatorDict.TryGetValue(type, out var result);
			return result;
		}

		public static void Serialize(object value, BinaryWriter writer, object referenceObject = null)
		{
			var type = value.GetType();
			if (typeof(INetStatePolymorphic).IsAssignableFrom(type))
			{
				polymorphicTypeIDManager.WriteID(writer, type);
			}

			if (serializerFunctionDict.TryGetValue(type, out var serializer))
			{
				serializer(value, writer, referenceObject);
			}
			else
			{
				Debug.LogWarning($"Cannot write type \"{type}\" because no serializer was found. Make sure generated serializers are up to date, and the type has a {nameof(NetStateSerializeAttribute)}.");
			}
		}

		public static T Deserialize<T>(BinaryReader reader, object referenceObject = null)
		{
			return (T)Deserialize(typeof(T), reader, referenceObject);
		}

		public static object Deserialize(Type type, BinaryReader reader, object referenceObject = null)
		{
			if (typeof(INetStatePolymorphic).IsAssignableFrom(type))
			{
				var id = polymorphicTypeIDManager.PeekID(reader);
				type = polymorphicTypeIDManager.IDToType(id);
			}
			object value = null;
			var customAllocator = GetCustomAllocator(type);
			if (customAllocator != null)
			{
				value = customAllocator(type);
			}
			else
			{
				value = Activator.CreateInstance(type);
			}

			Deserialize(ref value, reader, referenceObject);
			return value;
		}

		public static object Deserialize(object value, BinaryReader reader, object referenceObject = null)
		{
			Deserialize(ref value, reader, referenceObject);
			return value;
		}

		public static void Deserialize(ref object value, BinaryReader reader, object referenceObject = null)
		{
			var type = value.GetType();

			if (typeof(INetStatePolymorphic).IsAssignableFrom(type))
			{
				polymorphicTypeIDManager.ReadID(reader);
			}

			if (deserializerFunctionDict.TryGetValue(type, out var deserializer))
			{
				deserializer(ref value, reader, referenceObject);
			}
			else
			{
				Debug.LogWarning($"Cannot read type \"{type}\" because no deserializer was found. Make sure generated serializers are up to date, and the type has a {nameof(NetStateSerializeAttribute)}.");
			}
		}
	}
}
