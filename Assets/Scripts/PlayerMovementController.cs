using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerMovementController : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private NetworkTransform networkTransform;
    [SerializeField] private LocalPlayerTransform localPlayerTransform;

    private void Start()
    {
        inputProvider ??= GetComponent<InputProvider>();
        characterController = GetComponent<CharacterController>();

        networkTransform = GetComponent<NetworkTransform>();
        localPlayerTransform = GetComponent<LocalPlayerTransform>();
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
        if (IsServer || IsLocalPlayer)
            characterController.Move(inputProvider.Current.movement * moveSpeed * NetworkManager.LocalTime.FixedDeltaTime);

        if (IsLocalPlayer && !IsServer)
        {
            var sync = inputProvider.Current.movement.magnitude < 0.1f;
            localPlayerTransform.IsSyncEnabled = sync;
        }
    }
}
