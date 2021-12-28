namespace com.github.elementbound.NetWind
{
    public interface IRewindableObject : INetworkObject
    {
        IRewindableInput[] GetInputs();
        IRewindableState[] GetStates();

        void MarkForDestroy();
        void MarkForDestroy(int tick);
        int? GetDestroyMark();
    }
}