using UnityEngine;

namespace com.github.elementbound.NetWind
{
    public interface IRewindableInput : INetworkObject
    {
        void SaveInput(int tick);
        void RestoreInput(int tick);
        void CommitInput(int tick);
        void AcknowledgeInputs();
        int EarliestReceivedInput { get; }
        bool HasNewInput { get; }
        int LatestKnownInput { get; }
    }
}