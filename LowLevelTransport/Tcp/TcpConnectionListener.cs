using System.Net;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System;
using LowLevelTransport.Utils;
#if DOTNET_CORE
using System.Threading.Tasks.Dataflow;
using System.Threading.Channels;
#endif

namespace LowLevelTransport.Tcp
{
    public partial class TcpConnectionListener
    {
        private Dictionary<Socket, TcpServerConnection> clients = new Dictionary<Socket, TcpServerConnection>();
#if DOTNET_CORE
        private readonly BufferBlock<TcpServerConnection> newConnQueue = new BufferBlock<TcpServerConnection>();
#else
        private readonly Queue<TcpServerConnection> newConnQueue = new Queue<TcpServerConnection>();
#endif
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
#if DOTNET_CORE
        public async Task<Connection> AcceptAsync(CancellationToken token)
        {
            return await newConnQueue.ReceiveAsync(cancellationToken: token);
        }
#else
        public Connection Accept()
        {
            if (newConnQueue.Count != 0)
                return newConnQueue.Dequeue();
            return null;
        }
#endif
        public void Close()
        {
            sendReceiveThread.Abort();
            listenSock.Close();
            StopTimer();
        }
        private void SendReceiveMsg()
        {
            List<Socket> checkRead = new List<Socket>();
            List<Socket> checkWrite = new List<Socket>();
            while(true)
            {
                checkRead.Clear();
                checkWrite.Clear();
                checkRead.Add(listenSock);
                //Log.Error("====count {0}", clients.Count);
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
        private void ReadListenfd(Socket listensock)
        {
            Console.WriteLine("Accept");
            Socket client = listensock.Accept();
            client.NoDelay = true;
            TcpServerConnection conn = new TcpServerConnection();
            conn.socket = client;
            conn.lastAliveTime = GetMillisecondStamp();
            conn.State = ConnectionState.Connected;
            clients.Add(client, conn);
#if DOTNET_CORE
            newConnQueue.Post(conn);
#else
            newConnQueue.Enqueue(conn);
#endif
        }
        private void WriteClientfd(Socket clientfd)
        {
            TcpServerConnection conn;
            if(!clients.TryGetValue(clientfd, out conn))
            {
                Console.WriteLine("WriteClientfd not find clientfd");
                return;
            }
            lock(conn.sendQueue)
            {
                while(conn.sendQueue.Count != 0)
                {
                    byte[] msg = conn.sendQueue.Dequeue();
                    conn.socket.Send(msg);
                }
            }
        }
        private bool ReadClientfd(Socket clientfd)
        {
            TcpServerConnection conn;
            if(!clients.TryGetValue(clientfd, out conn))
            {
                Console.WriteLine("ReadClientfd not find clientfd");
                return false;
            }

            int hostByte = 0;
            int count = 0;
            try
            {
                count = clientfd.Receive(conn.peekReceiveBuffer, 0, 4, SocketFlags.None);
                if(count == 0)
                {
                    Remove(clientfd);
                    clientfd.Close();
                    Log.Info("Socket Close");
                    return false;
                }
                while(count != 4)
                {
                    count += clientfd.Receive(conn.peekReceiveBuffer, count, 4 - count, SocketFlags.None);
                }
                int bytes = BitConverter.ToInt32(conn.peekReceiveBuffer, 0);
                hostByte = IPAddress.NetworkToHostOrder(bytes);
                if(hostByte == 0)
                {
                    //Log.Info("heart ===============");
                    Interlocked.Exchange(ref conn.lastAliveTime, GetMillisecondStamp());
                    byte[] dst = new byte[4]{0, 0, 0, 0};
                    conn.socket.Send(dst);
                    return true;
                }
                count = clientfd.Receive(conn.peekReceiveBuffer, 0, hostByte, SocketFlags.None);
                while(count != hostByte)
                {
                    count += clientfd.Receive(conn.peekReceiveBuffer, count, hostByte - count, SocketFlags.None);
                }
            }
            catch(SocketException e)
            {
                Remove(clientfd);
                clientfd.Close();
                Log.Error($"ReadClientfd SocketException {e.Message} {e.ErrorCode}");
                return false;
            }
            catch(ObjectDisposedException e)
            {
                Remove(clientfd);
                Log.Error("ReadClientfd ObjectDisposedException {0}", e.Message);
            }
            if(count == 0)
            {
                Remove(clientfd);
                clientfd.Close();

                Console.WriteLine("Socket close");
                return false;
            }

            byte[] buff = new byte[hostByte];
            Buffer.BlockCopy(conn.peekReceiveBuffer, 0, buff, 0, hostByte);
#if DOTNET_CORE
            if(!conn.recvQueue.Writer.TryWrite(buff))
            {
                UInt16 msgType = hostByte > 1 ? (UInt16)(( (UInt16)buff[1] << 8 ) | ( (UInt16)buff[0] )) : (UInt16)0;
                Log.Error($"receive queue overload & last type{msgType}");
            }
#else
            lock (conn.recvQueue)
            {
                conn.recvQueue.Enqueue(buff);
            }
#endif
            return true;
        }
        private void Remove(Socket socket)
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