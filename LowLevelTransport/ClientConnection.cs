using LowLevelTransport.Udp;
using LowLevelTransport.Tcp;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Net;

namespace LowLevelTransport
{
    public class ClientConnection
    {
        UdpClientConnection connection;
        public ClientConnection(string host, int port, string remoteHost, int remotePort)
        {
            connection = new UdpClientConnection(host, port, remoteHost, remotePort);
        }
        public ClientConnection(EndPoint ep, int flushInterval = 10)
        {
            connection = new UdpClientConnection(ep, flushInterval);
        }
        public Task<bool> ConnectAsync(int timeout = 3000)
        {
            return connection.ConnectAsync(timeout);
        }
        public void SendBytes(byte[] buff, SendOption sendOption = SendOption.None)
        {
            connection.SendBytes(buff, sendOption);
        }
#if DOTNET_CORE
        public byte[] Receive()
        {
            return connection.Receive();
        }
        public async Task<byte[]> ReceiveAsync(CancellationToken cToken)
        {
            return await connection.ReceiveAsync(cToken);
        }
        public Task<byte[]> ReceiveAsync(CancellationToken cToken, TimeSpan t)
        {
            return connection.ReceiveAsync(cToken, t);
        }
#else
        public byte[] Receive()
        {
            return connection.Receive();
        }
#endif
        public void Close()
        {
            connection.Close();
        }
        public Action TryReconnectCallback;
        public Action<bool> ReconnectFinishCallback;
        public void Tick()
        {
            connection.Tick();
        }
        public void Flush()
        {
            connection.Flush();
        }
        public bool IsClosed()
        {
            return connection.IsClosed();
        }
        public void ReconnectTest()
        {

        }
    }
}