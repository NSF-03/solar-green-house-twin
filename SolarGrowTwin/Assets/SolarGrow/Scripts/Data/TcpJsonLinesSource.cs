using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SolarGrow
{
    /// <summary>
    /// TCP client for a data bridge (protocol in README.md): UTF-8, one JSON object per line.
    /// A background thread connects (auto-reconnect with backoff 1, 2, 4 ... 15 s), reads lines, parses them and
    /// queues them; it also sends queued commands and a ping every 10 s. Tick() on the main thread drains the
    /// queues and raises OnState once per greenhouse with the LAST frame only. The thread never touches Unity objects.
    /// </summary>
    public sealed class TcpJsonLinesSource : ITelemetrySource, ICommandSink
    {
        public event Action<TwinStateFrame> OnState;
        public event Action<string> OnStatus;

        public string Host { get; private set; }
        public int Port { get; private set; }
        public double PingIntervalSeconds { get; set; }
        public double ConnectTimeoutSeconds { get; set; }
        public double MaxBackoffSeconds { get; set; }
        /// <summary>No byte at all for this long (the bridge answers pings) = connection presumed dead.</summary>
        public double SilenceTimeoutSeconds { get; set; }
        public int MaxLineBytes { get; set; }

        readonly ConcurrentQueue<JObject> frames = new ConcurrentQueue<JObject>();
        readonly ConcurrentQueue<string> statuses = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        readonly Dictionary<string, JObject> latest = new Dictionary<string, JObject>();
        readonly List<string> latestOrder = new List<string>();
        readonly object sync = new object();
        readonly UTF8Encoding utf8 = new UTF8Encoding(false);

        Thread thread;
        volatile bool running;
        volatile bool connected;
        TcpClient client;
        int seq;
        double lastRttMs = -1;
        DateTime pingSentUtc = DateTime.MinValue;
        string bridgeInfo = "";

        public TcpJsonLinesSource(string host, int port)
        {
            Host = string.IsNullOrEmpty(host) ? "127.0.0.1" : host.Trim();
            Port = port;
            PingIntervalSeconds = 10;
            ConnectTimeoutSeconds = 3;
            MaxBackoffSeconds = 15;
            SilenceTimeoutSeconds = 35;
            MaxLineBytes = 4 * 1024 * 1024;
        }

        public bool Connected { get { return connected; } }

        /// <summary>Round trip of the last ping in ms, or -1.</summary>
        public double LastRttMs { get { lock (sync) return lastRttMs; } }

        /// <summary>Text from the bridge's hello message.</summary>
        public string BridgeInfo { get { lock (sync) return bridgeInfo; } }

        public void Connect()
        {
            if (thread != null) return;
            running = true;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "SolarGrow TCP " + Host + ":" + Port;
            thread.Start();
        }

        public void Disconnect()
        {
            running = false;
            TcpClient c;
            lock (sync) c = client;
            if (c != null)
            {
                try { c.Close(); } catch (Exception) { }
            }
            Thread t = thread;
            thread = null;
            if (t != null && !t.Join(2000)) RaiseStatus("TCP thread still closing (it stops on its own).");
            connected = false;
            string dropped;
            while (outbox.TryDequeue(out dropped)) { }
        }

        public void Tick()
        {
            string s;
            while (statuses.TryDequeue(out s)) RaiseStatus(s);

            JObject f;
            while (frames.TryDequeue(out f))
            {
                string gh = TwinJson.GetString(f, "greenhouse") ?? "";
                if (!latest.ContainsKey(gh)) latestOrder.Add(gh);
                latest[gh] = f;
            }
            if (latestOrder.Count == 0) return;
            Action<TwinStateFrame> handler = OnState;
            for (int i = 0; i < latestOrder.Count; i++)
            {
                if (handler != null) handler(new TwinStateFrame(latest[latestOrder[i]]));
            }
            latest.Clear();
            latestOrder.Clear();
        }

        public bool SendCommand(string greenhouse, string device, string value)
        {
            if (!connected) return false;
            JObject cmd = new JObject();
            cmd["type"] = "cmd";
            cmd["greenhouse"] = greenhouse;
            cmd["device"] = device;
            cmd["value"] = value;
            cmd["source"] = "unity-manual";
            cmd["seq"] = Interlocked.Increment(ref seq);
            cmd["sent_at_utc"] = TwinJson.NowIso();
            outbox.Enqueue(cmd.ToString(Formatting.None));
            return true;
        }

        void RaiseStatus(string text)
        {
            Action<string> handler = OnStatus;
            if (handler != null) handler(text);
        }

        void Post(string text)
        {
            statuses.Enqueue(text);
        }

        // ------------------------------------------------------------------ network thread

        void Run()
        {
            double backoff = 1.0;
            while (running)
            {
                TcpClient c = null;
                try
                {
                    Post("Connecting to " + Host + ":" + Port + " ...");
                    c = new TcpClient();
                    c.NoDelay = true;
                    lock (sync) client = c; // so Disconnect() can abort a pending connect
                    IAsyncResult ar = c.BeginConnect(Host, Port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(ConnectTimeoutSeconds)))
                        throw new TimeoutException("no answer within " + ConnectTimeoutSeconds + " s");
                    c.EndConnect(ar);
                    if (!running) break;
                    connected = true;
                    backoff = 1.0;
                    Post("Connected to " + Host + ":" + Port);
                    ReadLoop(c);
                    if (running) Post("Bridge closed the connection.");
                }
                catch (Exception e)
                {
                    if (running) Post("Connection error: " + e.Message);
                }
                finally
                {
                    connected = false;
                    lock (sync) client = null;
                    if (c != null)
                    {
                        try { c.Close(); } catch (Exception) { }
                    }
                    string stale;
                    while (outbox.TryDequeue(out stale)) { }
                }
                if (!running) break;
                Post("Reconnecting in " + backoff.ToString("0") + " s");
                SleepWhileRunning(backoff);
                backoff = Math.Min(backoff * 2.0, MaxBackoffSeconds);
            }
        }

        void SleepWhileRunning(double seconds)
        {
            DateTime until = DateTime.UtcNow.AddSeconds(seconds);
            while (running && DateTime.UtcNow < until) Thread.Sleep(100);
        }

        void ReadLoop(TcpClient c)
        {
            NetworkStream stream = c.GetStream();
            stream.WriteTimeout = 3000;
            Socket socket = c.Client;
            byte[] buffer = new byte[16384];
            MemoryStream line = new MemoryStream();
            bool overflow = false;
            DateTime lastRx = DateTime.UtcNow;
            DateTime lastPing = DateTime.MinValue;

            while (running)
            {
                string outLine;
                while (outbox.TryDequeue(out outLine)) WriteLine(stream, outLine);

                DateTime now = DateTime.UtcNow;
                if ((now - lastPing).TotalSeconds >= PingIntervalSeconds)
                {
                    lock (sync) pingSentUtc = now;
                    WriteLine(stream, "{\"type\":\"ping\"}");
                    lastPing = now;
                }
                if ((now - lastRx).TotalSeconds > SilenceTimeoutSeconds)
                    throw new IOException("nothing received for " + SilenceTimeoutSeconds + " s");

                if (!socket.Poll(100000, SelectMode.SelectRead)) continue;
                int n = stream.Read(buffer, 0, buffer.Length);
                if (n <= 0) return;
                lastRx = DateTime.UtcNow;
                for (int i = 0; i < n; i++)
                {
                    byte b = buffer[i];
                    if (b == (byte)'\n')
                    {
                        if (overflow) Post("Dropped a line longer than " + MaxLineBytes + " bytes.");
                        else HandleLine(utf8.GetString(line.GetBuffer(), 0, (int)line.Length));
                        line.SetLength(0);
                        overflow = false;
                    }
                    else if (line.Length < MaxLineBytes)
                    {
                        line.WriteByte(b);
                    }
                    else
                    {
                        overflow = true;
                    }
                }
            }
        }

        void WriteLine(NetworkStream stream, string text)
        {
            byte[] bytes = utf8.GetBytes(text + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }

        void HandleLine(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return;
            JObject obj;
            try
            {
                obj = TwinJson.ParseObject(text);
            }
            catch (Exception e)
            {
                Post("Skipped a line that is not JSON: " + e.Message);
                return;
            }
            if (obj == null) return;
            string type = TwinJson.GetString(obj, "type");
            if (type == "state" || (type == null && obj["greenhouse"] != null))
            {
                frames.Enqueue(obj);
            }
            else if (type == "hello")
            {
                string info = (TwinJson.GetString(obj, "bridge") ?? "bridge") + " v" + (TwinJson.GetString(obj, "version") ?? "?");
                JArray houses = obj["greenhouses"] as JArray;
                if (houses != null)
                {
                    string list = "";
                    for (int i = 0; i < houses.Count; i++)
                    {
                        string h = TwinJson.AsString(houses[i]);
                        if (h != null) list = list.Length == 0 ? h : list + ", " + h;
                    }
                    info += ", greenhouses " + list;
                }
                lock (sync) bridgeInfo = info;
                Post("Bridge hello: " + info);
            }
            else if (type == "pong")
            {
                lock (sync)
                {
                    if (pingSentUtc != DateTime.MinValue) lastRttMs = (DateTime.UtcNow - pingSentUtc).TotalMilliseconds;
                }
            }
            else if (type == "error" || type == "ack" || type == "info")
            {
                Post("Bridge " + type + ": " + (text.Length > 300 ? text.Substring(0, 300) + "..." : text));
            }
        }
    }
}
