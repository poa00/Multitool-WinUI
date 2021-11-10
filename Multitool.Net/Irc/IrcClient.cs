﻿using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Windows.Foundation;

namespace Multitool.Net.Irc
{
    public abstract class IrcClient : IIrcClient
    {
        private readonly CancellationTokenSource rootCancelToken = new();
        private Thread receiveThread;
        private long disconnected;
        private bool disposed;

        public IrcClient()
        {
            receiveThread = new(ReceiveData);
        }

        public event TypedEventHandler<IIrcClient, string> MessageReceived;

        #region properties
        public CancellationTokenSource CancellationToken => rootCancelToken;

        public WebSocketState ClientState => Socket.State;

        public bool Connected { get; protected set; }

        public string NickName { get; set; }

        protected bool Disposed => disposed;

        protected ClientWebSocket Socket { get; } = new();

        protected Thread ReceiveThread => receiveThread;
        #endregion

        #region public methods
        /// <inheritdoc/>
        public abstract Task SendMessage(string message);
        /// <inheritdoc/>
        public abstract Task Join(string channel);
        /// <inheritdoc/>
        public abstract Task Part(string channel);

        /// <inheritdoc/>
        public virtual async Task Connect(Uri uri)
        {
            await Socket.ConnectAsync(uri, CancellationToken.Token);
        }

        /// <inheritdoc/>
        public async Task Connect(Uri channel, CancellationToken cancellationToken)
        {
            await Socket.ConnectAsync(channel, cancellationToken);
        }

        public async Task Disconnect()
        {
            if (ClientState != WebSocketState.Closed)
            {
                Interlocked.Exchange(ref disconnected, 1);
                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User closed the connection", CancellationToken.Token);
                Trace.TraceInformation("Irc client disconnected");
            }
            else
            {
                Trace.TraceWarning("Client already disconnected.");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            CheckDisposed();
            rootCancelToken.Cancel();
            Disconnect()
                .ContinueWith((Task t) => Socket.Dispose());
            disposed = true;
        }

        public CancellationTokenSource GetCancellationToken()
        {
            return CancellationTokenSource.CreateLinkedTokenSource(rootCancelToken.Token);
        }
        #endregion

        #region protected methods
        protected abstract void OnMessageReceived(string message);

        protected void AssertConnectionValid()
        {
            if (ClientState != WebSocketState.Open)
            {
                switch (ClientState)
                {
                    case WebSocketState.None:
                        throw new InvalidOperationException("Client status is unknown");
                    case WebSocketState.Connecting:
                        throw new InvalidOperationException("Client is connecting");
                    case WebSocketState.CloseSent:
                        throw new InvalidOperationException("Client is closing it's connection");
                    case WebSocketState.CloseReceived:
                        throw new InvalidOperationException("Client has received close request from the connection endpoint");
                    case WebSocketState.Closed:
                        throw new InvalidOperationException("Client connection is closed");
                    case WebSocketState.Aborted:
                        throw new InvalidOperationException($"{nameof(ClientState)} == WebSocketState.Aborted");
                    default:
                        throw new ArgumentException($"Unkown WebSocketState state, argument name : {nameof(ClientState)}");
                }
            }
        }

        protected void AssertChannelNameValid(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentException("Channel name cannot be empty", nameof(channel));
            }
        }

        protected void InvokeMessageReceived(string message)
        {
            Task.Run(() => MessageReceived?.Invoke(this, message));
        }
        #endregion

        #region private methods
        protected void CheckDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(string.Empty);
            }
        }

        private async void ReceiveData(object obj)
        {
            ArraySegment<byte> data = new(new byte[1024]);
            CancellationTokenSource tokenSource = GetCancellationToken();
            do
            {
                if (Interlocked.Read(ref disconnected) == 1)
                {
                    break;
                }
                await Socket.ReceiveAsync(data, tokenSource.Token);
                if (data.Count != 0)
                {
                    OnMessageReceived(Encoding.UTF8.GetString(data));
                }
                for (int i = 0; i < data.Count; i++)
                {
                    data[i] = default;
                }
            }
            while (true);
        }
        #endregion
    }
}