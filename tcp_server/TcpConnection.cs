using System.Collections.Generic;
using System.Threading;
using System;
using System.Net.Sockets;
using System.Net;

namespace LowLevelTransport.Tcp
{
    public class TcpConnection
    {
        public Socket socket;
        public byte[] readBuff = new byte[1024 * 1024];
        public Queue<byte[]> sendQueue = new Queue<byte[]>();
        public long lastAliveTime;
        public static readonly long keepAliveTimeout = 30 * 1000;
        public bool CheckAlive(long timeNow) =>
            (timeNow - Interlocked.Read(ref lastAliveTime)) < keepAliveTimeout;
        public void Close(Exception e = null)
        {
            Console.WriteLine("Close for exception:{0}", e?.Message);
            socket.Close();
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