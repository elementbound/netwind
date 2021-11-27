namespace com.github.elementbound.NetWind
{
    public interface IRewindableState : INetworkObject
    {
        void SaveState(int tick);
        void RestoreState(int tick);
        void InterpolateState(int tickFrom, int tickTo, float f);
        void CommitState(int tick);
        void Simulate(int tick, float deltaTime);
        void AcknowledgeStates();
        int LatestReceivedState { get; }
        bool HasNewState { get; }
        IRewindableInput ControlledBy { get; }
        bool IsInterpolated { get; }
    }
}
