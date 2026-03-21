using UnityEngine;

public class CycleDataManager : MonoBehaviour
{
    public static CycleDataManager Instance { get; private set; }

    public int playerCount = 6;
    public float wheelCircumference = 2.1f; // meters per rotation (standard 700c wheel)
    public float speedSmoothTime = 0.5f;

    public PlayerCycleData[] Players { get; private set; }

    private void Awake()
    {
        Instance = this;
        Players = new PlayerCycleData[playerCount];
        for (int i = 0; i < playerCount; i++)
            Players[i] = new PlayerCycleData();
    }

    public void ProcessGameData(int[] rotations)
    {
        float time = Time.time;
        float dt = Time.deltaTime;

        for (int i = 0; i < Mathf.Min(rotations.Length, playerCount); i++)
        {
            var p = Players[i];
            p.TotalRotations += rotations[i];
            p.TotalDistance = p.TotalRotations * wheelCircumference;

            float instantSpeed = (rotations[i] * wheelCircumference) / dt;
            p.Speed = Mathf.Lerp(p.Speed, instantSpeed, Time.deltaTime / speedSmoothTime);

            if (p.Speed > p.TopSpeed)
                p.TopSpeed = p.Speed;

            p.AverageSpeed = (time > 0f) ? p.TotalDistance / time : 0f;
        }
    }

    public void ResetAll()
    {
        for (int i = 0; i < playerCount; i++)
            Players[i] = new PlayerCycleData();
    }
}

[System.Serializable]
public class PlayerCycleData
{
    public int TotalRotations;
    public float TotalDistance;   // meters
    public float Speed;          // m/s (smoothed)
    public float TopSpeed;       // m/s
    public float AverageSpeed;   // m/s
}
