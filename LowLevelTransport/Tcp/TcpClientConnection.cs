using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using LowLevelTransport.Utils;

namespace LowLevelTransport.Tcp
{
    public partial class TcpClientConnection : TcpConnection
    {
        private Thread sendReceiveThread;
        private EndPoint remoteEP;

        public TcpClientConnection(string host, int port, string remoteHost, int remotePort, int sendBufferSize = 65535, int receiveBufferSize = 65535)
        {
            IPAddress ipAddress = IPAddress.Parse(remoteHost);
            remoteEP = new IPEndPoint(ipAddress, remotePort);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }
        public TcpClientConnection(EndPoint ep, int flushInterval = 10, 
            int sendBufferSize = (int)ClientSocketBufferOption.SendSize, int receiveBufferSize = (int)ClientSocketBufferOption.ReceiveSize)
        {
            remoteEP = ep;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }
        public Task<bool> ConnectAsync(int timeout = 3000)
        {
            lock(stateLock)
            {
                if(State != ConnectionState.NotConnected)
                {
                    throw new InvalidOperationException("Cannot connect as the Connection is already connected.");
                }
                State = ConnectionState.Connecting;
            }

            try
            {
                socket.Connect(remoteEP);
                socket.NoDelay = true;
            }
            catch(SocketException e)
            {
                State = ConnectionState.NotConnected;
                throw new LowLevelTransportException("tcp socket connect fail", e);
            }

            Console.WriteLine("Socket connected to {0}", socket.RemoteEndPoint.ToString());
            InitKeepAliveTimer();
            lock(stateLock)
            {
                State = ConnectionState.Connected;
            }
            sendReceiveThread = new Thread(SendReceiveMsg)
            {
                IsBackground = true
            };
            sendReceiveThread.Start();
            return Task.FromResult(true);
        }
        private void SendReceiveMsg()
        {
            List<Socket> checkRead = new List<Socket>();
            List<Socket> checkWrite = new List<Socket>();
            while(true)
            {
                if(State != ConnectionState.Connected)
                {
                    Log.Error("not connected is exist");
                    break;
                }

                checkRead.Clear();
                checkWrite.Clear();
                checkRead.Add(socket);
                if(sendQueue.Count > 0)
                {
                    checkWrite.Add(socket);
                }
                
                Socket.Select(checkRead, checkWrite, null, 1000);

                foreach(var socket in checkRead)
                {
                    if(socket != null)
                    {
                        ReadClientfd(socket);
                    }
                }

                foreach(var socket in checkWrite)
                {
                    if(socket != null)
                    {
                        WriteClientfd(socket);
                    }
                }
            }
        }
        private void WriteClientfd(Socket clientfd)
        {
            lock(sendQueue)
            {
                while(sendQueue.Count != 0)
                {
                    byte[] msg = sendQueue.Dequeue();
                    if(clientfd != null)
                        clientfd.Send(msg);
                }
            }
        }
        private bool ReadClientfd(Socket clientfd)
        {
            int hostByte = 0;
            int count = 0;
            try
            {
                count = clientfd.Receive(peekReceiveBuffer, 0, 4, SocketFlags.None);
                if(count == 0)
                {
                    clientfd.Close();
                    clientfd = null;
                    Log.Info("Socket Close");
                    return false;
                }
                while(count != 4)
                {
                    count += clientfd.Receive(peekReceiveBuffer, count, 4 - count, SocketFlags.None);
                }
                int bytes = BitConverter.ToInt32(peekReceiveBuffer, 0);
                hostByte = IPAddress.NetworkToHostOrder(bytes);
                if(hostByte == 0)
                {
                    HandleHeartbeat();
                    return true;
                }
                count = clientfd.Receive(peekReceiveBuffer, 0, hostByte, SocketFlags.None);
                while(count != hostByte)
                {
                    count += clientfd.Receive(peekReceiveBuffer, count, hostByte - count, SocketFlags.None);
                }
            }
            catch(SocketException e)
            {
                clientfd.Close();
                clientfd = null;
                Console.WriteLine($"Receive Socket Exception {e.Message} {e.ErrorCode}");
                return false;
            }
            catch(ObjectDisposedException e)
            {
                Console.WriteLine("ReadClientfd {0}", e.Message);
                return false;
            }
            if(count == 0)
            {
                clientfd.Close();
                clientfd = null;
                Console.WriteLine("data Socket close");
                return false;
            }

            byte[] buff = new byte[hostByte];
            Buffer.BlockCopy(peekReceiveBuffer, 0, buff, 0, hostByte);
#if DOTNET_CORE
            if(!recvQueue.Writer.TryWrite(buff))
            {
                UInt16 msgType = hostByte > 1 ? (UInt16)(( (UInt16)buff[1] << 8 ) | ( (UInt16)buff[0] )) : (UInt16)0;
                Log.Error($"receive queue overload & last type{msgType}");
            }
#else
            lock (recvQueue)
            {
                recvQueue.Enqueue(buff);
            }
#endif
            return true;
        }
        private void InvokeDisconnected(Exception e = null)
        {
            Log.Error("Disconnect tcp IP:{0} Exception{1}", remoteEP.ToString(), e?.Message);
            lock(stateLock)
            {
                State = ConnectionState.NotConnected;
            }
            StopTimer();
            Close();
        }
        private void HandleDisconnect(Exception e)
        {
            bool invoke = false;
            lock(stateLock)
            {
                if(State == ConnectionState.Connected)
                {
                    State = ConnectionState.Disconnecting;
                    invoke = true;
                }
            }
            if(invoke)
            {
                InvokeDisconnected(e);
            }
        }
    }
}