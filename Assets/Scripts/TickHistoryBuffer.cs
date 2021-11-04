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

    public TickHistoryBuffer(int size, int startingFrame)
    {
        data = new Entry[size];
        latestFrame = startingFrame;
        headIdx = 0;

        for (int i = 0; i < size; ++i)
            data[i].isPresent = false;
    }

    private int FrameToIdx(int frame)
    {
        int offset = frame - latestFrame;

        if (offset <= -data.Length)
            throw new IndexOutOfRangeException($"Can't convert frame {frame} to index; frame is too far in the past");

        return (headIdx + offset + data.Length) % data.Length;
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
            while (latestFrame < at - 1)
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
    /// <param name="frame">Frame index</param>
    /// <returns>Found state</returns>
    /// 
    /// <throws>IndexOutOfRange if no backfill frame found</throws>
    public T Get(int frame)
    {
        int latestKnownFrame = GetLatestKnownFrame();
        int earliestKnownFrame = GetEarliestKnownFrame();

        if (frame >= latestKnownFrame)
            return data[FrameToIdx(latestKnownFrame)].value;

        if (frame <= earliestKnownFrame)
            return data[FrameToIdx(earliestKnownFrame)].value;

        int offset = latestFrame - frame;

        while (offset < data.Length)
        {
            int idx = (headIdx - offset + data.Length) % data.Length;
            if (data[idx].isPresent)
                return data[idx].value;
            else
                ++offset;
        }

        throw new IndexOutOfRangeException($"No past data found for frame {frame} ( earliest known is {earliestKnownFrame} )");
    }

    public int LatestFrame => latestFrame;

    public int GetLatestKnownFrame()
    {
        for (int i = 0; i < data.Length; ++i)
        {
            int idx = (headIdx + data.Length - i) % data.Length;
            if (data[idx].isPresent)
                return latestFrame - i;
        }

        return latestFrame - data.Length;
    }

    public int GetEarliestKnownFrame()
    {
        for (int i = 0; i < data.Length; ++i)
        {
            int idx = (headIdx + i + 1) % data.Length;
            if (data[idx].isPresent)
                return latestFrame - data.Length + i;
        }

        return latestFrame;
    }
}
