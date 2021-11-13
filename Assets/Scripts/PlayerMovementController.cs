using UnityEngine;
using Unity.Netcode;

public class PlayerMovementController : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;

    public ulong NetId => NetworkObjectId;

    public bool IsOwn => IsOwner;

    private void OnEnable()
    {
        inputProvider ??= GetComponent<InputProvider>();

        NetworkHistoryManager.Singleton.OnSimulate += OnSimulate;
    }

    private void OnDisable()
    {
        NetworkHistoryManager.Singleton.OnSimulate -= OnSimulate;
    }

    private void OnSimulate(int tick, float deltaTime)
    {
        transform.position += inputProvider.Current.movement * moveSpeed * deltaTime;
    }
}
