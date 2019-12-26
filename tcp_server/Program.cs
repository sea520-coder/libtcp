using System;

namespace LowLevelTransport.Tcp
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpConnectionListener listener = new TcpConnectionListener("127.0.0.1", 8888);
            listener.Start();
            while(true)
            {
                string key = Console.ReadLine();
                byte[] sendBytes = System.Text.Encoding.Default.GetBytes(key);
                listener.Send(sendBytes);
            }
        }
    }
}