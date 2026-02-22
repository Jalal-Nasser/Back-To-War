using UnityEngine;

namespace Back2War.Core.Simulation
{
    /// <summary>
    /// Determinism constraints:
    /// - Simulation stage processing is keyed by LocalSimTick only.
    /// - Each tick is processed at most once in monotonic order.
    /// </summary>
    public sealed class SimulationManager : MonoBehaviour
    {
        [SerializeField] private SimClock simClock;
        [SerializeField] private CommandQueue commandQueue;

        private bool _initialized;
        private uint _lastProcessedTick;

        private void Awake()
        {
#pragma warning disable CS0618
            if (simClock == null)
            {
                simClock = FindObjectOfType<SimClock>();
            }

            if (commandQueue == null)
            {
                commandQueue = FindObjectOfType<CommandQueue>();
            }
#pragma warning restore CS0618
        }

        private void Update()
        {
            if (simClock == null || commandQueue == null)
            {
                return;
            }

            if (!_initialized)
            {
                _lastProcessedTick = simClock.LocalSimTick;
                commandQueue.ProcessTick(_lastProcessedTick);
                _initialized = true;
            }

            while (_lastProcessedTick < simClock.LocalSimTick)
            {
                _lastProcessedTick++;
                commandQueue.ProcessTick(_lastProcessedTick);
            }
        }
    }
}
