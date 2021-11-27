using UnityEngine;
using com.github.elementbound.NetWind;

[RequireComponent(typeof(RewindableTransformState))]
public class PlayerMovementController : EmptyStateBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;

    private void OnEnable()
    {
        inputProvider ??= GetComponent<InputProvider>();
    }

    public override void Simulate(int tick, float deltaTime)
    {
        Debug.Log($"[Player] Simulating tick {tick}");
        var input = inputProvider.Current;
        transform.position += input.movement * moveSpeed * deltaTime;
    }
}
