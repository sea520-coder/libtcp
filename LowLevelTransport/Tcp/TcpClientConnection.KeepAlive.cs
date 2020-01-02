using System;
using System.Threading;

namespace LowLevelTransport.Tcp
{
    public partial class TcpClientConnection
    {
        private long reconnectTimes = 0;
        private Timer timer;
        protected void InitKeepAliveTimer()
        {
            timer = new Timer(KeepAlive, null, 5000, 5000);
        }
        private void KeepAlive(object obj)
        {
            if(Interlocked.Read(ref reconnectTimes) > (long)5)
            {
                HandleDisconnect(new Exception("receonnct too many times"));
                return;
            }
            Interlocked.Add(ref reconnectTimes, 1);
            byte[] data = new byte[4]{ 0,0,0,0 };
            sendQueue.Enqueue(data);
        }
        private void HandleHeartbeat()
        {
            Interlocked.Exchange(ref reconnectTimes, 0);
        }
        private void StopTimer()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}