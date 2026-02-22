using System;
using UnityEngine;

namespace Back2War.Core.Simulation
{
    /// <summary>
    /// Determinism constraints:
    /// - LocalSimTick is the ordering authority.
    /// - Ticks advance from FixedUpdate step accumulation only.
    /// - Apply ticks are computed as current tick + input delay.
    /// </summary>
    public sealed class SimClock : MonoBehaviour
    {
        [SerializeField, Min(1)] private int tickRate = 20;
        [SerializeField, Min(0)] private int inputDelayTicks = 3;
        [SerializeField] private bool autoAdvance = true;

        private double _fixedStepAccumulator;

        public uint LocalSimTick { get; private set; }
        public int TickRate => tickRate;
        public int InputDelayTicks => inputDelayTicks;

        private void FixedUpdate()
        {
            if (!autoAdvance)
            {
                return;
            }

            double simStepSeconds = 1.0d / Math.Max(1, tickRate);
            _fixedStepAccumulator += Time.fixedDeltaTime;

            while (_fixedStepAccumulator + 1e-12d >= simStepSeconds)
            {
                _fixedStepAccumulator -= simStepSeconds;
                LocalSimTick++;
            }
        }

        public uint ComputeApplyTick()
        {
            return LocalSimTick + (uint)Mathf.Max(0, inputDelayTicks);
        }

        public void SetLocalSimTick(uint tick)
        {
            LocalSimTick = tick;
            _fixedStepAccumulator = 0.0d;
        }
    }
}
