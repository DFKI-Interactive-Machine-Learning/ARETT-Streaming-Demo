using System;
using UnityEngine;

public class TimeDisplay : MonoBehaviour
{
    [SerializeField]
    private TextMesh textMesh;

	private void Update()
	{
		textMesh.text = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
	}
}
