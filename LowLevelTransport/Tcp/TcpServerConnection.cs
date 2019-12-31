using System.Threading;

namespace LowLevelTransport.Tcp
{
    public class TcpServerConnection : TcpConnection
    {
        internal long lastAliveTime;
        private static readonly long keepAliveTimeout = 30 * 1000;
        internal bool CheckAlive(long timeNow) =>
            (timeNow - Interlocked.Read(ref lastAliveTime)) < keepAliveTimeout;
    }
}