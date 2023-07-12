using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

public class MySignaler : Signaler
{
	void Awake()
	{
		InitServer();
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		StartServer();
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		StopServer();
	}

	#region Signaler Messages

	/// <summary>
	/// The last message which was tried to be sent
	/// </summary>
	private SignalSdpMessage lastSdpMessage;

	/// <summary>
	/// The ICE messages which were tried to be sent
	/// </summary>
	private List<IceCandidate> previousIceCandidates = new List<IceCandidate>();

	/// <summary>
	/// Send an sdp message
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	public override Task SendMessageAsync(SdpMessage message)
	{
		Task sendTask = new Task(() => SendMessageSync(message));
		sendTask.Start();
		return sendTask;
	}

	private void SendMessageSync(SdpMessage message)
	{
		// Keep the last message
		lastSdpMessage = new SignalSdpMessage(message);

		if (connectedTcpClient == null)
		{
			Debug.Log($"[MySignaler] No client connected, only storing sdp message for later!\nMessage: {JsonUtility.ToJson(lastSdpMessage)}");
		}
		else
		{
			Debug.Log($"[MySignaler] Sending sdp message to client\nMessage: {JsonUtility.ToJson(lastSdpMessage)}");
			SendTCPMessage(JsonUtility.ToJson(lastSdpMessage));
		}
	}

	/// <summary>
	/// Send an IceCandidate, currently unused
	/// </summary>
	/// <param name="candidate"></param>
	/// <returns></returns>
	public override Task SendMessageAsync(IceCandidate candidate)
	{
		Task sendTask = new Task(() => SendMessageSync(candidate));
		sendTask.Start();
		return sendTask;
	}

	private void SendMessageSync(IceCandidate candidate)
	{
		
		// Add this new candidate to our list
		_mainThreadWorkQueue.Enqueue(() =>
		{
			previousIceCandidates.Add(candidate);
			//Debug.Log($"[MySignaler] Added queued ICE candidate\nMessage: {JsonUtility.ToJson(candidate)}");
		});

		Debug.Log($"[MySignaler] Queued new ICE candidate to be sent with sdp message!\nMessage: {JsonUtility.ToJson(candidate)}");
	}
	#endregion

	#region Signal Message Type
	/// <summary>
	/// Sdp messages exchanged with a aiortc client, serialized as JSON
	/// </summary>
	/// <remarks>
	/// The names of the fields is critical here for proper JSON serialization.
	/// </remarks>
	[Serializable]
	private class SignalSdpMessage
	{
		/// <summary>
		/// Type of the message
		/// </summary>
		public string type;

		/// <summary>
		/// SDP Message
		/// </summary>
		public string sdp;

		/// <summary>
		/// Get the message type from the sdp message
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static string MessageTypeFromSdpMessageType(SdpMessageType type)
		{
			switch (type)
			{
				case SdpMessageType.Offer: return "offer";
				case SdpMessageType.Answer: return "answer";
				default: return "none";
			}
		}

		public SignalSdpMessage AppendIceCandidates(IEnumerable<IceCandidate> iceCandidates)
		{
			// Create new message
			SignalSdpMessage appendedMessage = new SignalSdpMessage();

			// Carry over the type
			appendedMessage.type = this.type;

			// How many streams with ice candidates do we have?
			Dictionary<int, List<string>> foundCandidates = new Dictionary<int, List<string>>();
			foreach (var iceCandidate in iceCandidates)
			{
				if (!foundCandidates.ContainsKey(iceCandidate.SdpMlineIndex))
				{
					foundCandidates.Add(iceCandidate.SdpMlineIndex, new List<string>());
				}
				
				foundCandidates[iceCandidate.SdpMlineIndex].Add(iceCandidate.Content);
			}

			// How many streams do we have in the sdp Message?
			int nSdpStreams = Regex.Matches(this.sdp, "m=").Count;
			Debug.Log($"[MySignaler] {foundCandidates.Keys.Count} ice candidates found, {nSdpStreams} sdp streams found");

			if (foundCandidates.Keys.Count != nSdpStreams)
				throw new Exception($"Not the same amount of candidates found! {foundCandidates.Keys.Count} ice candidates found, {nSdpStreams} sdp streams found");

			// Create sdp message with ice candidates appended
			int curStream = 0;
			bool curStreamTypeIsVideo = false;
			StringBuilder sdpBuilder = new StringBuilder();
			using (StringReader strReader = new StringReader(sdp))
			{
				// Read all lines
				while (true)
				{
					// Read the next line
					string curLine = strReader.ReadLine();

					// If no line is left leave the loop
					if (curLine == null) break;

					/*
					// Start Workaround for wrong video message
					if (curStreamTypeIsVideo && curLine == "a=inactive")
					{
						curLine = "a=sendonly";
						Debug.LogWarning("[MySignaler] Fixed video stream type!");
					}
					// End Workaround for wrong video message
					*/

					// Append the current line to the output
					sdpBuilder.AppendLine(curLine);

					// If the line starts with m we have the beginning of a new stream and want to add the ice corresponding information to it
					if (curLine.StartsWith("m="))
					{
						// Start Workaround for wrong video message
						if (curLine.StartsWith("m=video"))
						{
							curStreamTypeIsVideo = true;
						}
						else
						{
							curStreamTypeIsVideo = false;
						}
						// End Workaround
						foreach (string iceCandidate in foundCandidates[curStream])
						{
							sdpBuilder.Append("a=");
							sdpBuilder.AppendLine(iceCandidate);
						}
						sdpBuilder.AppendLine("a=end-of-candidates");
						curStream++;
					}
				}
			}
			appendedMessage.sdp = sdpBuilder.ToString();

			return appendedMessage;
		}

		public SignalSdpMessage(SdpMessage message)
		{
			type = MessageTypeFromSdpMessageType(message.Type);
			sdp = message.Content;
		}

		public SignalSdpMessage() { }
	}
	#endregion

	#region TCP Server

	/// <summary>
	/// IP of the device on which we want to bind the server
	/// </summary>
	//public string serverIP = "169.254.243.89";  // "127.0.0.1";
    /// <summary>
    /// Port to which we want to bind
    /// </summary>
    public int serverPort = 11000;

	/// <summary>
	/// TCPListener to listen for incomming TCP connection
	/// requests.
	/// </summary>
	private TcpListener tcpListener;
	/// <summary>
	/// Task for TcpServer workload.
	/// </summary>
	private Task serverTask;
	/// <summary>
	/// Create handle to connected tcp client.
	/// </summary>
	private TcpClient connectedTcpClient;
	/// <summary>
	/// Are we listening for TCP connections?
	/// </summary>
	private bool listening = true;

	private void InitServer()
	{
		serverTask = new Task(() => StartListening());
	}

	private void StartServer()
	{
		listening = true;
		serverTask.Start();
		Debug.Log("[MySignaler] Started");
	}

	private void StopServer()
	{
		// We don't want to listen to new connections anymore
		listening = false;

		// Send a bye message if we have a client and then disconnect
		if (connectedTcpClient != null)
		{
			SendTCPMessage("{\"type\": \"bye\"}");
			connectedTcpClient.Close();
		}

		// Stop the listener
		tcpListener.Stop();

		// Join the server task
		serverTask.Wait();

		// Dispose the server task
		serverTask.Dispose();

		// Done
		Debug.Log("[MySignaler] Stopped");
	}

	/// <summary>
	/// Runs in background TcpServerThread;Handles incomming TcpClient requests
	/// </summary>
	private void StartListening()
	{
		try
		{
			// Create listener on localhost port 8052.
			//tcpListener = new TcpListener(IPAddress.Parse(serverIP), serverPort);
			tcpListener = new TcpListener(IPAddress.Any, serverPort);
			tcpListener.Start();
			Debug.Log("[MySignaler] TCP server is waiting for clients");

			// Reading Buffer
			byte[] bytes = new byte[10];

			// Read data while listening
			while (listening)
			{
				using (connectedTcpClient = tcpListener.AcceptTcpClient())
				{
					Debug.Log("[MySignaler] TCP client connected!");

					// Send the last sdp message together with the ice content
					try
					{
						if (lastSdpMessage != null)
						{
							Debug.Log($"[MySignaler] Send last sdp message");
							SendTCPMessage(JsonUtility.ToJson(lastSdpMessage.AppendIceCandidates(previousIceCandidates)));
						}
						else
						{
							Debug.Log($"[MySignaler] No last sdp message saved!");
						}
					}
					catch (Exception e)
					{
						Debug.Log($"[MySignaler] Exception on sending last sdp message!\nException: {e}");
					}

					// Get a stream object for reading
					using (NetworkStream stream = connectedTcpClient.GetStream())
					{
						//Debug.Log("[MySignaler] Starting listening to response");
						int length;
						StringBuilder currentMessage = new StringBuilder();
						// Read incoming stream into byte array.
						while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
						{
							var incommingData = new byte[length];
							Array.Copy(bytes, 0, incommingData, 0, length);
							// Convert byte array to string message.
							currentMessage.Append(Encoding.UTF8.GetString(incommingData));
							//Debug.Log("[MySignaler] current message received: " + currentMessage);
							string currentMessageStr = currentMessage.ToString();
							if (currentMessageStr.EndsWith("\n"))
							{
								// Process the full message in the main unity thread
								_mainThreadWorkQueue.Enqueue(() => GotTCPMessage(currentMessageStr));
								// Reset the string buffer
								currentMessage.Clear();
							}
						}
					}
				}
			}
		}
		catch (SocketException socketException)
		{
			Debug.LogError("[MySignaler] SocketException while listening for TCP: " + socketException.ToString());
		}
	}

	private void GotTCPMessage(string message)
	{
		// Log that we got a message
		//Debug.Log("[MySignaler] Full message received\nMessage: " + message);

		// Try to decode it
		try
		{
			SignalSdpMessage messageObj = JsonUtility.FromJson<SignalSdpMessage>(message);

			switch (messageObj.type)
			{
				case "offer":
					Debug.Log("[MySignaler] got offer\nMessage: " + message);
					// Apply the offer coming from the remote peer to the local peer
					var sdpOffer = new SdpMessage { Type = SdpMessageType.Offer, Content = messageObj.sdp };
					PeerConnection.HandleConnectionMessageAsync(sdpOffer).ContinueWith(_ =>
					{
						// If the remote description was successfully applied then immediately send
						// back an answer to the remote peer to acccept the offer.
						_nativePeer.CreateAnswer();
					}, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);
					break;

				case "answer":
					Debug.Log("[MySignaler] got answer\nMessage: " + message);
					// No need to wait for completion; there is nothing interesting to do after it.
					var sdpAnswer = new SdpMessage { Type = SdpMessageType.Answer, Content = messageObj.sdp };
					_ = PeerConnection.HandleConnectionMessageAsync(sdpAnswer);
					break;

				default:
					// TODO, but not needed when communicating with aiortc
					break;
			}
		}
		catch (Exception e)
		{
			Debug.LogError("[MySignaler] Exception while converting json from incoming message: " + e);
		}
	}

	/// <summary>
	/// Send message to client using socket connection.
	/// </summary>
	private void SendTCPMessage(string message)
	{
		if (connectedTcpClient == null)
		{
			Debug.LogError("[MySignaler] No client to which we can send the message!\nMessage: " + message);
			return;
		}
		
		if (message == "")
		{
			Debug.LogError("[MySignaler] Can't send empty message!");
			return;
		}

		try
		{
			// Get a stream object for writing.
			NetworkStream stream = connectedTcpClient.GetStream();
			if (stream.CanWrite)
			{
				// Convert string message to byte array.
				byte[] serverMessageAsByteArray = Encoding.UTF8.GetBytes(message + "\n");
				// Write byte array to socketConnection stream.
				stream.Write(serverMessageAsByteArray, 0, serverMessageAsByteArray.Length);
				Debug.Log("[MySignaler] Message sent\nMessage:" + message);
			}
		}
		catch (SocketException socketException)
		{
			Debug.LogError("[MySignaler] Socket exception while sending: " + socketException);
		}
		catch (ObjectDisposedException)
		{
			Debug.LogWarning("[MySignaler] Can't send message because client connection was disposed\nMessage: " + message);
		}
	}
	#endregion

	#region String Helper
	private static string ToLiteral(string input)
	{
		using (var writer = new StringWriter())
		{
			using (var provider = CodeDomProvider.CreateProvider("CSharp"))
			{
				provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
				return writer.ToString();
			}
		}
	}
	#endregion
}
