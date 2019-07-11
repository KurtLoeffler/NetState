using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NetState
{
	public class TypeIDManager<T>
	{
		private Dictionary<int, Type> typeIDToType = new Dictionary<int, Type>();
		private Dictionary<Type, int> typeToTypeID = new Dictionary<Type, int>();

		public int typeCount => typeIDToType.Count;
		public Dictionary<int, Type>.KeyCollection ids => typeIDToType.Keys;
		public Dictionary<int, Type>.ValueCollection types => typeIDToType.Values;

		public Comparison<Type> typeComparison { get; set; }
		public readonly int idSize;

		public TypeIDManager(int idSize = 2)
		{
			if (idSize != 1 && idSize != 2 && idSize != 4)
			{
				throw new ArgumentException("Must be 1, 2, or 4", nameof(idSize));
			}
			
			this.idSize = idSize;

			typeIDToType.Clear();
			typeToTypeID.Clear();

			List<Type> allTypes = new List<Type>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					foreach (Type type in assembly.GetTypes())
					{
						try
						{
							if (typeof(T).IsAssignableFrom(type) && !type.IsAbstract)
							{
								allTypes.Add(type);
							}
						}
						catch { }
					}
				}
				catch { }
			}

			Comparison<Type> comparisonProvider = typeComparison ?? DefaultTypeComparisonProvider;
			allTypes.Sort(comparisonProvider);

			for (int i = 0; i < allTypes.Count; i++)
			{
				Type type = allTypes[i];
				typeIDToType.Add(i, type);
				typeToTypeID.Add(type, i);
			}
		}

		public Type IDToType(int typeID)
		{
			Type type;
			typeIDToType.TryGetValue(typeID, out type);
			return type;
		}

		public int TypeToID(Type type)
		{
			int id;
			if (!typeToTypeID.TryGetValue(type, out id))
			{
				id = -1;
			}
			return id;
		}

		public InstanceType CreateInstance<InstanceType>(int id) where InstanceType : T
		{
			Type type = IDToType(id);
			if (type == null)
			{
				return default;
			}

			Type resultType = typeof(InstanceType);
			if (resultType.IsValueType)
			{
				return default;
			}
			else
			{
				return (InstanceType)Activator.CreateInstance(type);
			}
		}

		public T CreateInstance(int id)
		{
			Type type = IDToType(id);
			return (T)Activator.CreateInstance(type);
		}

		private int DefaultTypeComparisonProvider(Type a, Type b)
		{
			return a.FullName.CompareTo(b.FullName);
		}

		public int ReadID(BinaryReader reader)
		{
			int id = 0;
			if (idSize == 1)
			{
				id = reader.ReadByte();
			}
			else if (idSize == 2)
			{
				id = reader.ReadUInt16();
			}
			else if (idSize == 4)
			{
				id = (int)reader.ReadUInt32();
			}
			return id;
		}

		public int PeekID(BinaryReader reader)
		{
			int id = ReadID(reader);
			reader.BaseStream.Position -= idSize;
			return id;
		}

		public void WriteID(BinaryWriter writer, int typeID)
		{
			if (idSize == 1)
			{
				writer.Write((byte)typeID);
			}
			else if (idSize == 2)
			{
				writer.Write((ushort)typeID);
			}
			else if (idSize == 4)
			{
				writer.Write((uint)typeID);
			}
		}

		public void WriteID(BinaryWriter writer, Type type)
		{
			int typeID = TypeToID(type);
			WriteID(writer, typeID);
		}

	}
}
