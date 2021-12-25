namespace com.github.elementbound.NetWind
{
    public class NetworkRewindEvents
    {
        public delegate void ResimulateEvent(int fromTick, int toTick);
        public delegate void RewindEvent(int tick);

        /// <summary>
        /// Event invoked before resimulating a tick range.
        /// </summary>
        public ResimulateEvent BeforeResimulate;

        /// <summary>
        /// Event called during resimulation, for each tick, after the state is
        /// restored, but before simulating the new state.
        /// </summary>
        public RewindEvent OnTickRestore;

        /// <summary>
        /// Event called once the given tick is simulated.
        /// </summary>
        public RewindEvent OnTickSimulate;

        /// <summary>
        /// Event called after resimulation, once the state is restored to the
        /// visual state ( i.e. the state that was displayOffset ticks ago ).
        /// </summary>
        public RewindEvent OnVisualRestore;
    }
}