using System.Net;
using System.Net.Sockets;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace tcpclient
{
    class Program111
    {
        static void Main111(string[] args)
        {
            try
            {
                string ipAddr = "127.0.0.1";
                TcpClient sender = new TcpClient(ipAddr, 8888);
                sender.NoDelay = true;
                Console.WriteLine("Socket connected to {0}", sender.Client.RemoteEndPoint.ToString());
                NetworkStream stream = null;
                while(true)
                {
                    string message = Console.ReadLine();
                    if(message == "exit")
                    {
                        break;
                    }
                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);

                    stream = sender.GetStream();
                    stream.Write(data, 0, data.Length);
                    Console.WriteLine("Sent: {0}", message);

                    data = new Byte[256];
                    String responseData = String.Empty;
                    Int32 bytes = stream.Read(data, 0, data.Length);
                    responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                    Console.WriteLine("Received: {0}", responseData);
                }
                stream.Close();
                sender.Close();
            }
            catch(ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch(SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }

            Console.WriteLine("\n Press Enter to continue...");
            Console.Read();
        }
    }
}