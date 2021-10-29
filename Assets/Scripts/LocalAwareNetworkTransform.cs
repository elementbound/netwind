using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using System.Linq;

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

    [Header("Configuration")]
    public bool isLocalAuthorative;
    public float correctionSpeed = 1f;
    public float cacheTime = 1f;
    public bool interpolate = false;

    [Header("Runtime")]
    [SerializeField] private TransformState fromTransform;
    [SerializeField] private TransformState toTransform;
    [SerializeField] private TransformState deltaTransform;
    [SerializeField] private double fromTimestamp;
    [SerializeField] private Vector3 appliedTranslation;
    private Dictionary<double, TransformState> transformCache = new Dictionary<double, TransformState>();

    [Header("Gizmos")]
    [SerializeField] private double latency;
    [SerializeField] private int tickLatency;
    [SerializeField] private double serverTime;
    [SerializeField] private double localTime;
    [SerializeField] private double lastTimestamp;
    [SerializeField] private Vector3 authorativePosition;
    [SerializeField] private Vector3 closestCachedPosition;
    [SerializeField] private Vector3 correctiveDelta;

    private void OnEnable()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkUpdate;
        isLocalAuthorative = IsOwner && !IsServer;

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

        transformCache[NetworkManager.LocalTime.Time] = toTransform;
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick -= NetworkUpdate;
        transformCache.Clear();
    }

    void NetworkUpdate()
    {
        serverTime = NetworkManager.ServerTime.Time;
        localTime = NetworkManager.LocalTime.Time;

        if (IsServer || isLocalAuthorative)
        {
            fromTransform = toTransform;

            toTransform.position = transform.localPosition;
            toTransform.rotationEuler = transform.localRotation.eulerAngles;
            toTransform.scale = transform.localScale;

            fromTimestamp = NetworkManager.ServerTime.Time;

            if (IsServer)
                CommitPositionClientRpc(toTransform, NetworkManager.LocalTime.Time, NetworkManager.LocalTime.Tick);
            else if (isLocalAuthorative)
            {
                // Disregard corrections before caching - this should be where the client thinks they are based on local simulation
                toTransform.position -= appliedTranslation;
                transformCache[NetworkManager.LocalTime.Time] = toTransform;
                toTransform.position += appliedTranslation;

                transformCache = transformCache
                    .Where(entry => NetworkManager.LocalTime.Time - entry.Key <= cacheTime)
                    .ToDictionary(entry => entry.Key, entry => entry.Value);

                // Apply authorative delta
                var deltaStrength = 1f / (1f + Mathf.Pow(deltaTransform.position.magnitude / 4f, 2f));
                var appliedDelta = deltaTransform.position.normalized * deltaStrength * correctionSpeed * NetworkManager.LocalTime.FixedDeltaTime;
                if (appliedDelta.magnitude > deltaTransform.position.magnitude)
                    appliedDelta = deltaTransform.position;

                appliedTranslation = appliedDelta;
                correctiveDelta = appliedDelta / NetworkManager.LocalTime.FixedDeltaTime;

                toTransform.position += appliedDelta;
                deltaTransform.position -= appliedDelta;
            }
        }
    }

    private void Update()
    {
        if (!interpolate)
            return;

        var f = (float)(NetworkManager.LocalTime.Time - fromTimestamp) / NetworkManager.LocalTime.FixedDeltaTime;
        f = Mathf.Clamp01(f);

        transform.localPosition = Vector3.Lerp(fromTransform.position, toTransform.position, f);
        transform.localRotation = Quaternion.Lerp(Quaternion.Euler(fromTransform.rotationEuler), Quaternion.Euler(toTransform.rotationEuler), f);
        transform.localScale = Vector3.Lerp(fromTransform.scale, toTransform.scale, f);
    }

    [ClientRpc]
    private void CommitPositionClientRpc(TransformState transform, double timestamp, int tick)
    {
        if (IsServer)
            return;

        lastTimestamp = timestamp;
        latency = NetworkManager.LocalTime.Time - timestamp;
        // latency = (NetworkManager.LocalTime.Tick - NetworkManager.ServerTime.Tick) * NetworkManager.ServerTime.FixedDeltaTime;
        tickLatency = NetworkManager.LocalTime.Tick - tick;

        if (isLocalAuthorative)
        {
            // Find nearest cached transform to timestamp
            TransformState closestTransform = transformCache.Values.GetEnumerator().Current;
            double closestMetric = double.PositiveInfinity;

            foreach (var entry in transformCache)
            {
                var cachedTimestamp = entry.Key;
                var cachedTransform = entry.Value;
                var metric = Math.Abs((timestamp - latency) - cachedTimestamp);

                if (metric < closestMetric)
                {
                    closestMetric = metric;
                    closestTransform = cachedTransform;
                }
            }

            if (Vector3.Distance(toTransform.position, transform.position) < Vector3.Distance(closestTransform.position, transform.position))
                closestTransform = toTransform;

            // Set delta from closest match to cumulative delta
            deltaTransform.position = transform.position - closestTransform.position;
            deltaTransform.scale = transform.scale - closestTransform.scale;
            deltaTransform.rotationEuler = AccumulateRotationTransform(deltaTransform.rotationEuler, transform.rotationEuler, closestTransform.rotationEuler);

            authorativePosition = transform.position;
            closestCachedPosition = closestTransform.position;
        }
        else
        {
            fromTransform = toTransform;
            toTransform = transform;
            fromTimestamp = NetworkManager.ServerTime.Time;
        }
    }

    private Vector3 AccumulateRotationTransform(Vector3 accumulatorEuler, Vector3 aEuler, Vector3 bEuler)
    {
        var accumulator = Quaternion.Euler(accumulatorEuler);
        var a = Quaternion.Euler(aEuler);
        var b = Quaternion.Euler(bEuler);

        var delta = a * Quaternion.Inverse(b);
        accumulator = delta * accumulator;

        return accumulator.eulerAngles;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(authorativePosition, 1f);

        Gizmos.color = Color.blue;
        foreach (var cachedTransform in transformCache.Values)
            Gizmos.DrawWireSphere(cachedTransform.position, 0.5f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(closestCachedPosition, 1f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(closestCachedPosition, closestCachedPosition + correctiveDelta);
    }
}
