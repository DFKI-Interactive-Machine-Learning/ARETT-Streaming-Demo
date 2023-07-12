using System.Collections.Generic;
using UnityEngine;

public class GazeTag
{
    /// <summary>
    /// Unix Timestamp in ms from which the data stems (accuracy 1ms)
    /// </summary>
    public long timestamp;

    /// <summary>
    /// Position of the gaze point in the world
    /// </summary>
    public float[] gaze_world;
    
    /// <summary>
    /// Position of the gaze point in the world, converted to Unity format if it exists and has the right length
    /// </summary>
    public Vector3 GazeWorldVector { get
		{
            if (gaze_world.Length == 3)
                return new Vector3(gaze_world[0], gaze_world[1], gaze_world[2]);
            else
                return Vector3.zero;
		}
    }

    /// <summary>
    /// Label of the gaze point
    /// </summary>
    public string label;

    /// <summary>
    /// Accuracy(?) of the tag
    /// </summary>
    public float prob;

    /// <summary>
    /// Duration of the gaze event
    /// </summary>
    public float duration;

    /// <summary>
    /// Type of the event
    /// </summary>
    public string event_type;
}
