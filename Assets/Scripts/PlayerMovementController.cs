using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;
using System.Linq;

[Serializable]
public class TickHistoryBuffer<T>
{
    [Serializable] private struct Entry
    {
        public bool isPresent;
        public T value;
    }

    [SerializeField] private Entry[] data;
    [SerializeField] private int latestFrame;
    [SerializeField] private int headIdx;

    public T defaultValue;

    public TickHistoryBuffer(int size, int startingFrame) {
        data = new Entry[size];
        latestFrame = startingFrame;
        headIdx = 0;

        for (int i = 0; i < size; ++i)
            data[i].isPresent = false;
    }

    public void Push(T value)
    {
        headIdx = (headIdx + 1) % data.Length;
        ++latestFrame;

        data[headIdx].isPresent = true;
        data[headIdx].value = value;
    }

    public void PushEmpty()
    {
        headIdx = (headIdx + 1) % data.Length;
        ++latestFrame;

        data[headIdx].isPresent = false;
    }

    public void Set(T value, int at)
    {
        if (at < latestFrame)
        {
            if (latestFrame - at >= data.Length)
            {
                Debug.LogWarning($"Trying to set data too far in the past - {at} from {latestFrame} is {latestFrame - at} ago, buffer size is {data.Length} - ignoring");
                return;
            }

            int idx = (headIdx - (latestFrame - at) + data.Length) % data.Length;
            data[idx].isPresent = true;
            data[idx].value = value;
            return;
        } else if (at == latestFrame)
        {
            data[headIdx].isPresent = true;
            data[headIdx].value = value;
        } else if(at > latestFrame)
        {
            while (latestFrame < at)
                PushEmpty();

            Push(value);
        }
    }

    public T Get(int frame)
    {
        if (frame > latestFrame)
        {
            Debug.LogWarning($"Trying to get frame from the future ( {frame} > {latestFrame} ), returning current");
            frame = latestFrame;
        }

        int offset = latestFrame - frame;
        if (offset >= data.Length)
        {
            Debug.LogWarning($"Trying to get data too far in the past - {frame} from {latestFrame} is {offset} ago, buffer size is {data.Length} - returning oldest");
            offset = data.Length - 1;
        }

        while (offset < data.Length)
        {
            int idx = (headIdx - offset + data.Length) % data.Length;
            if (data[idx].isPresent)
                return data[idx].value;
            else
                ++offset;
        }

        Debug.LogError($"Couldn't find present data for frame {frame}, returning default");
        return defaultValue;
    }
}

public class PlayerMovementController : NetworkBehaviour
{
    private static readonly int HISTORY_LENGTH = 64;

    [Serializable]
    private struct CharacterState
    {
        public Vector3 position;
    }

    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;

    [Header("Runtime")]
    // private Dictionary<int, InputProvider.State> inputCache = new Dictionary<int, InputProvider.State>();
    // private Dictionary<int, CharacterState> stateCache = new Dictionary<int, CharacterState>();
    [SerializeField] private TickHistoryBuffer<InputProvider.State> inputCache;
    [SerializeField] private TickHistoryBuffer<CharacterState> stateCache;

    [SerializeField] private int earliestInput;
    [SerializeField] private int earliestState;
    [SerializeField] private bool needsResimulation = false;
    [SerializeField] private CharacterState fromState;
    [SerializeField] private CharacterState toState;
    [SerializeField] private double fromTime;
    [SerializeField] private double toTime;

    [Header("Debug")]
    [SerializeField] private int lastSeenTick;

    private void Start()
    {
        inputProvider ??= GetComponent<InputProvider>();
    }

    private void OnEnable()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkUpdate;
        lastSeenTick = NetworkManager.LocalTime.Tick;

        fromState = CaptureState();
        toState = CaptureState();
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton)
            NetworkManager.NetworkTickSystem.Tick -= NetworkUpdate;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        int currentTick = NetworkManager.LocalTime.Tick;
        earliestInput = currentTick;
        earliestState = currentTick;

        inputCache = new TickHistoryBuffer<InputProvider.State>(HISTORY_LENGTH, currentTick);
        stateCache = new TickHistoryBuffer<CharacterState>(HISTORY_LENGTH, currentTick);

        stateCache.Set(CaptureState(), currentTick);
        stateCache.defaultValue = CaptureState();

        Debug.Log($"Saved cache for tick {currentTick}");
    }

    private void Update()
    {
        if (IsServer)
            return;

        var currentTime = NetworkManager.LocalTime.Time;
        var f = 1.0 - (toTime - currentTime) / (toTime - fromTime);

        Debug.Log($"Interpolation factor {f} - {fromTime} < {currentTime} < {toTime}");

        var interpolatedState = new CharacterState()
        {
            position = Vector3.Lerp(fromState.position, toState.position, (float) f)
        };

        ApplyState(interpolatedState);
    }

    private void NetworkUpdate()
    {
        float deltaTime = NetworkManager.LocalTime.FixedDeltaTime;

        int currentTick = NetworkManager.LocalTime.Tick;

        fromState = toState; // stateCache.Get(NetworkManager.LocalTime.Tick - 2);
        fromTime = NetworkManager.LocalTime.Time;
        toTime = fromTime + deltaTime;

        if (lastSeenTick != NetworkManager.LocalTime.Tick - 1)
            Debug.LogError($"Local time skipped a tick: {lastSeenTick} -> {NetworkManager.LocalTime.Tick}");

        lastSeenTick = NetworkManager.LocalTime.Tick;

        if (IsServer)
        {
            if (needsResimulation)
            {
                if (currentTick - earliestInput >= 2)
                    Debug.Log($"Resimulating with input from tick#{earliestInput}, which was {currentTick - earliestInput} ticks ago.");

                for (int tick = earliestInput; tick <= currentTick; ++tick)
                    stateCache.Set(Simulate(inputCache.Get(tick), stateCache.Get(tick - 1), deltaTime), tick);

                CommitStateClientRpc(stateCache.Get(currentTick), currentTick);
                ApplyState(stateCache.Get(currentTick));

                needsResimulation = false;
                earliestInput = currentTick;
            }
        }
        else if (IsLocalPlayer)
        {
            inputCache.Set(inputProvider.Current, currentTick);
            CommitInputServerRpc(inputCache.Get(currentTick), currentTick);

            if (needsResimulation)
            {
                if (currentTick - earliestState >= 2)
                    Debug.Log($"Resimulating with authorative state from tick#{earliestState}, which was {currentTick - earliestState} ticks ago.");

                var startingState = CaptureState();

                for (int tick = earliestState; tick <= currentTick; ++tick)
                    stateCache.Set(Simulate(inputCache.Get(tick), stateCache.Get(tick - 1), deltaTime), tick);

                ApplyState(stateCache.Get(currentTick));
                var resultingState = stateCache.Get(currentTick);

                if (Vector3.Distance(startingState.position, resultingState.position) > 0.25f)
                    Debug.LogWarning($"Simulating from tick {earliestState} -> {currentTick} produced jump of {Vector3.Distance(startingState.position, resultingState.position)}");

                needsResimulation = false;
                earliestState = 0;
            }
        }

        toState = stateCache.Get(NetworkManager.LocalTime.Tick - 1);
    }

    private CharacterState Simulate(InputProvider.State input, CharacterState sourceState, float deltaTime)
    {
        ApplyState(sourceState);

        transform.position += input.movement * moveSpeed * deltaTime;

        return CaptureState();
    }

    private void ApplyState(CharacterState state)
    {
        transform.position = state.position;
    }

    private CharacterState CaptureState()
    {
        return new CharacterState()
        {
            position = transform.position
        };
    }

    [ClientRpc]
    private void CommitStateClientRpc(CharacterState state, int tick)
    {
        stateCache.Set(state, tick);

        earliestState = Math.Max(earliestState, tick);
        needsResimulation = true;
    }

    [ServerRpc]
    private void CommitInputServerRpc(InputProvider.State input, int tick)
    {
        inputCache.Set(input, tick);

        earliestInput = Math.Min(earliestInput, tick);
        needsResimulation = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(fromState.position, 1f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(toState.position, 1f);

        var currentTime = NetworkManager.LocalTime.Time;
        var f = 1.0 - (toTime - currentTime) / (toTime - fromTime);
        var interpolatedState = new CharacterState()
        {
            position = Vector3.Lerp(fromState.position, toState.position, (float)f)
        };

        Gizmos.color = Color.Lerp(Color.red, Color.green, (float) f);
        Gizmos.DrawWireSphere(interpolatedState.position, 1f);
    }
}
