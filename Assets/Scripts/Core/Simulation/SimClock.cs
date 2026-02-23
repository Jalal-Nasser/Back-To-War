using UnityEngine;

namespace Back2War.Core.Simulation
{
    /// <summary>
    /// Deterministic simulation tick clock.
    /// </summary>
    public sealed class SimClock : MonoBehaviour
    {
        public int TickRate = 20;
        public int InputDelayTicks = 3;

        public uint LocalTick { get; private set; }

        private void FixedUpdate()
        {
            LocalTick++;
        }

        public uint ComputeApplyTick()
        {
            return LocalTick + (uint)Mathf.Max(0, InputDelayTicks);
        }
    }
}
