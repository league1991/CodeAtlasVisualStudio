using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Json;
using System.ServiceModel.Web;

namespace CodeAtlas
{
    class SocketThread
	{
		public delegate void SocketCallback(string name, object[] paramList);

        private string m_ipAddress = "", m_remoteAddress = "";
        private int m_port = 0, m_remotePort = 0;
        private Socket m_socket = null;
        private Thread m_socketThread = null;
		private SocketCallback m_socketCallback;

        public SocketThread(string ipAddress, int port, string remoteAddress, int remotePort, SocketCallback callback = null)
        {
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_ipAddress = ipAddress;
            m_port = port;
            m_remoteAddress = remoteAddress;
            m_remotePort = remotePort;
			m_socketCallback = callback;
        }

        public void run()
        {
            if (m_socket == null)
                return;
			try
			{
				m_socket.Bind(new IPEndPoint(IPAddress.Parse(m_ipAddress), m_port));
				m_socketThread = new Thread(this.receiveMessage);
				m_socketThread.Start();
			}
			catch (SocketException ex)
			{
				System.Console.Out.WriteLine(ex.ToString());
			}
        }

        private void receiveMessage()
        {
            byte[] data = new byte[2048];
            EndPoint endPoint = new IPEndPoint(IPAddress.Parse(m_remoteAddress), m_remotePort);
            while (true)
            {
                try
                {
                    int rlen = m_socket.ReceiveFrom(data, ref endPoint);
                    string msg = Encoding.Default.GetString(data, 0, rlen);
					JsonPacket packet = JsonPacket.fromJson(msg);
					if (m_socketCallback != null)
						m_socketCallback(packet.f, packet.p);
                }
                catch (Exception ex)
                {
                    System.Console.Out.WriteLine(ex.ToString());
                }
            }
        }

        public void sendMessage(string msg)
        {
            byte[] data = Encoding.Default.GetBytes(msg);
            m_socket.SendTo(data, SocketFlags.None, new IPEndPoint(IPAddress.Parse(m_remoteAddress), m_remotePort));
        }

		public void remoteCall(string functionName, object[] param = null)
		{
			JsonPacket packet = new JsonPacket() { f = functionName, p = param };
			string jsonStr = packet.toJson();
			sendMessage(jsonStr);
		}

		public void release()
		{
			if (m_socket != null)
			{
				m_socket.Close();
			}
		}
	}
}

