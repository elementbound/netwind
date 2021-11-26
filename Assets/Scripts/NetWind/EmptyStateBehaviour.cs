using Unity.Netcode;

namespace com.github.elementbound.NetWind
{
    public abstract class EmptyStateBehaviour : NetworkBehaviour, IRewindableState
    {
        public int LatestReceivedState => IsOwn ? 0 : NetworkManager.LocalTime.Tick;

        public bool HasNewState => false;

        public IRewindableInput ControlledBy { get; private set; }

        public ulong NetId => NetworkObjectId;

        public bool IsOwn => IsOwner || (IsOwnedByServer && IsServer);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            ControlledBy ??= GetComponent<IRewindableInput>();
            NetworkRewindManager.Instance.RegisterState(this);
        }

        public void AcknowledgeStates()
        {
        }

        public void CommitState(int tick)
        {
        }

        public void RestoreState(int tick)
        {
        }

        public void SaveState(int tick)
        {
        }

        public abstract void Simulate(int tick, float deltaTime);
    }
}
