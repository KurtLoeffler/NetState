using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NetState
{
	[DefaultExecutionOrder(-1)]
	public class NetClient : MonoBehaviour
	{
		public NetInterface netInterface;
		public string serverAddress = "127.0.0.1";
		public int serverPort;
		public bool connectOnStart = true;

		protected virtual void Awake()
		{
			SetupNetInterface();
		}

		protected virtual void SetupNetInterface()
		{
			netInterface = new NetInterface();
			netInterface.onConnected += OnConnect;
			netInterface.onDisconnected += OnDisconnect;
			netInterface.onPacket += OnPacket;
		}

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
			netInterface.Connect(serverAddress, serverPort);
		}

		protected virtual void Update()
		{
			netInterface.Update();
		}

		protected virtual void OnConnect(NetEvent networkEvent)
		{

		}

		protected virtual void OnDisconnect(NetEvent networkEvent)
		{

		}

		protected virtual void OnPacket(NetPacket packet)
		{

		}
	}
}
