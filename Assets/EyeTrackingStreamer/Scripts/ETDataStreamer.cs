using ARETT;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.WebRTC;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ETDataStreamer : MonoBehaviour
{
    [Header("ET Data Provider")]
    public DataProvider dataProvider;

    [Header("ET Data Channel")]
    public ETDataChannel etDataChannel;

    [Header("Raw Tool Tip")]
    public ToolTip toolTip;
    public Transform taggedGazePosition;

    [Header("Attention Tool Tips")]
    public GameObject attentionToolTipsParent;
    public ToolTip[] attentionToolTips;
    private GameObject[] attentionToolTipsGameObjects;
    private int currentAttentionToolTip = 0;

    [Header("Buttons")]
    public Interactable RawButton;
    public Interactable AttentionButton;

    private LinkedList<GazeData> gazeDataCache = new LinkedList<GazeData>();
    private int maxCacheSize = 30;

    /// <summary>
    /// On Start subscribe to new ET data
    /// </summary>
    void Start()
    {
        dataProvider.NewDataEvent += OnNewETData;
        StartCoroutine(addMessageReceived());

        // Cache the game objects of all attention tool tips
        // Note: This might not be necessary but might also speed things up if unity caching isn't good
        attentionToolTipsGameObjects = new GameObject[attentionToolTips.Length];
		for (int i = 0; i < attentionToolTips.Length; i++)
		{
            attentionToolTipsGameObjects[i] = attentionToolTips[i].gameObject;
		}
    }

    /// <summary>
    /// Function which is called when the raw button is pushed
    /// </summary>
    public void OnRawButtonPush()
	{
        toolTip.gameObject.SetActive(RawButton.IsToggled);
	}

    /// <summary>
    /// Function which is called when the attention button is pushed
    /// </summary>
    public void OnAttentionButtonPush()
    {
        attentionToolTipsParent.SetActive(AttentionButton.IsToggled);
    }

    /// <summary>
    /// Subscribe to new messages when the channel is connected
    /// </summary>
    /// <returns></returns>
    private IEnumerator addMessageReceived()
	{
        // Wait until a data channel exists
        yield return new WaitUntil(() => etDataChannel.dataChannel != null);

        // Wait until the channel is connected
        yield return new WaitUntil(() => etDataChannel.dataChannel.State == DataChannel.ChannelState.Open);

        // Add new message hook
        etDataChannel.dataChannel.MessageReceived += OnNewDataMessage;

        Debug.Log("[ETDataStreamer] Subscribed to new messages on data channel");
    }

	/// <summary>
	/// When we get new ET data cache it and if the data channel is connected send the data as JSON
	/// </summary>
	/// <param name="gazeData"></param>
	private void OnNewETData(GazeData gazeData)
	{
        // Cache the new gaze data
        mainThreadActions.Enqueue(() => CacheGazeData(gazeData));
        //Debug.Log($"[ETDataStreamer] Got new gaze data {gazeData}");
        // Send the data
        if (etDataChannel.dataChannel != null && etDataChannel.dataChannel.State == Microsoft.MixedReality.WebRTC.DataChannel.ChannelState.Open)
		{
            etDataChannel.dataChannel.SendMessage(Encoding.UTF8.GetBytes(JsonUtility.ToJson(gazeData)));
            //Debug.Log($"[ETDataStreamer] Sent gaze data {gazeData}");
        }
	}

    /// <summary>
    /// When we get a new message and it is a gaze tag show it
    /// </summary>
    /// <param name="obj"></param>
    private void OnNewDataMessage(byte[] obj)
    {
        // Get string from message
        string message = Encoding.UTF8.GetString(obj);

        // If the message starts with a { we assume its json and contains the data we want
        if (message.StartsWith("{"))
        {
            // Enqueue the message processing
            mainThreadActions.Enqueue(() => processMessage(message));
        }
    }

    /// <summary>
    /// Process incoming message
    /// </summary>
    /// <param name="message"></param>
    private void processMessage(string message)
	{
        try
        {
            // Get tag info
            GazeTag tag = JsonUtility.FromJson<GazeTag>(message);

            // Process raw ("real time") data
            if (tag.event_type == "raw")
			{
                if (tag.label == null || tag.label == "")
                {
                    tag.label = "No Label";
                }

				// Set the tool tip
				toolTip.ToolTipText = $"{tag.label} ({tag.prob * 100:##.##}%)";
                toolTip.transform.position = tag.GazeWorldVector;

                //Debug.Log($"[ETDataStreamer] Got gaze tag of type raw: {tag.event_type} position: {tag.GazeWorldVector}");
            }

            else if (tag.event_type == "attention")
			{
                if (tag.label == null || tag.label == "")
                {
                    tag.label = "No Label";
                }

                // Set the tool tip
                attentionToolTips[currentAttentionToolTip].ToolTipText = $"{tag.label} ({tag.duration / 1000:.##} s)";
                attentionToolTips[currentAttentionToolTip].transform.position = tag.GazeWorldVector;

                // Make sure the tool tip is visible
                attentionToolTipsGameObjects[currentAttentionToolTip].SetActive(true);

                // Update the current tool tip number
                currentAttentionToolTip++;
                if (currentAttentionToolTip >= attentionToolTips.Length) currentAttentionToolTip = 0;

                //Debug.LogWarning($"[ETDataStreamer] Got gaze tag of type raw: {tag.event_type} position: {tag.GazeWorldVector}");
                //Debug.LogError($"[DataChannel message] timestamp: {tag.timestamp} label: {tag.label} gaze_world count: {tag.gaze_world.Length} gaze_world Vector: {tag.GazeWorldVector} prob: {tag.prob} duration: {tag.duration} event_type: {tag.event_type}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ETDataStreamer] Exception while parsing tag message. Tag: {message}\nException:{e}");
        }
    }

    /// <summary>
    /// Add the gaze data to the cache
    /// </summary>
    /// <param name="newGazeData"></param>
    private void CacheGazeData(GazeData newGazeData)
	{
        // Add the new data at the end
        gazeDataCache.AddLast(newGazeData);

        // If we exceed the cache size remove the first element
        if (gazeDataCache.Count > maxCacheSize)
		{
            gazeDataCache.RemoveFirst();
		}
	}

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
