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

		public Comparison<Type> typeComparison { get; set; }

		public TypeIDManager()
		{
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

		public Type TypeIDToType(int typeID)
		{
			Type type;
			typeIDToType.TryGetValue(typeID, out type);
			return type;
		}

		public int TypeToTypeID(Type type)
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
			Type type = TypeIDToType(id);
			if (type == null)
			{
				return default(InstanceType);
			}

			Type resultType = typeof(InstanceType);
			if (resultType.IsValueType)
			{
				return default(InstanceType);
			}
			else
			{
				return (InstanceType)Activator.CreateInstance(type);
			}
		}

		public T CreateInstance(int id)
		{
			Type type = TypeIDToType(id);
			return (T)Activator.CreateInstance(type);
		}

		private int DefaultTypeComparisonProvider(Type a, Type b)
		{
			return a.FullName.CompareTo(b.FullName);
		}
	}
}
