using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NetState
{
	public class ClientInfo
	{
		public int id { get; private set; }

		public ClientInfo(int id)
		{
			this.id = id;
		}
	}
}
