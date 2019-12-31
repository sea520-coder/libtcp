using LowLevelTransport.Udp;
using LowLevelTransport.Tcp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LowLevelTransport
{
    public class ConnectionListener
    {
        TcpConnectionListener listener;
        public ConnectionListener(string host, int port, int sendBufferSize = (int)ServerSocketBufferOption.SendSize, 
            int receiveBufferSize = (int)ServerSocketBufferOption.ReceiveSize)
        {
            listener = new TcpConnectionListener(host, port, sendBufferSize, receiveBufferSize);
        }
        public virtual void Start()
        {
             listener.Start();
        }
#if DOTNET_CORE
        public virtual Task<Connection> AcceptAsync(CancellationToken token)
        {
            return listener.AcceptAsync(token);
        }
#else
        public virtual Connection Accept()
        {
            return listener.Accept();
        }
#endif
        public virtual void Close()
        {
            listener.Close();
        }
        
    }
}