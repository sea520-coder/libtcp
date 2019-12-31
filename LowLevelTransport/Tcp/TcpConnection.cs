using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Net.Sockets;
using System.Net;
using LowLevelTransport.Utils;
#if DOTNET_CORE
using System.Threading.Channels;
#endif

namespace LowLevelTransport.Tcp
{
    public class TcpConnection : Connection
    {
#if DOTNET_CORE
        internal readonly Channel<byte[]> recvQueue = Channel.CreateBounded<byte[]>((int)ReceiveQueue.Size);
#else
        internal Queue<byte[]> recvQueue = new Queue<byte[]>();
#endif
        internal Socket socket;
        internal Queue<byte[]> sendQueue = new Queue<byte[]>();
        protected static readonly int MaxSize = 1 << 20; //ushort.MaxValue
        private volatile bool isClosed;
        internal byte[] peekReceiveBuffer = new byte[MaxSize];

        private volatile ConnectionState state;
        internal ConnectionState State
        {
            get => state;
            set => state = value;
        }
        protected object stateLock = new object();

        public override void SendBytes(byte[] buff, SendOption sendOption = SendOption.None)
        {
            if(State != ConnectionState.Connected)
            {
                throw new LowLevelTransportException("tcp is not connected");
            }
            if(buff.Length >= MaxSize)
            {
                throw new LowLevelTransportException($"buff's length {buff.Length} is too large");
            }

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
#if DOTNET_CORE
        public override byte[] Receive()
        {
            lock(recvQueue)
            {
                if(isClosed)
                {
                    throw new LowLevelTransportException("Receive From a Not Connected Connection");
                }
                byte[] p;
                return recvQueue.Reader.TryRead(out p) ? p : null;
            }
        }
        public override async Task<byte[]> ReceiveAsync(CancellationToken cToken)
        {
            return await recvQueue.Reader.ReadAsync(cToken);
        }
        public override Task<byte[]> ReceiveAsync(CancellationToken cToken, TimeSpan t)
        {
            var task = recvQueue.Reader.ReadAsync(cToken);
            return task.AsTask().TimeoutAfter<byte[]>((int)t.TotalMilliseconds);
        }
#else
        public override byte[] Receive()
        {
            lock(recvQueue)
            {
                if(isClosed)
                {
                    throw new LowLevelTransportException("Receive From a Not Connected Connection");
                }
                if(recvQueue.Count != 0)
                    return recvQueue.Dequeue();
                return null;
            }
        }
#endif
        public override bool IsClosed()
        {
            lock(recvQueue)
            {
                return isClosed;
            }
        }
        public override void Close()
        {
            socket.Close();
            socket = null;
        }
    }
}