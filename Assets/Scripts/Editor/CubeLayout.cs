using System.Collections.Generic;
using UnityEngine;

public class CubeLayout
{
    public readonly List<Vector2Int> Solids = new List<Vector2Int>();
    public readonly List<Vector2Int> Spikes = new List<Vector2Int>();
    public readonly List<Vector2> Pads = new List<Vector2>();
    public readonly List<Vector2> Orbs = new List<Vector2>();

    public float StartX { get; private set; } = float.PositiveInfinity;
    public float EndX { get; private set; } = float.NegativeInfinity;

    public int ObstacleCount;

    public void Extend(float fromX, float toX)
    {
        StartX = Mathf.Min(StartX, fromX);
        EndX = Mathf.Max(EndX, toX);
    }
}
