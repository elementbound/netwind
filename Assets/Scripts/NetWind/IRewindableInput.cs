namespace com.github.elementbound.NetWind
{
    public interface IRewindableInput : INetworkObject
    {
        void SaveInput(int tick);
        void RestoreInput(int tick);
        void CommitInput(int tick);
        int EarliestReceivedInput { get; }
    }
}