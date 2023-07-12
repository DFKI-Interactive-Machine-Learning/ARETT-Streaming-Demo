using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class Autoconnect : MonoBehaviour
{
    public Microsoft.MixedReality.WebRTC.Unity.PeerConnection peerConnection;

    public void StartConnection()
    {
        Invoke(nameof(StartConectionNow), 2f);

        //Task.Run(() => PrintFormats());
    }

    private void StartConectionNow() {
        var success = peerConnection.StartConnection();

        if (!success)
		{
            Debug.LogError("[Autoconnect] Error on starting connection!");
		}
    }

    private async void PrintFormats()
    {
        var devices = await DeviceVideoTrackSource.GetCaptureDevicesAsync();

        foreach (var device in devices)
        {
            var formats = await DeviceVideoTrackSource.GetCaptureFormatsAsync(device.id);

            foreach (var format in formats)
            {
                Debug.LogWarning($"[Format] Device: {device.name} id: {device.id} Format: width {format.width} height {format.height} framerate {format.framerate}");
            }

            var profiles = await DeviceVideoTrackSource.GetCaptureProfilesAsync(device.id);

			foreach (var profile in profiles)
			{
                Debug.LogWarning($"[Profile] Device: {device.name} id: {device.id} Profile: uniqueId {profile.uniqueId}");
            }
        }
    }
}
