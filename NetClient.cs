using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NetState
{
	[DefaultExecutionOrder(-1)]
	public class NetClient : NetBase
	{
		public string serverAddress = "127.0.0.1";
		public bool connectOnStart = true;

		public virtual bool isConnected => netInterface != null && netInterface.isConnected;

		protected virtual void Start()
		{
			if (connectOnStart)
			{
				Connect();
			}
		}

		public void Connect()
		{
			netInterface.StartHost(0, 1);
			netInterface.Connect(serverAddress, port);
		}

		public void SendPacket(string channel, NetPacket packet)
		{
			SendPacket(netInterface.connectionID, channel, packet);
		}

		public void SendPacket(int channelID, NetPacket packet)
		{
			SendPacket(netInterface.connectionID, channelID, packet);
		}
	}
}
