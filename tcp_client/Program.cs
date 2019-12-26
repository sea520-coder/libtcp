using System.Net;
using System.Net.Sockets;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LowLevelTransport.Tcp
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpClientConnection tccon = new TcpClientConnection("127.0.0.1", 8888);
            while(true)
            {
                string key = Console.ReadLine();
                byte[] buff = System.Text.Encoding.Default.GetBytes(key);
                tccon.SendBytes(buff);
            }
        }
    }
}