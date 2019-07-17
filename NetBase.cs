using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NetState
{
	public class NetBase : MonoBehaviour
	{
		public NetInterface netInterface;
		public int port;

		private class OnPacketCallbackBase
		{
			public virtual void Invoke(NetPacket packet) { }
		}

		private class OnPacketCallback<T> : OnPacketCallbackBase where T : NetPacket
		{
			public OnPacketGenericDelegate<T> callback;
			public OnPacketCallback(OnPacketGenericDelegate<T> callback)
			{
				this.callback = callback;
			}
			public override void Invoke(NetPacket packet)
			{
				callback((T)packet);
			}
		}

		public delegate void OnPacketDelegate(NetPacket packet);
		public delegate void OnPacketGenericDelegate<T>(T packet) where T : NetPacket;
		private Dictionary<System.Type, List<OnPacketCallbackBase>> onPacketDelegateDict = new Dictionary<System.Type, List<OnPacketCallbackBase>>();

		protected virtual void Awake()
		{
			SetupNetInterface();
		}

		protected virtual void Update()
		{
			netInterface.Update();
		}

		protected virtual void OnDestroy()
		{
			if (sendBinaryWriterCache != null)
			{
				sendBinaryWriterCache.BaseStream.Close();
				sendBinaryWriterCache.Close();
			}
			sendBinaryWriterCache = null;
			sendBufferCache = null;
		}

		protected virtual void SetupNetInterface()
		{
			netInterface = new NetInterface();

			netInterface.onConnected += OnConnect;
			netInterface.onDisconnected += OnDisconnect;
			netInterface.onPacket += OnPacket;
		}

		public void AddOnPacketCallback<T>(OnPacketGenericDelegate<T> callback) where T : NetPacket
		{
			var type = typeof(T);
			if (!onPacketDelegateDict.TryGetValue(type, out var callbacks))
			{
				callbacks = new List<OnPacketCallbackBase>();
				onPacketDelegateDict.Add(type, callbacks);
			}
			callbacks.Add(new OnPacketCallback<T>(callback));
		}

		public void SendPacket(int connectionID, string channel, NetPacket packet)
		{
			SendPacket(connectionID, netInterface.GetChannel(channel).id, packet);
		}

		private BinaryWriter sendBinaryWriterCache;
		private byte[] sendBufferCache;
		public void SendPacket(int connectionID, int channelID, NetPacket packet)
		{
			if (sendBinaryWriterCache == null)
			{
				sendBinaryWriterCache = new BinaryWriter(new MemoryStream());
			}

			packet.Serialize(sendBinaryWriterCache);

			var memoryStream = (MemoryStream)sendBinaryWriterCache.BaseStream;
			memoryStream.Seek(0, SeekOrigin.Begin);

			int streamLength = (int)memoryStream.Length;
			if (sendBufferCache == null || sendBufferCache.Length < streamLength)
			{
				int arrayLength = Mathf.Max(streamLength*2, 1024);
				sendBufferCache = new byte[arrayLength];
			}
			memoryStream.Read(sendBufferCache, 0, streamLength);

			memoryStream.Seek(0, SeekOrigin.Begin);
			memoryStream.SetLength(0);

			netInterface.Send(connectionID, channelID, sendBufferCache, streamLength);
		}

		protected virtual void OnConnect(NetEvent networkEvent)
		{

		}

		protected virtual void OnDisconnect(NetEvent networkEvent)
		{

		}

		protected virtual void OnPacket(NetPacket packet)
		{
			var type = packet.GetType();
			if (onPacketDelegateDict.TryGetValue(type, out var callbacks))
			{
				foreach (var callback in callbacks)
				{
					callback.Invoke(packet);
				}
			}
		}
	}
}
