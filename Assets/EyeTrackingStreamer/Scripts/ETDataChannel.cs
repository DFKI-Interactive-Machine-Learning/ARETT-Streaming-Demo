using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class ETDataChannel : MonoBehaviour
{
    /// <summary>
    /// The peer connection to which we want to add the data channel
    /// </summary>
    public Microsoft.MixedReality.WebRTC.Unity.PeerConnection peerConnection;

    /// <summary>
    /// The data channel of this object
    /// </summary>
    public DataChannel dataChannel;

    /// <summary>
    /// Event which is fired after the data channel has been added
    /// </summary>
    [Tooltip("Event which is fired after the data channel has been added")]
    public UnityEvent OnInitialized = new UnityEvent();

    /// <summary>
    /// Add the data channel
    /// </summary>
    public void AddDataChannel()
    {
        Task.Run(() => addDataChannelAsync());
    }

    private async void addDataChannelAsync()
    {
        Debug.Log("[ETDataChannel] PeerConnection initialized, adding data channel");

        // Add Data Channel
        dataChannel = await peerConnection.Peer.AddDataChannelAsync("chat", true, true);

        dataChannel.MessageReceived += dataMessageReceived;
        dataChannel.StateChanged += dataChannelStateChanged;

        Debug.Log("[ETDataChannel] Data channel added");

        // Execute the action in the main thread which we want to execute after creating the data channel
        mainThreadActions.Enqueue(() => OnInitialized.Invoke());
    }

    private void dataMessageReceived(byte[] obj)
    {
        string message = Encoding.UTF8.GetString(obj);

        if (message.StartsWith("pong")) {
            try
			{
                float timeMessage = float.Parse(message.Split(' ')[1], new CultureInfo("en-US"));
                float curTime = (float)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
                float rtt = (curTime - timeMessage) * 1000;
                Debug.Log($"[ETDataChannel] Got pong on data channel: {message} (RTT {rtt.ToString("f6", new CultureInfo("en-US"))} ms)");
            }
            catch (Exception e)
			{
                Debug.Log($"[ETDataChannel] Exception while parsing pong message. Message: {message}\nException:{e}");
			}
        }
        else if (!message.StartsWith("{"))
		{
            Debug.Log($"[ETDataChannel] Got data on data channel: {message}");
        }
    }

    #region Debug functions
    [Header("Send ping/pong over Data Channel")]
    /// <summary>
    /// Flag if we want to send ping/pong messages over the data channel
    /// Note: The handling of ping/pong messages always exist but are only triggered if the coroutine sends the first ping
    /// </summary>
    public bool sendPingMessage = false;

    private Coroutine sendPingCoroutine;
    private bool sendingPing = false;

    private void dataChannelStateChanged()
    {
        Debug.Log($"[ETDataChannel] State change for data channel: {dataChannel.State}");

        if (sendPingMessage)
		{
            if (dataChannel.State == DataChannel.ChannelState.Open)
            {
                sendingPing = true;
                mainThreadActions.Enqueue(() => sendPingCoroutine = StartCoroutine(sendPing()));
            }
            else if (dataChannel.State == DataChannel.ChannelState.Closing)
            {
                sendingPing = false;
                mainThreadActions.Enqueue(() => StopCoroutine(sendPingCoroutine));
            }
        }
    }

    private IEnumerator sendPing()
    {
        while (sendingPing)
        {
            // Send message
            string message = "ping " + ((float)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency).ToString("f6", new CultureInfo("en-US"));
            dataChannel.SendMessage(Encoding.UTF8.GetBytes(message));

            // Wait 1s
            yield return new WaitForSeconds(1f);
        }
    }
    #endregion

    #region Unity Thread sync
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action();
        }
    }
	#endregion
}
