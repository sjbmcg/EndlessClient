﻿// Original Work Copyright (c) Ethan Moffat 2014-2016
// This file is subject to the GPL v2 License
// For additional details, see the LICENSE file

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EOLib.Net
{
	[Flags]
	public enum ConnectResult
	{
		/// <summary>
		/// Connect succeeded
		/// </summary>
		Success,
		/// <summary>
		/// Endpoint is invalid (most likely null)
		/// </summary>
		InvalidEndpoint,
		/// <summary>
		/// Socket is invalid (most likely disposed)
		/// </summary>
		InvalidSocket,
		/// <summary>
		/// WinSock error code
		/// </summary>
		SocketError
	}

	public class AsyncSocket : IAsyncSocket
	{
		private readonly Socket _socket;

		public AsyncSocket(AddressFamily family, SocketType type, ProtocolType protocol)
		{
			_socket = new Socket(family, type, protocol);
		}

		public async Task<int> SendAsync(byte[] data, CancellationToken ct)
		{
			var task = Task.Run(() => BlockingSend(data), ct);
			
			await task;
			return task.Result;
		}

		public async Task<byte[]> ReceiveAsync(int bytes, CancellationToken ct)
		{
			var task = Task.Run(() => BlockingReceive(bytes), ct);

			await task;
			return task.Result;
		}

		public async Task<bool> CheckIsConnectedAsync(CancellationToken ct)
		{
			var task = Task.Run(() => BlockingIsConnected(), ct);

			await task;
			return task.Result;
		}

		public async Task<ConnectResult> ConnectAsync(EndPoint endPoint, CancellationToken ct)
		{
			var task = Task.Run(() => BlockingConnect(endPoint), ct);

			await task;
			return task.Result;
		}

		public async Task DisconnectAsync(CancellationToken ct)
		{
			var task = Task.Run(() => BlockingDisconnect(), ct);
			
			await task;
		}

		private int BlockingSend(byte[] data)
		{
			return _socket.Send(data);
		}

		private byte[] BlockingReceive(int bytes)
		{
			var ret = new byte[bytes];
			var numBytes = 0;

			do
			{
				var localBytes = new byte[bytes];
				var startIndex = numBytes;

				numBytes += _socket.Receive(localBytes, bytes, SocketFlags.None);
				Array.Copy(localBytes, 0, ret, startIndex, numBytes - startIndex);
			} while (numBytes < bytes);

			return ret;
		}

		private bool BlockingIsConnected()
		{
			var dataAvailableOrConnectionReset = _socket.Poll(1000, SelectMode.SelectRead);
			var dataAvailable = _socket.Available > 0;
			return dataAvailableOrConnectionReset && dataAvailable;
		}

		private ConnectResult BlockingConnect(EndPoint endPoint)
		{
			ConnectResult result;

			try
			{
				_socket.Connect(endPoint);
				result = ConnectResult.Success;
			}
			catch(ArgumentNullException)
			{
				result = ConnectResult.InvalidEndpoint;
			}
			catch(SocketException sex)
			{
				result = ConnectResult.SocketError | (ConnectResult)sex.ErrorCode;
			}
			catch(ObjectDisposedException)
			{
				result = ConnectResult.InvalidSocket;
			}
			catch(InvalidOperationException)
			{
				result = ConnectResult.InvalidSocket;
			}

			return result;
		}

		private void BlockingDisconnect()
		{
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Disconnect(false);
		}

		#region IDisposable

		~AsyncSocket()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				_socket.Dispose();
			}
		}

		#endregion
	}
}