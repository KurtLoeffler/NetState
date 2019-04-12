using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NetState
{
	[DefaultExecutionOrder(-2)]
	public class NetServer : NetBase
	{
		public int maxConnections = 12;
		public bool startServerOnStart = true;

		public List<int> connectedClientIDs { get; } = new List<int>();

		protected virtual void Start()
		{
			if (startServerOnStart)
			{
				StartServer();
			}
		}

		public void StartServer()
		{
			netInterface.StartHost(port, maxConnections);
		}

		protected override void OnConnect(NetEvent networkEvent)
		{
			connectedClientIDs.Add(networkEvent.connectionID);
		}

		protected override void OnDisconnect(NetEvent networkEvent)
		{
			connectedClientIDs.Remove(networkEvent.connectionID);
		}

		public void BroadcastPacket(string channel, NetPacket packet)
		{
			BroadcastPacket(netInterface.GetChannel(channel).id, packet);
		}

		public void BroadcastPacket(int channelID, NetPacket packet)
		{
			foreach (var id in connectedClientIDs)
			{
				SendPacket(id, channelID, packet);
			}
		}
	}
}
