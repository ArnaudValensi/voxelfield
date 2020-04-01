using System;
using Collections;
using Components;
using Session.Player;
using UnityEngine;

namespace Session
{
    public abstract class SessionBase<TSessionState> where TSessionState : SessionStateComponentBase
    {
        protected class StampedPlayerStateComponent : StampComponent
        {
            public PlayerStateComponent state;
        }

        protected readonly SessionStates<TSessionState> m_States;
        protected uint m_Tick;

        protected SessionBase()
        {
            m_States = new SessionStates<TSessionState>();
        }

        public virtual void Render()
        {
        }

        protected virtual void Tick(uint tick, float time)
        {
        }

        public virtual void HandleInput()
        {
        }

        public void FixedUpdate()
        {
            Tick(m_Tick++, Time.realtimeSinceStartup);
        }

        protected bool InterpolateHistoryInto(PlayerStateComponent stateToInterpolate, CyclicArray<StampedPlayerStateComponent> stateHistory,
                                              float rollback, float timeSinceLastUpdate)
        {
            StampedPlayerStateComponent fromState = null, toState = null;
            var durationCount = 0.0f;
            for (var stateHistoryIndex = 0; stateHistoryIndex < stateHistory.Size; stateHistoryIndex++)
            {
                fromState = stateHistory.Get(-stateHistoryIndex - 1);
                toState = stateHistory.Get(-stateHistoryIndex);
                durationCount += toState.duration;
                if (durationCount >= rollback - timeSinceLastUpdate)
                    break;
                if (stateHistoryIndex == stateHistory.Size - 1)
                    // We have reached the end of the cyclic array
                    return false;
            }
            if (toState == null)
                throw new ArgumentException("Cyclic array is not big enough");
            float interpolation;
            if (toState.duration > 0.0f)
            {
                float timeIntoStateUs = durationCount - rollback + timeSinceLastUpdate;
                interpolation = timeIntoStateUs / toState.duration;
            }
            else
                interpolation = 0.0f;
            Interpolator.InterpolateInto(fromState.state, toState.state, stateToInterpolate, interpolation);
            return true;
        }
    }
}