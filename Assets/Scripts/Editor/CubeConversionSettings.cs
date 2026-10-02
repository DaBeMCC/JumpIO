using UnityEngine;

[CreateAssetMenu(fileName = "CubeConversionSettings", menuName = "JumpIO/Cube Conversion Settings")]
public class CubeConversionSettings : ScriptableObject
{
    public int seed = 1;

    [Min(0.25f)] public float minGapSeconds = 0.6f;

    [Range(0f, 3f)] public float density = 1f;
}
