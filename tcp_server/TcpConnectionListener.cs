using System.Net;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace LowLevelTransport.Tcp
{
    public partial class TcpConnectionListener
    {
        private Dictionary<Socket, TcpConnection> clients = new Dictionary<Socket, TcpConnection>();
        private readonly Queue<TcpConnection> newConnQueue = new Queue<TcpConnection>();
        private Socket listenSock;
        private Thread sendReceiveThread;
        private readonly int AliveCheckInterval = 1000;
        private readonly CancellationTokenSource cancelSrc = new CancellationTokenSource();
        private readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private long GetMillisecondStamp() =>(long)(DateTime.UtcNow - unixEpoch).TotalMilliseconds;
        public TcpConnectionListener(string host, int port, int sendBufferSize=65535, int receiveBufferSize=65535)
        {
            listenSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAddress = IPAddress.Parse(host);
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, port);
            listenSock.Bind(ipEndPoint);
            listenSock.Listen(0);
            Console.WriteLine("Server start!");
        }
        public void Start()
        {
            AliveCheckLoop();
            sendReceiveThread = new Thread(SendReceiveMsg)
            {
                IsBackground = true
            };
            sendReceiveThread.Start();
        }
        private void SendReceiveMsg()
        {
            List<Socket> checkRead = new List<Socket>();
            List<Socket> checkWrite = new List<Socket>();
            while(true)
            {
                checkRead.Clear();
                checkRead.Add(listenSock);
                foreach(var clientState in clients.Values)
                {
                    checkRead.Add(clientState.socket);
                    if(clientState.sendQueue.Count > 0)
                    {
                        checkWrite.Add(clientState.socket);
                    }
                }
                Socket.Select(checkRead, checkWrite, null, 1000);
                foreach(var socket in checkWrite)
                {
                    WriteClientfd(socket);
                }
                foreach(var socket in checkRead)
                {
                    if(socket == listenSock)
                    {
                        ReadListenfd(socket);
                    }
                    else
                    {
                        ReadClientfd(socket);
                    }
                }
            }
        }
        public TcpConnection Accept()
        {
            if (newConnQueue.Count != 0)
                return newConnQueue.Dequeue();
            return null;
        }
        public void Close()
        {
            sendReceiveThread.Abort();
            listenSock.Close();
            StopTimer();
        }
        private void ReadListenfd(Socket listensock)
        {
            Console.WriteLine("Accept");
            Socket client = listensock.Accept();
            client.NoDelay = true;
            TcpConnection state = new TcpConnection();
            state.socket = client;
            state.lastAliveTime = GetMillisecondStamp();
            clients.Add(client, state);
            newConnQueue.Enqueue(state);
        }
        private void WriteClientfd(Socket clientfd)
        {
            TcpConnection state = clients[clientfd];
            lock(state.sendQueue)
            {
                while(state.sendQueue.Count != 0)
                {
                    byte[] msg = state.sendQueue.Dequeue();
                    state.socket.Send(msg);
                }
            }
        }
        private bool ReadClientfd(Socket clientfd)
        {
            TcpConnection state = clients[clientfd];
            int hostByte = 0;
            int count = 0;
            try
            {
                count = clientfd.Receive(state.readBuff, 4, SocketFlags.None);
                if(count == 0)
                {
                    clientfd.Close();
                    Console.WriteLine("Socket close");
                    return false;
                }
                int bytes = BitConverter.ToInt32(state.readBuff, 0);
                hostByte = IPAddress.NetworkToHostOrder(bytes);
                if(hostByte == 0)
                {
                    Interlocked.Exchange(ref state.lastAliveTime, GetMillisecondStamp());
                //  Console.WriteLine("lastAliveTime {0}", state.lastAliveTime);
                    byte[] dst = new byte[4]{0, 0, 0, 0};
                    state.socket.Send(dst);
                    return true;
                }
                count = clientfd.Receive(state.readBuff, hostByte, SocketFlags.None);
            }
            catch(SocketException ex)
            {
                clientfd.Close();
                clients.Remove(clientfd);
                Console.WriteLine($"Receive Socket Exception {ex.ToString()}");
                return false;
            }
            if(count == 0)
            {
                clientfd.Close();
                clients.Remove(clientfd);
                Console.WriteLine("Socket close");
                return false;
            }
            string recvStr = System.Text.Encoding.Default.GetString(state.readBuff, 0, hostByte);
            Console.WriteLine($"Rec {recvStr}");
            return true;
        }
        private void Remote(Socket socket)
        {
            lock(clients)
            {
                clients.Remove(socket);
            }
        }
        public void Send(byte[] buff)
        {
            lock(clients)
            {
                foreach(var clientState in clients.Values)
                {
                    clientState.SendBytes(buff);
                }
            }
        }
    }
}