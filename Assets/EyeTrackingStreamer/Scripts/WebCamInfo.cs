using Microsoft.MixedReality.WebRTC.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WebCamInfo : MonoBehaviour
{
    public WebcamSource webcamSource;

    public void PrintInfo()
	{
		Debug.LogWarning($"[WebcamSource] VideoProfileId: {webcamSource.VideoProfileId} FormatMode: {webcamSource.FormatMode} Source.Name: {webcamSource.Source.Name} WebcamDevice: {webcamSource.WebcamDevice} VideoProfileKind: {webcamSource.VideoProfileKind} MediaKind: {webcamSource.MediaKind} VideoProfileId: {webcamSource.VideoProfileId}");
	}
}
