using UnityEngine;
using Unity.Netcode;
using System;

[DefaultExecutionOrder(200000)]
public class LocalAwareNetworkTransform : NetworkBehaviour
{
    [Serializable]
    private struct TransformState
    {
        public Vector3 position;
        public Vector3 rotationEuler;
        public Vector3 scale;
    }

    [Header("Runtime")]
    [SerializeField] private TransformState fromTransform;
    [SerializeField] private TransformState toTransform;
    [SerializeField] private double fromTimestamp;

    private void OnEnable()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkUpdate;

        fromTransform = new TransformState()
        {
            position = transform.localPosition,
            rotationEuler = transform.localRotation.eulerAngles,
            scale = transform.localScale
        };

        toTransform = new TransformState()
        {
            position = transform.localPosition,
            rotationEuler = transform.localRotation.eulerAngles,
            scale = transform.localScale
        };
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick -= NetworkUpdate;
    }

    void NetworkUpdate()
    {
        if (IsServer)
        {
            fromTransform = toTransform;

            toTransform.position = transform.localPosition;
            toTransform.rotationEuler = transform.localRotation.eulerAngles;
            toTransform.scale = transform.localScale;

            fromTimestamp = NetworkManager.LocalTime.Time;

            CommitPositionClientRpc(toTransform);
        }
    }

    private void Update()
    {
        var f = (float)(NetworkManager.LocalTime.Time - fromTimestamp) / NetworkManager.LocalTime.FixedDeltaTime;
        f = Mathf.Clamp01(f);

        transform.localPosition = Vector3.Lerp(fromTransform.position, toTransform.position, f);
        transform.localRotation = Quaternion.Lerp(Quaternion.Euler(fromTransform.rotationEuler), Quaternion.Euler(toTransform.rotationEuler), f);
        transform.localScale = Vector3.Lerp(fromTransform.scale, toTransform.scale, f);
    }

    [ClientRpc]
    private void CommitPositionClientRpc(TransformState transform)
    {
        if (IsServer)
            return;

        fromTransform = toTransform;
        toTransform = transform;
        fromTimestamp = NetworkManager.LocalTime.Time;

        Debug.Log("Received transform from server");
    }
}
