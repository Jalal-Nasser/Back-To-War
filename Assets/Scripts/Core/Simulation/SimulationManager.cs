using UnityEngine;

namespace Back2War.Core.Simulation
{
    /// <summary>
    /// Fixed-tick simulation processing entry point.
    /// </summary>
    public sealed class SimulationManager : MonoBehaviour
    {
        [SerializeField] private SimClock clock;
        [SerializeField] private CommandQueue queue;

        private void Awake()
        {
#pragma warning disable CS0618
            if (clock == null)
            {
                clock = FindObjectOfType<SimClock>();
            }

            if (queue == null)
            {
                queue = FindObjectOfType<CommandQueue>();
            }
#pragma warning restore CS0618
        }

        private void FixedUpdate()
        {
            if (clock == null || queue == null)
            {
                return;
            }

            queue.ProcessTick(clock.LocalTick);
        }
    }
}
