﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;

namespace NetState
{
	public class NetInterface
	{
		public class ChannelInfo
		{
			public string name;
			public int id;
			public QosType type;
		}

		public delegate void OnConnectedDelegate(NetEvent networkEvent);
		public event OnConnectedDelegate onConnected;
		public delegate void OnDisconnectedDelegate(NetEvent networkEvent);
		public event OnDisconnectedDelegate onDisconnected;
		public delegate void OnPacketDelegate(NetPacket packet);
		public event OnPacketDelegate onPacket;

		public int connectionID { get; private set; }
		public int hostID { get; private set; }
		public int port { get; private set; }

		private Dictionary<int, ChannelInfo> channelIDToInfoDict = new Dictionary<int, ChannelInfo>();
		private Dictionary<string, ChannelInfo> channelNameToInfoDict = new Dictionary<string, ChannelInfo>();

		public bool isConnected { get; private set; }

		private static Dictionary<int, NetInterface> hostIDDict = new Dictionary<int, NetInterface>();

		public NetInterface()
		{
			connectionID = -1;
			hostID = -1;
		}

		private static int GetFreeTcpPort()
		{
			TcpListener l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			int port = ((IPEndPoint)l.LocalEndpoint).Port;
			l.Stop();
			return port;
		}

		public void StartHost(int desiredPort, int maxConnections)
		{
			if (hostID != -1)
			{
				Debug.LogWarning("hostID is not -1");
				return;
			}

			NetworkTransport.Init();

			port = desiredPort;
			if (port <= 0)
			{
				port = GetFreeTcpPort();
			}

			NetworkTransport.Init();

			ConnectionConfig config = new ConnectionConfig();
			foreach (var channelInfo in channelNameToInfoDict.Values)
			{
				channelInfo.id = config.AddChannel(channelInfo.type);
			}
			HostTopology topology = new HostTopology(config, maxConnections);

			bool simulateLatency = false;
			if (simulateLatency)
			{
				hostID = NetworkTransport.AddHostWithSimulator(topology, 50, 60, port);
			}
			else
			{
				hostID = NetworkTransport.AddHost(topology, port);
			}

			hostIDDict.Add(hostID, this);
		}

		public void Connect(string address, int port)
		{
			byte error;

			bool simulateLatency = false;
			if (simulateLatency)
			{
				ConnectionSimulatorConfig simConfig = new ConnectionSimulatorConfig(50, 50, 50, 50, 0);
				connectionID = NetworkTransport.ConnectWithSimulator(hostID, address, port, 0, out error, simConfig);
			}
			else
			{
				connectionID = NetworkTransport.Connect(hostID, address, port, 0, out error);
			}

			NetworkError networkError = (NetworkError)error;
			if (networkError != NetworkError.Ok)
			{
				Debug.LogError(networkError);
			}
		}

		public void AddChannel(string name, QosType type)
		{
			channelNameToInfoDict.Add(name, new ChannelInfo { name = name, id = -1, type = type });
		}

		public ChannelInfo GetChannel(string name)
		{
			channelNameToInfoDict.TryGetValue(name, out var value);
			return value;
		}

		public ChannelInfo GetChannel(int id)
		{
			channelIDToInfoDict.TryGetValue(id, out var value);
			return value;
		}

		public NetworkError Send(int connectionID, int channelID, string text)
		{
			byte[] buffer = System.Text.Encoding.Unicode.GetBytes(text);
			return Send(connectionID, channelID, buffer, buffer.Length);
		}

		public NetworkError Send(int connectionID, int channelID, byte[] dataBuffer)
		{
			return Send(connectionID, channelID, dataBuffer, dataBuffer.Length);
		}

		public NetworkError Send(int connectionID, int channelID, byte[] dataBuffer, int dataLength)
		{
			byte errorCode;
			NetworkTransport.Send(hostID, connectionID, channelID, dataBuffer, dataLength, out errorCode);
			NetworkError error = (NetworkError)errorCode;
			if (error != NetworkError.Ok)
			{
				Debug.LogError("Send Error: "+error+" "+hostID+" "+connectionID+" "+channelID);
			}
			return error;
		}

		private static byte[] dataBuffer = new byte[0xffff-1];
		private List<NetEvent> eventQueue = new List<NetEvent>();
		public void Update()
		{
			if (!NetworkTransport.IsStarted)
			{
				return;
			}

			while (true)
			{
				int recConnectionID;
				int recChannelID;
				int dataLength;

				byte error;
				NetworkEventType recNetworkEvent = NetworkTransport.ReceiveFromHost(hostID, out recConnectionID, out recChannelID, dataBuffer, dataBuffer.Length, out dataLength, out error);

				if (recNetworkEvent == NetworkEventType.Nothing)
				{
					break;
				}

				NetEvent networkEvent = new NetEvent();
				networkEvent.netInterface = this;
				networkEvent.connectionID = recConnectionID;
				networkEvent.channelID = recChannelID;
				networkEvent.eventType = recNetworkEvent;
				networkEvent.error = (NetworkError)error;

				if (networkEvent.eventType == NetworkEventType.DataEvent)
				{
					networkEvent.data = new byte[dataLength];
					System.Array.Copy(dataBuffer, networkEvent.data, dataLength);
				}
				eventQueue.Add(networkEvent);
			}

			foreach (var networkEvent in eventQueue)
			{
				if (networkEvent.error != NetworkError.Ok)
				{
					Debug.LogError("NetworkEvent Error: "+networkEvent.error+" "+hostID+" "+connectionID);
				}

				if (networkEvent.eventType == NetworkEventType.ConnectEvent)
				{
					isConnected = true;

					try
					{
						onConnected?.Invoke(networkEvent);
					}
					catch (System.Exception ex)
					{
						Debug.LogException(ex);
					}
				}
				else if (networkEvent.eventType == NetworkEventType.DisconnectEvent)
				{
					try
					{
						onDisconnected?.Invoke(networkEvent);
					}
					catch (System.Exception ex)
					{
						Debug.LogException(ex);
					}

					isConnected = false;
					connectionID = -1;
					foreach (var channelInfo in channelNameToInfoDict.Values)
					{
						channelInfo.id = -1;
					}
				}
				else if (networkEvent.eventType == NetworkEventType.DataEvent)
				{
					MemoryStream stream = new MemoryStream(networkEvent.data);
					BinaryReader reader = new BinaryReader(stream);

					NetPacket packet = NetPacket.ReadNext(reader);
					packet.receivedTime = Time.time;
					packet.networkEvent = networkEvent;

					reader.Close();
					stream.Close();

					try
					{
						onPacket?.Invoke(packet);
					}
					catch (System.Exception ex)
					{
						Debug.LogException(ex);
					}
				}
			}
			eventQueue.Clear();
		}
	}
}
