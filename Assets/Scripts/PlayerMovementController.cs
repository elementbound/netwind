using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerMovementController : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;

    private void Start()
    {
        inputProvider ??= GetComponent<InputProvider>();
    }

    private void OnEnable()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkUpdate;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton)
            NetworkManager.NetworkTickSystem.Tick -= NetworkUpdate;
    }

    private void NetworkUpdate()
    {
        if (IsServer)
            transform.position += inputProvider.Current.movement * moveSpeed * NetworkManager.LocalTime.FixedDeltaTime;
    }
}
