using UnityEngine;
using System.Collections;

public class PlayerMovementController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;
    [SerializeField] private CharacterController characterController;

    private void Start()
    {
        inputProvider ??= GetComponent<InputProvider>();
        characterController ??= GetComponent<CharacterController>();
    }

    private void FixedUpdate()
    {
        characterController.Move(inputProvider.Current.movement * moveSpeed * Time.fixedDeltaTime);
    }
}
