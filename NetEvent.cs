using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;

namespace NetState
{
	public class NetEvent
	{
		public NetInterface netInterface;
		public int connectionID;
		public int channelID;
		public NetworkEventType eventType;
		public NetworkError error;
		public byte[] data;
	}
}
