using UnityEngine;

public class RockData
{
    public Vector3 position;
    public int rockType;
    public float rotation;

    public RockData(Vector3 pos, int type, float rot)
    {
        position = pos;
        rockType = type;
        rotation = rot;
    }
}
