using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LowLevelTransport.Tcp
{
    public partial class TcpClientConnection
    {
        private readonly Socket client;
        private byte[] dataBuffer = new byte[1024 * 1024];
        private Queue<byte[]> sendQueue = new Queue<byte[]>();
        private readonly CancellationTokenSource sessionCTS = new CancellationTokenSource();
        private Thread sendReceiveThread;
        public TcpClientConnection(string host, int port, int sendBufferSize = 65535, int receiveBufferSize = 65535)
        {
            IPAddress ipAddress = IPAddress.Parse(host);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
            client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(remoteEP);
            client.NoDelay = true;
            Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());
            InitKeepAliveTimer();
            sendReceiveThread = new Thread(SendReceiveMsg)
            {
                IsBackground = true
            };
            sendReceiveThread.Start();
        }
        public void Close(Exception e = null)
        {
            Console.WriteLine("Close for exception:{0}", e?.Message);
            client.Close();
        }
        private void SendReceiveMsg()
        {
            List<Socket> checkRead = new List<Socket>();
            List<Socket> checkWrite = new List<Socket>();
            while(true)
            {
                checkRead.Clear();
                checkRead.Add(client);
                if(sendQueue.Count > 0)
                {
                    checkWrite.Add(client);
                }
                
                Socket.Select(checkRead, checkWrite, null, 1000);
                foreach(var socket in checkRead)
                {
                    ReadClientfd(socket);
                }
                foreach(var socket in checkWrite)
                {
                    WriteClientfd(socket);
                }
            }
        }
        public void WriteClientfd(Socket clientfd)
        {
            lock(sendQueue)
            {
                while(sendQueue.Count != 0)
                {
                    byte[] msg = sendQueue.Dequeue();
                    client.Send(msg);
                }
            }
        }
        public bool ReadClientfd(Socket clientfd)
        {
            int hostByte = 0;
            int count = 0;
            try
            {
                count = clientfd.Receive(dataBuffer, 4, SocketFlags.None);
                if(count == 0)
                {
                    clientfd.Close();
                    Console.WriteLine("Socket close");
                    return false;
                }
                int bytes = BitConverter.ToInt32(dataBuffer, 0);
                hostByte = IPAddress.NetworkToHostOrder(bytes);
                if(hostByte == 0)
                {
                    HandleHeartbeat();
                    return true;
                }
                count = clientfd.Receive(dataBuffer, hostByte, SocketFlags.None);
            }
            catch(SocketException ex)
            {
                clientfd.Close();
                Console.WriteLine($"Receive Socket Exception {ex.ToString()}");
                return false;
            }
            if(count == 0)
            {
                clientfd.Close();
                Console.WriteLine("Socket close");
                return false;
            }
            string recvStr = System.Text.Encoding.Default.GetString(dataBuffer, 0, count);
            Console.WriteLine($"Rec {recvStr}");
            return true;
        }
        public void SendBytes(byte[] buff)
        {
            int length = buff.Length;
            int hostBytes = IPAddress.HostToNetworkOrder(length);
            byte[] lengthBytes = BitConverter.GetBytes(hostBytes);
            byte[] dst = new byte[length + 4];
            Buffer.BlockCopy(lengthBytes, 0, dst, 0, 4);
            Buffer.BlockCopy(buff, 0, dst, 4, length);
            lock(sendQueue)
            {
                sendQueue.Enqueue(dst);
            }
        }
    }
}