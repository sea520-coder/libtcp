using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System;
using System.Threading;

namespace tcp_server
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                int port = 8888;
                TcpListener server = new TcpListener(IPAddress.Any, port);
                server.Start();

                byte[] bytes = new byte[1024];
                string data;
                while(true)
                {
                    Console.Write("Waiting for a connection... ");

                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    NetworkStream stream = client.GetStream();
                    int i;
                    i = stream.Read(bytes, 0, bytes.Length);
                    while(i != 0)
                    {
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        Console.WriteLine(String.Format("Received: {0}", data));

                        data = data.ToUpper();
                        byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);
                        stream.Write(msg, 0, msg.Length);
                        Console.WriteLine(string.Format("Sent: {0}", data));
                        i = stream.Read(bytes, 0, bytes.Length);
                    }
                    client.Close();
                }
            }
            catch(SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            Console.WriteLine("Hit enter to continue...");
            Console.Read();
        }
        //心跳包
    }
}