using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class LocalPlayerTransform : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NetworkTransform networkTransform;
    [SerializeField] private CharacterController characterController;

    [Header("Runtime")]
    [SerializeField] private Vector3 authorativePosition;
    [SerializeField] private bool isSyncEnabled;

    void Start()
    {
        networkTransform = GetComponent<NetworkTransform>();
        characterController = GetComponent<CharacterController>();

        if (IsLocalPlayer && !IsServer)
            networkTransform.enabled = false;
    }

    private void OnEnable()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkUpdate;
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick -= NetworkUpdate;
    }

    void NetworkUpdate()
    {
        if (IsServer)
            CommitTransformClientRpc(transform.position);
    }

    private void Update()
    {
        if (IsLocalPlayer && IsSyncEnabled)
        {
            if (characterController)
                characterController.Move(authorativePosition - (authorativePosition + transform.position) / 2f);
            else
                transform.position = (authorativePosition + transform.position) / 2f;
        }
    }

    public bool IsSyncEnabled { get => isSyncEnabled; set { isSyncEnabled = value; } }

    [ClientRpc]
    private void CommitTransformClientRpc(Vector3 position)
    {
        authorativePosition = position;
    }
}
