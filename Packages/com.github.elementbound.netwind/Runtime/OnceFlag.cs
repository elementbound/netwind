using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class OnceFlag
{
    [SerializeField] private HashSet<long> processedTicks = new HashSet<long>();

    public bool CanProcess(long tick)
    {
        return !processedTicks.Contains(tick);
    }

    public void AcknowledgeTick(long tick)
    {
        processedTicks.Add(tick);
    }
}
