using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace NetState
{
	public abstract class NetPacket
	{
		private static TypeIDManager<NetPacket> typeIDManager = new TypeIDManager<NetPacket>();

		public float receivedTime { get; set; }
		public NetEvent networkEvent { get; set; }
		
		public NetPacket()
		{
			receivedTime = Mathf.NegativeInfinity;
		}

		public static T ReadNext<T>(BinaryReader reader) where T : NetPacket
		{
			NetPacket packet = ReadNext(reader);

			if (!typeof(T).IsAssignableFrom(packet.GetType()))
			{
				throw new System.Exception("Invalid packet type \""+packet.GetType()+"\" expected \""+typeof(T)+"\"");
			}

			return (T)packet;
		}

		public static NetPacket ReadNext(BinaryReader reader)
		{
			int packetTypeID = typeIDManager.PeekID(reader);

			NetPacket packet = typeIDManager.CreateInstance(packetTypeID);
			packet.Deserialize(reader);

			return packet;
		}

		public byte[] Serialize()
		{
			var stream = new MemoryStream();
			var writer = new BinaryWriter(stream);

			Serialize(writer);

			byte[] bytes = stream.ToArray();

			writer.Close();
			stream.Close();

			return bytes;
		}

		public void Deserialize()
		{
			var stream = new MemoryStream();
			var reader = new BinaryReader(stream);

			Deserialize(reader);

			byte[] bytes = stream.ToArray();

			reader.Close();
			stream.Close();
		}

		public void Serialize(BinaryWriter writer)
		{
			typeIDManager.WriteID(writer, GetType());

			OnSerialize(writer);
		}

		public void Deserialize(BinaryReader reader)
		{
			int packetTypeID = typeIDManager.ReadID(reader);
			var packetType = typeIDManager.IDToType(packetTypeID);

			if (packetType != GetType())
			{
				reader.BaseStream.Position -= typeIDManager.idSize;
				throw new System.Exception("Unexpected PacketType \""+packetType+"\" expected \""+GetType()+"\"");
			}

			OnDeserialize(reader);
		}

		protected abstract void OnSerialize(BinaryWriter writer);
		protected abstract void OnDeserialize(BinaryReader reader);
	}
}
