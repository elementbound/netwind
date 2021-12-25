using UnityEngine;
using Unity.Netcode;
using com.github.elementbound.NetWind;

[RequireComponent(typeof(RewindableTransformState))]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovementController : EmptyStateBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private MinionController minionPrefab;
    [SerializeField] private double spawnCooldown = 0.5f;

    [Header("Dependencies")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private InputProvider inputProvider;
    [SerializeField] private OnceFlag spawnFlag;
    [SerializeField] private double lastSpawn;

    private void OnEnable()
    {
        inputProvider ??= GetComponent<InputProvider>();
        characterController = GetComponent<CharacterController>();
    }

    public override void Simulate(int tick, float deltaTime)
    {
        var input = inputProvider.Current;
        characterController.Move(input.movement * moveSpeed * deltaTime);

        if (IsServer)
        {
            if (spawnFlag.CanProcess(tick) && NetworkRewindManager.Instance.Time - lastSpawn > spawnCooldown && inputProvider.Current.isSpawning)
            {
                spawnFlag.AcknowledgeTick(tick);
                lastSpawn = NetworkRewindManager.Instance.Time;

                var minion = Instantiate(minionPrefab);
                minion.GetComponent<NetworkObject>().SpawnAsPlayerObject(OwnerClientId);
                minion.Attach(transform);
            }
        }
    }
}
