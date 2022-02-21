using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace DiscordMotusBot
{
    class WebServer
    {
        private static int _Port = 8080;
        private static IPAddress _localAddress = IPAddress.Parse("0.0.0.0");
        private static TcpListener _Listener;
        private static Thread _ThreadServer;
        private static bool _IsAlive = false;

        private static void ThreadServer()
        {
            _Listener = new TcpListener(_localAddress, _Port);
            _Listener.Start();
            while(_IsAlive == true)
            {
                var client = _Listener.AcceptTcpClient();
                var stream = client.GetStream();
/*
                var buffer = new byte[256];
                var length = stream.Read(buffer, 0, buffer.Length);
                var message = Encoding.UTF8.GetString(buffer, 0, length);
                Console.WriteLine("Received: ");
                Console.WriteLine(message);
*/
                var pageContent = "Mo-mo-motus, oh oh oh oooh!";
                stream.Write(
                  Encoding.UTF8.GetBytes(
                    "HTTP/1.0 200 OK" + Environment.NewLine
                    + "Content-Length: " + pageContent.Length + Environment.NewLine
                    + "Content-Type: text/plain" + Environment.NewLine
                    + Environment.NewLine
                    + pageContent
                    + Environment.NewLine
                    + Environment.NewLine));
            }
            _Listener.Stop();
        }
  
        public static void Start()
        {
            if (_IsAlive == false)
            {
                _IsAlive = true;
                Console.WriteLine("Starting web server.");
                _ThreadServer = new Thread(new ThreadStart(WebServer.ThreadServer));
                _ThreadServer.Start();
            }
            else
            {
                Console.WriteLine("Server already alive!!!");
            }
        }
    }  
}