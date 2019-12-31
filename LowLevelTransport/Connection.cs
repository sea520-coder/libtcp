using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LowLevelTransport.Utils;
using System.Collections.Generic;
using LowLevelTransport.Udp;
#if DOTNET_CORE
using System.Threading.Channels;
#endif

namespace LowLevelTransport
{
    public class Connection
    {
        public virtual void SendBytes(byte[] buff, SendOption sendOption = SendOption.None)
        {
            throw new NotImplementedException();
        }
        #if DOTNET_CORE
        public virtual byte[] Receive()
        {
            throw new NotImplementedException();
        }
        public virtual Task<byte[]> ReceiveAsync(CancellationToken cToken)
        {
            throw new NotImplementedException();
        }
        public virtual Task<byte[]> ReceiveAsync(CancellationToken cToken, TimeSpan t)
        {
            throw new NotImplementedException();
        }
#else
        public virtual byte[] Receive()
        {
             throw new NotImplementedException();
        }
#endif
        public  virtual bool IsClosed()
        {
            throw new NotImplementedException();
        }
        public virtual void Close()
        {
            throw new NotImplementedException();
        }
        public Action TryReconnectCallback;
        public Action<bool> ReconnectFinishCallback;
        public void Tick()
        {
        }
        public void Flush()
        {
        }
    }
}