using System;
using UnityEngine;

[Serializable]
public class TickHistoryBuffer<T>
{
    [Serializable]
    private struct Entry
    {
        public bool isPresent;
        public T value;
    }

    [SerializeField] private Entry[] data;
    [SerializeField] private int latestFrame;
    [SerializeField] private int headIdx;

    public T defaultValue;

    public TickHistoryBuffer(int size, int startingFrame)
    {
        data = new Entry[size];
        latestFrame = startingFrame;
        headIdx = 0;

        for (int i = 0; i < size; ++i)
            data[i].isPresent = false;
    }

    /// <summary>
    /// Push new state to history buffer.
    /// </summary>
    /// 
    /// This will advance the latest known frame.
    /// 
    /// <param name="value">State</param>
    public void Push(T value)
    {
        headIdx = (headIdx + 1) % data.Length;
        ++latestFrame;

        data[headIdx].isPresent = true;
        data[headIdx].value = value;
    }

    /// <summary>
    /// Push empty state to history buffer.
    /// </summary>
    /// 
    /// This will advance the latest known frame.
    public void PushEmpty()
    {
        headIdx = (headIdx + 1) % data.Length;
        ++latestFrame;

        data[headIdx].isPresent = false;
    }

    /// <summary>
    /// Set the given frame's state.
    /// </summary>
    /// 
    /// If the frame is in the future, this will advance the latest known frame.
    /// 
    /// If the frame is in the past, this will set an older frame's value.
    /// 
    /// If the frame is too far in the past ( i.e. further than the configured buffer length ), it is silently ignored.
    /// 
    /// <param name="value">State</param>
    /// <param name="at">Frame index</param>
    public void Set(T value, int at)
    {
        if (at < latestFrame)
        {
            if (latestFrame - at >= data.Length)
                return;

            int idx = (headIdx - (latestFrame - at) + data.Length) % data.Length;
            data[idx].isPresent = true;
            data[idx].value = value;
            return;
        }
        else if (at == latestFrame)
        {
            data[headIdx].isPresent = true;
            data[headIdx].value = value;
        }
        else if (at > latestFrame)
        {
            while (latestFrame < at)
                PushEmpty();

            Push(value);
        }
    }

    /// <summary>
    /// Get state for given frame.
    /// </summary>
    /// 
    /// If the requested frame is in the future, the most recent frame is returned.
    /// 
    /// If the requested frame is in the past, the last known state will be returned.
    /// 
    /// If the requested frame is in the past and no present frame is found, the default state will be returned.
    /// 
    /// <param name="frame">Frame index</param>
    /// <returns>Found state</returns>
    public T Get(int frame)
    {
        if (frame > latestFrame)
            frame = latestFrame;

        int offset = latestFrame - frame;
        if (offset >= data.Length)
            offset = data.Length - 1;

        while (offset < data.Length)
        {
            int idx = (headIdx - offset + data.Length) % data.Length;
            if (data[idx].isPresent)
                return data[idx].value;
            else
                ++offset;
        }

        return defaultValue;
    }
}
