using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DonChan.TelemetryProbe
{
    // Minimal, read-only HTTP/1.1 endpoint. TcpListener avoids Windows HttpListener URL ACL setup.
    internal sealed class TelemetryServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly TelemetryFeed _feed;
        private readonly int _port;
        private volatile bool _stopped;
        private readonly Thread _thread;

        internal TelemetryServer(TelemetryFeed feed, int port)
        {
            _feed = feed; _port = port;
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start(8);
            _thread = new Thread(Run) { IsBackground = true, Name = "DonChanTelemetryHTTP" };
            _thread.Start();
        }

        public void Dispose()
        {
            _stopped = true;
            _listener.Stop();
        }

        private void Run()
        {
            while (!_stopped)
            {
                try
                {
                    using (var client = _listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 1000; client.SendTimeout = 1000;
                        using (var stream = client.GetStream()) Handle(stream);
                    }
                }
                catch (Exception)
                {
                    // Disconnected/malformed clients must not affect game observation or spam logs.
                    if (_stopped) return;
                }
            }
        }

        private void Handle(NetworkStream stream)
        {
            var header = new StringBuilder();
            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            while (header.Length < 4096 && DateTime.UtcNow < deadline)
            {
                int b = stream.ReadByte();
                if (b < 0) return;
                header.Append((char)b);
                int n = header.Length;
                if (n >= 4 && header[n - 4] == '\r' && header[n - 3] == '\n'
                    && header[n - 2] == '\r' && header[n - 1] == '\n') break;
            }
            string text = header.ToString();
            if (!text.EndsWith("\r\n\r\n", StringComparison.Ordinal)) { Reply(stream, 400, "{}"); return; }
            string[] lines = text.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            string[] request = lines[0].Split(' ');
            if (request.Length != 3 || request[0] != "GET") { Reply(stream, 405, "{}"); return; }
            string expectedHost = "127.0.0.1:" + _port.ToString(CultureInfo.InvariantCulture);
            bool host = false;
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon < 0) continue;
                string key = lines[i].Substring(0, colon).Trim();
                string value = lines[i].Substring(colon + 1).Trim();
                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) host = value == expectedHost;
                // Only server-to-server polling; deny browser-originated requests including DNS rebinding.
                if (key.Equals("Origin", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Sec-Fetch-Site", StringComparison.OrdinalIgnoreCase)) { Reply(stream, 403, "{}"); return; }
            }
            if (!host) { Reply(stream, 403, "{}"); return; }
            string[] target = request[1].Split('?');
            if (target[0] != "/v1/feed" || target.Length > 2) { Reply(stream, 404, "{}"); return; }
            string session = ""; long after = 0;
            if (target.Length == 2)
            {
                foreach (string pair in target[1].Split('&'))
                {
                    string[] kv = pair.Split('=');
                    if (kv.Length != 2) { Reply(stream, 400, "{}"); return; }
                    if (kv[0] == "sessionId") session = kv[1];
                    else if (kv[0] == "after")
                    {
                        if (!long.TryParse(kv[1], NumberStyles.None, CultureInfo.InvariantCulture, out after)
                            || after < 0 || after > 9007199254740991L) { Reply(stream, 400, "{}"); return; }
                    }
                    else { Reply(stream, 400, "{}"); return; }
                }
            }
            if (session.Length != 0)
            {
                Guid ignored;
                if (session.Length != 32 || !Guid.TryParseExact(session, "N", out ignored)) { Reply(stream, 400, "{}"); return; }
            }
            Reply(stream, 200, _feed.Read(session, after));
        }

        private static void Reply(Stream stream, int status, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            string header = "HTTP/1.1 " + status + (status == 200 ? " OK" : " Error")
                + "\r\nContent-Type: application/json; charset=utf-8\r\nCache-Control: no-store"
                + "\r\nConnection: close\r\nContent-Length: " + bytes.Length + "\r\n\r\n";
            byte[] headers = Encoding.ASCII.GetBytes(header);
            stream.Write(headers, 0, headers.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
