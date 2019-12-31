using System.Threading;
using System.Collections.Generic;
using System;

namespace LowLevelTransport.Tcp
{
    public partial class TcpConnectionListener
    {
        private Timer timer;
        private void AliveCheckLoop()
        {
            timer = new Timer(CheckTimeout, null, 5000, 5000);
        }
        private void CheckTimeout(object obj)
        {
            var deadSession = new List<TcpServerConnection>();
            long timeNow = GetMillisecondStamp();

            lock(clients)
            {
                foreach(var state in clients.Values)
                {
                    if(!state.CheckAlive(timeNow))
                    {
                        deadSession.Add(state);
                    }
                }
            }

            foreach(var needClose in deadSession)
            {
                Remove(needClose.socket);
                needClose.Close();
                Console.WriteLine("Socket close");
            }
            deadSession.Clear();
        }
        private void StopTimer()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}