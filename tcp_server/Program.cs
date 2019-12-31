using System;
using System.Threading.Tasks;
using System.Threading;

namespace LowLevelTransport.Tcp
{
    class Program
    {
        private static readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private static TcpConnectionListener listener = null;
        static void Main(string[] args)
        {
            listener = new TcpConnectionListener("127.0.0.1", 8888);
            listener.Start();
            Task.Run(AcceptLoop);
            while(true)
            {
                string key = Console.ReadLine();
                byte[] sendBytes = System.Text.Encoding.Default.GetBytes(key);
                listener.Send(sendBytes);
            }
        }
        public static async Task AcceptLoop()
        {
            try
            {
                CancellationToken token = _cancellation.Token;
                while(!token.IsCancellationRequested)
                {
                    TcpConnection conn = await listener.AcceptAsync(token);
                    _ = Task.Run( () =>
                    {
                        while(true)
                        {
                            byte[] packet = conn.Receive();
                            if(packet != null)
                            {
                                string recvStr = System.Text.Encoding.Default.GetString(packet, 0, packet.Length);
                                Console.WriteLine($"Rec {recvStr}");
                            }
                            Thread.Sleep(300);
                        }
                    });
                }
            }
            catch(Exception)
            {
            }
        }
    }
}