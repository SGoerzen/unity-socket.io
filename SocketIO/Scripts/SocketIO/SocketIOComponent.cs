#region License

/*
 * SocketIO.cs
 *
 * The MIT License
 *
 * Copyright (c) 2014 Fabio Panettieri
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using WebSocketSharp;

namespace SocketIO
{
    public class SocketIOComponent : MonoBehaviour
    {
        #region Public Properties

        public string url = "ws://127.0.0.1:4567/socket.io/?EIO=3&transport=websocket";
        public bool autoConnect = false;
        public int reconnectDelay = 5;
        public float ackExpirationTime = 30f;
        public float pingInterval = 25f;
        public float pingTimeout = 60f;

        public WebSocket socket => ws;

        public string sid { get; private set; }

        public bool isConnected => connected;

        #endregion

        #region Private Properties

        private volatile bool connected;
        private volatile bool thPinging;
        private volatile bool thPong;
        private volatile bool wsConnected;

        private Thread socketThread;
        private Thread pingThread;
        private WebSocket ws;
        
        private Dictionary<string, List<Action<SocketIOEvent>>> handlers;
        private List<Ack> ackList;

        private int packetId;

        private object eventQueueLock;
        private Queue<SocketIOEvent> eventQueue;

        private object ackQueueLock;
        private Queue<Packet> ackQueue;

        private bool isOpen = false;

        #endregion

        #region Unity interface

        public void Awake()
        {
            handlers = new Dictionary<string, List<Action<SocketIOEvent>>>();
            ackList = new List<Ack>();
            sid = null;
            packetId = 0;

            ws = new WebSocket(url);
            ws.OnOpen += (s, e) => OnOpen();
            ws.OnMessage += OnMessage;
            ws.OnError += OnError;
            ws.OnClose += (s, e) => OnClose();
            wsConnected = false;

            eventQueueLock = new object();
            eventQueue = new Queue<SocketIOEvent>();

            ackQueueLock = new object();
            ackQueue = new Queue<Packet>();

            connected = false;
        }

        public void Start()
        {
            if (autoConnect)
            {
                Connect();
            }
        }

        public void Update()
        {
            lock (eventQueueLock)
            {
                while (eventQueue.Count > 0)
                {
                    EmitEvent(eventQueue.Dequeue());
                }
            }

            lock (ackQueueLock)
            {
                while (ackQueue.Count > 0)
                {
                    InvokeAck(ackQueue.Dequeue());
                }
            }

            if (wsConnected != ws.IsConnected)
            {
                wsConnected = ws.IsConnected;
                EmitEvent(wsConnected ? "connect" : "disconnect");
            }

            // GC expired acks
            if (ackList.Count == 0)
            {
                return;
            }

            if (DateTime.Now.Subtract(ackList[0].time).TotalSeconds < ackExpirationTime)
            {
                return;
            }

            ackList.RemoveAt(0);
        }

        public void OnDestroy()
        {
            socketThread?.Abort();
            pingThread?.Abort();
        }

        public void OnApplicationQuit() => Close();

        #endregion

        #region Public Interface

        public void SetHeader(string header, string value) => ws.SetHeader(header, value);

        public void Connect()
        {
            connected = true;

            socketThread = new Thread(RunSocketThread);
            socketThread.Start(ws);

            pingThread = new Thread(RunPingThread);
            pingThread.Start(ws);
        }

        public void Close()
        {
            EmitClose();
            connected = false;

            socketThread?.Abort();
            pingThread?.Abort();

            socketThread = null;
            pingThread = null;

            ws?.Close();
        }

        public void On(string ev, Action<SocketIOEvent> callback)
        {
            if (!handlers.ContainsKey(ev))
            {
                handlers[ev] = new List<Action<SocketIOEvent>>();
            }

            handlers[ev].Add(callback);
        }

        public void Off(string ev, Action<SocketIOEvent> callback)
        {
            if (!handlers.ContainsKey(ev))
            {
                return;
            }

            var l = handlers[ev];
            if (!l.Contains(callback))
            {
                return;
            }

            l.Remove(callback);
            if (l.Count == 0)
            {
                handlers.Remove(ev);
            }
        }

        public void Emit(string ev)
        {
            EmitMessage(-1, $"[\"{ev}\"]");
        }

        public void Emit(string ev, Action<JSONObject> action)
        {
            EmitMessage(++packetId, $"[\"{ev}\"]");
            ackList.Add(new Ack(packetId, action));
        }

        public void Emit(string ev, JSONObject data)
        {
            EmitMessage(-1, $"[\"{ev}\",{data}]");
        }

        public void Emit(string ev, JSONObject data, Action<JSONObject> action)
        {
            EmitMessage(++packetId, $"[\"{ev}\",{data}]");
            ackList.Add(new Ack(packetId, action));
        }

        #endregion

        #region Private Methods

        private void RunSocketThread(object obj)
        {
            var webSocket = (WebSocket) obj;
            while (connected)
            {
                if (webSocket.IsConnected)
                {
                    Thread.Sleep(reconnectDelay);
                }
                else
                {
                    webSocket.Connect();
                }
            }

            webSocket.Close();
        }

        private void RunPingThread(object obj)
        {
            var webSocket = (WebSocket) obj;

            var timeoutMilis = Mathf.FloorToInt(pingTimeout * 1000);
            var intervalMilis = Mathf.FloorToInt(pingInterval * 1000);
            
            while (connected)
            {
                if (!wsConnected)
                {
                    Thread.Sleep(reconnectDelay);
                }
                else
                {
                    thPinging = true;
                    thPong = false;

                    EmitPacket(new Packet(EnginePacketType.Ping));
                    var pingStart = DateTime.Now;

                    while (webSocket.IsConnected && thPinging &&
                           (DateTime.Now.Subtract(pingStart).TotalSeconds < timeoutMilis))
                    {
                        Thread.Sleep(200);
                    }

                    if (!thPong)
                    {
                        webSocket.Close();
                    }

                    Thread.Sleep(intervalMilis);
                }
            }
        }

        private void EmitMessage(int id, string raw)
        {
            EmitPacket(new Packet(EnginePacketType.Message, SocketPacketType.Event, 0, "/", id, new JSONObject(raw)));
        }

        private void EmitClose()
        {
            EmitPacket(
                new Packet(EnginePacketType.Message, SocketPacketType.Disconnect, 0, "/", -1, new JSONObject("")));
            EmitPacket(new Packet(EnginePacketType.Close));
        }

        private void EmitPacket(Packet packet)
        {
            try
            {
                ws.Send(Encoder.Encode(packet));
            }
            catch (SocketIOException ex)
            {
                Debug.LogError(ex.Message);
            }
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            var packet = Decoder.Decode(e);

            switch (packet.enginePacketType)
            {
                case EnginePacketType.Open:
                    HandleOpen(packet);
                    break;
                case EnginePacketType.Close:
                    EmitEvent("close");
                    break;
                case EnginePacketType.Ping:
                    HandlePing();
                    break;
                case EnginePacketType.Pong:
                    HandlePong();
                    break;
                case EnginePacketType.Message:
                    HandleMessage(packet);
                    break;
            }
        }

        private void HandleOpen(Packet packet)
        {
            sid = packet.json["sid"].str;
            OnOpen();
        }

        private void OnOpen()
        {
            if (isOpen)
                return;
            EmitEvent("open");
            isOpen = true;
        }

        private void OnClose()
        {
            if (!isOpen)
                return;
            EmitEvent("close");
            isOpen = false;
        }

        private void HandlePing()
        {
            EmitPacket(new Packet(EnginePacketType.Pong));
        }

        private void HandlePong()
        {
            thPong = true;
            thPinging = false;
        }

        private void HandleMessage(Packet packet)
        {
            if (packet.json == null)
            {
                return;
            }

            if (packet.socketPacketType == SocketPacketType.Ack)
            {
                for (var i = 0; i < ackList.Count; i++)
                {
                    if (ackList[i].packetId != packet.id)
                    {
                        continue;
                    }

                    lock (ackQueueLock)
                    {
                        ackQueue.Enqueue(packet);
                    }

                    return;
                }
            }

            if (packet.socketPacketType == SocketPacketType.Event)
            {
                var e = Parser.Parse(packet.json);
                lock (eventQueueLock)
                {
                    eventQueue.Enqueue(e);
                }
            }
        }

        private void OnError(object sender, ErrorEventArgs e) => EmitEvent("error");
        private void EmitEvent(string type) => EmitEvent(new SocketIOEvent(type));

        private void EmitEvent(SocketIOEvent ev)
        {
            if (!handlers.ContainsKey(ev.name))
                return;

            foreach (var handler in handlers[ev.name])
            {
                try
                {
                    handler(ev);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex.Message);
                }
            }
        }

        private void InvokeAck(Packet packet)
        {
            for (var i = 0; i < ackList.Count; i++)
            {
                if (ackList[i].packetId != packet.id)
                {
                    continue;
                }

                var ack = ackList[i];
                ackList.RemoveAt(i);
                ack.Invoke(packet.json);
                return;
            }
        }

        #endregion
    }
}