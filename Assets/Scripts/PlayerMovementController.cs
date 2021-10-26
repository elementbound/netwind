using UnityEngine;
using Unity.Netcode;

public class PlayerMovementController : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private NetworkVariable<Vector3> position;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;
    [SerializeField] private CharacterController characterController;

    private void Start()
    {
        inputProvider ??= GetComponent<InputProvider>();
        characterController = GetComponent<CharacterController>();
    }

    private void FixedUpdate()
    {
        if (IsServer)
        {
            characterController.Move(inputProvider.Current.movement * moveSpeed * Time.fixedDeltaTime);
            position.Value = transform.position;
        } else
        {
            transform.position = position.Value;
        }
    }
}
