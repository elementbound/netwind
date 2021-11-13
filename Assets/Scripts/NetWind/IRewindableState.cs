namespace com.github.elementbound.NetWind
{
    public interface IRewindableState : INetworkObject
    {
        void SaveState(int tick);
        void RestoreState(int tick);
        void CommitState(int tick);
    }
}