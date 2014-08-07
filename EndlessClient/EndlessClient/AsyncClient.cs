﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

using EOLib;

namespace EndlessClient
{
	public class SocketDataWrapper
	{
		public enum DataReceiveState
		{
			ReadLen1,
			ReadLen2,
			ReadData,
			NoData
		}

		public static int BUFFER_SIZE = 1;

		public byte[] Data;
		public DataReceiveState State;
		public byte[] RawLength;

		public SocketDataWrapper()
		{
			Data = new byte[BUFFER_SIZE];
			State = DataReceiveState.ReadLen1;
			RawLength = new byte[2];
		}
	}

	public abstract class AsyncClient : IDisposable
	{
		private static readonly object disposingLockObject = new object();

		private Socket m_sock;
		private EndPoint m_serverEndpoint;
		private bool m_disposing = false;
		private bool m_connected = false;
		private AutoResetEvent m_sendLock;

		protected ClientPacketProcessor m_packetProcessor;

		public bool Connected { get { return m_connected; } }

		//Set up socket to prepare for connection
		public AsyncClient()
		{
			m_sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		}

		/// <summary>
		/// Connects to the server specified when the constructor was called
		/// </summary>
		/// <returns>True if successful, false otherwise</returns>
		public bool ConnectToServer(string ipOrHostname, int port)
		{
			if (m_connected)
			{
				throw new Exception("Client has already connected to the server. Disconnect first before connecting again.");
			}

			if(m_serverEndpoint == null)
			{
				IPAddress ip;
				if (!System.Net.IPAddress.TryParse(ipOrHostname, out ip))
				{
					System.Net.IPHostEntry entry = System.Net.Dns.GetHostEntry(ipOrHostname);
					if (entry.AddressList.Length == 0)
						return false;
					else
						ipOrHostname = entry.AddressList[0].ToString();
				}

				try
				{
					m_serverEndpoint = new IPEndPoint(IPAddress.Parse(ipOrHostname), port);
				}
				catch
				{
					return false;
				}
			}

			IAsyncResult res = null;
			try
			{
				if (m_sock != null && m_sock.Connected)
				{
					m_connected = true;
					return true;
				}

				if (m_sendLock != null)
				{
					m_sendLock.Close();
					m_sendLock = null;
				}

				//this is stuff that would normally go in the constructor
				m_sendLock = new AutoResetEvent(true);
				m_packetProcessor = new ClientPacketProcessor(); //reset the packet processor to allow for new multis


				m_sock.Connect(m_serverEndpoint);
				SocketDataWrapper wrap = new SocketDataWrapper();
				res = m_sock.BeginReceive(wrap.Data, 0, wrap.Data.Length, SocketFlags.None, _recvCB, wrap);
				m_connected = true;

				Handlers.Init.Initialize();

				if(!Handlers.Init.CanProceed)
				{
					//pop up some dialogs when this fails (see EOGame::TryConnectToServer)
					m_sock.EndReceive(res);
					return (m_connected = false);
				}

				m_packetProcessor.SetMulti(Handlers.Init.Data.emulti_d, Handlers.Init.Data.emulti_e);
				UpdateSequence(Handlers.Init.Data.seq_1 * 7 - 11 + Handlers.Init.Data.seq_2 - 2);
				World.Instance.MainPlayer.SetPlayerID(Handlers.Init.Data.clientID);
				
				//send confirmation of init data to server
				Packet confirm = new Packet(PacketFamily.Connection, PacketAction.Accept);
				confirm.AddShort(m_packetProcessor.SendMulti);
				confirm.AddShort(m_packetProcessor.RecvMulti);
				confirm.AddShort(Handlers.Init.Data.clientID);
				
				if(!SendPacket(confirm))
				{
					m_sock.EndReceive(res);
					return (m_connected = false);
				}
			}
			catch
			{
				if(res != null)
					m_sock.EndReceive(res);
				m_connected = false;
			}

			return m_connected;
		}

		/// <summary>
		/// Disconnects from the server and recreates the socket. 
		/// </summary>
		public void Disconnect()
		{
			if (!m_connected)
			{
				throw new Exception("Unable to disconnect without connecting");
			}

			m_connected = false;
			m_sock.Shutdown(SocketShutdown.Both);
			//m_sock.Disconnect(false);
			m_sock.Close();

			Socket newSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			m_sock = newSock;
		}

		//---------------------------------------
		// Send a packet to the server
		//---------------------------------------

		public bool SendPacket(Packet pkt)
		{
			byte[] toSend = pkt.Get();
			m_packetProcessor.Encode(ref toSend);

			byte[] data = new byte[toSend.Length + 2];
			byte[] len = Packet.EncodeNumber(toSend.Length, 2);

			Array.Copy(len, 0, data, 0, 2);
			Array.Copy(toSend, 0, data, 2, toSend.Length);

			if (!m_sendLock.WaitOne(Constants.ResponseTimeout))//do one send at a time
				return false;

			SocketDataWrapper wrap = new SocketDataWrapper();
			wrap.Data = data;
			try
			{
				m_sock.BeginSend(wrap.Data, 0, wrap.Data.Length, SocketFlags.None, _sendCB, wrap);
			}
			catch(SocketException)
			{
				//connection aborted by hardware errors produce a socketexception.
				return false;
			}
			return true;
		}

		public bool SendRaw(Packet pkt)
		{
			pkt.WritePos = 0;
			pkt.AddShort((short)pkt.Length);

			m_sendLock.WaitOne();

			SocketDataWrapper wrap = new SocketDataWrapper();
			wrap.Data = pkt.Get();
			try
			{
				m_sock.BeginSend(wrap.Data, 0, wrap.Data.Length, SocketFlags.None, _sendCB, wrap);
			}
			catch(SocketException)
			{
				return false;
			}
			return true;
		}

		//---------------------------------------
		// Overridable methods in derived classes
		// Currently just _handle
		//---------------------------------------

		protected abstract void _handle(object state);

		//-----------------------------------
		//send and receive callback functions
		//-----------------------------------

		private void _recvCB(IAsyncResult res)
		{
			lock (disposingLockObject)
				if (m_disposing)
					return;

			int bytes = 0;
			SocketDataWrapper wrap = new SocketDataWrapper();
			try
			{
				bytes = m_sock.EndReceive(res);
				wrap = (SocketDataWrapper)res.AsyncState;
			}
			catch { return; }

			if (bytes == 0)
			{
				wrap.State = SocketDataWrapper.DataReceiveState.NoData;
			}

			try
			{
				switch (wrap.State)
				{
					case SocketDataWrapper.DataReceiveState.ReadLen1:
						{
							wrap.RawLength[0] = wrap.Data[0];
							wrap.State = SocketDataWrapper.DataReceiveState.ReadLen2;
							wrap.Data = new byte[SocketDataWrapper.BUFFER_SIZE];
							m_sock.BeginReceive(wrap.Data, 0, wrap.Data.Length, SocketFlags.None, _recvCB, wrap);
							break;
						}
					case SocketDataWrapper.DataReceiveState.ReadLen2:
						{
							wrap.RawLength[1] = wrap.Data[0];
							wrap.State = SocketDataWrapper.DataReceiveState.ReadData;
							wrap.Data = new byte[Packet.DecodeNumber(wrap.RawLength)];
							m_sock.BeginReceive(wrap.Data, 0, wrap.Data.Length, SocketFlags.None, _recvCB, wrap);
							break;
						}
					case SocketDataWrapper.DataReceiveState.ReadData:
						{
							byte[] data = wrap.Data;
							m_packetProcessor.Decode(ref data);

							//This block handles receipt of file data that is transferred to the client.
							//It should make file transfer nuances pretty transparent to the client.
							//The header for files stored in a Packet type is always as follows: FAMILY_INIT, ACTION_INIT, (InitReply)
							//A 3-byte offset is found throughout the code that handles creating these files.
							Packet pkt = new Packet(data);
							if (pkt.Family == PacketFamily.Init && pkt.Action == PacketAction.Init)
							{
								Handlers.InitReply reply = (Handlers.InitReply)pkt.GetChar();
								if (Enum.GetName(typeof(Handlers.InitReply), reply).Contains("_FILE_"))
								{
									int dataGrabbed = 0;
									
									int pktOffset = 0;
									for (; pktOffset < data.Length; ++pktOffset)
										if (data[pktOffset] == 0)
											break;

									do
									{
										byte[] fileBuffer = new byte[pkt.Length - pktOffset];
										int nextGrabbed = m_sock.Receive(fileBuffer);
										Array.Copy(fileBuffer, 0, data, dataGrabbed + 3, data.Length - (dataGrabbed + pktOffset));
										dataGrabbed += nextGrabbed;
									}
									while (dataGrabbed < pkt.Length - pktOffset);

									if(pktOffset > 3)
										data = data.SubArray(0, pkt.Length - (pktOffset - 3));
									
									data[2] = (byte)reply; //rewrite the InitReply with the correct value (retrieved with GetChar, server sends with GetByte for other reply types)
								}
							}

							ThreadPool.QueueUserWorkItem(_handle, new Packet(data));
							SocketDataWrapper newWrap = new SocketDataWrapper();
							m_sock.BeginReceive(newWrap.Data, 0, newWrap.Data.Length, SocketFlags.None, _recvCB, newWrap);
							break;
						}
					default:
						{
							Console.WriteLine("There was an error in the receive callback. Resetting to default state.");

							SocketDataWrapper newWrap = new SocketDataWrapper();
							m_sock.BeginReceive(newWrap.Data, 0, newWrap.Data.Length, SocketFlags.None, _recvCB, newWrap);
							break;
						}
				}
			}
			catch (SocketException se)
			{
				//in the process of disconnecting
				Console.WriteLine("There was a SocketException with SocketErrorCode {0} in _recvCB", se.SocketErrorCode);
				return;
			}
		}

		private void _sendCB(IAsyncResult res)
		{
			lock (disposingLockObject)
				if (m_disposing)
				{
					m_sendLock.Set();
					return;
				}

			int bytes = m_sock.EndSend(res);
			SocketDataWrapper wrap = (SocketDataWrapper)res.AsyncState;
			if (bytes != wrap.Data.Length)
			{
				m_sendLock.Set();
				throw new InvalidOperationException("There was an error sending the specified number of bytes to the server.");
			}

			m_sendLock.Set(); //send completed asyncronously. Allow pending sends to continue
		}

		//-----------------------------------
		//Receive file from server
		//-----------------------------------

		//private static readonly object receivingFileLock = new object();
		//private bool m_receivingFile = false;
		//protected ManualResetEvent m_doneReceivingFile = new ManualResetEvent(false);

		//public virtual void StartFileDownload()
		//{
		//	lock (receivingFileLock)
		//	{
		//		m_receivingFile = true;
		//		m_doneReceivingFile.Reset();
		//	}
		//}

		//public virtual bool WaitForFileDownload()
		//{
		//	lock (receivingFileLock)
		//	{
		//		//wait completely successfully when not receiving a file
		//		if (!m_receivingFile)
		//			return true;

		//		m_doneReceivingFile.WaitOne();
		//		if (m_receivingFile) //still receiving file means it was cancelled with CancelFileDownload();
		//			return false;
		//	}

		//	return false;
		//}

		//public virtual void CancelFileDownload()
		//{
		//	m_doneReceivingFile.Set();
		//}

		//-----------------------------------
		//dispose method
		//-----------------------------------

		public void Dispose()
		{
			lock (disposingLockObject)
			{
				if (m_disposing)
					return;

				m_disposing = true;
			}

			if (m_sendLock != null)
				m_sendLock.Set();

			if (m_connected)
				m_sock.Shutdown(SocketShutdown.Both);

			m_sock.Close();
			m_sock = null;
		}

		public void UpdateSequence(int newVal)
		{
			m_packetProcessor.SequenceStart = newVal;
		}
	}
}
