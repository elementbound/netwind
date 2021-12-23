using UnityEngine;
using Unity.Netcode;
using com.github.elementbound.NetWind;

[RequireComponent(typeof(RewindableTransformState))]
public class MinionController : EmptyStateBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private float followDistance;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private double lifetime = 2.0;

    [Header("Runtime")]
    [SerializeField] private NetworkVariable<Vector3> followOffset;
    [SerializeField] private double spawnedAt;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        spawnedAt = NetworkRewindManager.Instance.Time;
    }

    public override void Simulate(int tick, float deltaTime)
    {
        if (NetworkRewindManager.Instance.Time - spawnedAt > lifetime && IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
            return;
        }

        if (!followTarget)
            return;

        var deltaToTarget = followTarget.position - transform.position;
        var deltaToPoint = deltaToTarget + followOffset.Value;

        var targetWeight = Mathf.Clamp01((deltaToTarget.magnitude - followDistance) / followDistance);

        var delta = Vector3.Lerp(deltaToPoint, deltaToTarget, targetWeight);
        if (delta.magnitude < moveSpeed * deltaTime)
            transform.position += delta;
        else
            transform.position += moveSpeed * deltaTime * delta.normalized;
    }

    public void Attach(Transform target)
    {
        followTarget = target;
        followOffset.Value = (new Vector3(1 - 2 * Random.value, 0f, 1 - 2 * Random.value)).normalized * Mathf.Lerp(followDistance * 0.25f, followDistance, Random.value);
    }
}
