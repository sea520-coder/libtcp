using System.Collections.Generic;
using System.Threading;
using System;
using System.Net.Sockets;
using System.Net;

namespace LowLevelTransport.Tcp
{
    public class TcpConnection
    {
#if DOTNET_CCORE
        private readonly Channel<byte[]> recvQueue = Channel.CreateBounded<byte[]>((int)ReceiveQueue.Size);
        public CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
#else
        private Queue<byte[]> recvQueue = new Queue<byte[]>();
#endif
        public Socket socket;
        public byte[] peekReceiveBuffer = new byte[MaxSize];
        public Queue<byte[]> sendQueue = new Queue<byte[]>();
        public long lastAliveTime;
        public static readonly long keepAliveTimeout = 30 * 1000;
        private static readonly int MaxSize = 1 << 20; //ushort.MaxValue
        private volatile bool isClosed;
        public bool IsClosed
        {
            get
            {
                lock(recvQueue)
                {
                    return isClosed;
                }
            }
        }
        public bool CheckAlive(long timeNow) =>
            (timeNow - Interlocked.Read(ref lastAliveTime)) < keepAliveTimeout;
        public void SendBytes(byte[] buff, SendOption sendOption = SendOption.None)
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
#if DOTNET_CCORE
        public byte[] Receive()
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
        public async Task<byte[]> ReceiveAsync(CancellationToken cToken)
        {
            return await recvQueue.Reader.ReadAsync(cToken);
        }
        public Task<byte[]> ReceiveAsync(CancellationToken cToken, TimeSpan t)
        {
            var task = recvQueue.Reader.ReadAsync(cToken);
            return task.AsTask().TimeoutAfter<byte[]>((int)t.TotalMilliseconds));
        }
#else
        public byte[] Receive()
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
        public void Close(Exception e = null)
        {
            Console.WriteLine("Close for exception:{0}", e?.Message);
            socket.Close();
        }
    }
}