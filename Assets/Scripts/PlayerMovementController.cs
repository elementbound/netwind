using UnityEngine;
using Unity.Netcode;

public class PlayerMovementController : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool enableInterpolation = true;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;

    private void OnEnable()
    {
        inputProvider ??= GetComponent<InputProvider>();

        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkUpdate;
    }

    private void OnDisable()
    {
        if (NetworkManager)
            NetworkManager.NetworkTickSystem.Tick -= NetworkUpdate;
    }

    private void NetworkUpdate()
    {
        float deltaTime = NetworkManager.LocalTime.FixedDeltaTime;

        var input = inputProvider.Current;
        transform.position += input.movement * moveSpeed * deltaTime;
    }
}
