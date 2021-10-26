using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputProvider : MonoBehaviour
{
    [Serializable]
    public class State
    {
        public Vector3 movement;

        public void Reset()
        {
            movement = Vector3.zero;
        }

        public State AverageOver(int sampleCount)
        {
            return new State()
            {
                movement = this.movement / sampleCount
            };
        }
    }

    [Header("Runtime")]
    [SerializeField] private State cumulativeInput;
    [SerializeField] private State currentInput;
    [SerializeField] private int sampleCount;

    private void Update()
    {
        var movement = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        if (movement.magnitude > 1f)
            movement.Normalize();

        cumulativeInput.movement += movement;
        sampleCount++;
    }

    private void FixedUpdate()
    {
        currentInput = cumulativeInput.AverageOver(sampleCount);
        cumulativeInput.Reset();
        sampleCount = 0;
    }

    public State Current => currentInput;
}
