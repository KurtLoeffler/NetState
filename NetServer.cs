using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NetState
{
	[DefaultExecutionOrder(-2)]
	public class NetServer : MonoBehaviour
	{
		public NetInterface netInterface;
		public int desiredPort;
		public int maxConnections = 12;

		public List<ClientInfo> clientInfos = new List<ClientInfo>();
		public ClientInfo GetClientInfo(int id)
		{
			return clientInfos.FirstOrDefault(v => v.id == id);
		}

		protected virtual void Awake()
		{
			netInterface = new NetInterface();
			netInterface.onConnected += OnClientConnect;
			netInterface.onDisconnected += OnClientDisconnect;
			netInterface.onPacket += OnClientPacket;

			netInterface.StartHost(desiredPort, maxConnections);
		}

		protected virtual void Update()
		{
			netInterface.Update();
		}

		protected virtual void OnClientConnect(NetEvent networkEvent)
		{
			ClientInfo clientInfo = new ClientInfo(networkEvent.connectionID);
			clientInfos.Add(clientInfo);
		}

		protected virtual void OnClientDisconnect(NetEvent networkEvent)
		{
			ClientInfo clientInfo = GetClientInfo(networkEvent.connectionID);
			clientInfos.Remove(clientInfo);
		}

		protected virtual void OnClientPacket(NetPacket packet)
		{

		}
	}
}
