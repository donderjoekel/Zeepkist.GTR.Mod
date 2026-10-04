using System;
using System.Collections;
using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Utilities;

/// <summary>Append-only capture buffer. Growth never copies existing frame payloads.</summary>
internal sealed class StructFrameBuffer<T> : IReadOnlyList<T> where T : struct
{
    private readonly int _blockSize;
    private readonly List<T[]> _blocks = new();
    public int Count { get; private set; }

    public StructFrameBuffer(int blockSize)
    {
        if (blockSize <= 0) throw new ArgumentOutOfRangeException(nameof(blockSize));
        _blockSize = blockSize;
    }

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _blocks[index / _blockSize][index % _blockSize];
        }
    }

    public void Add(T frame)
    {
        int offset = Count % _blockSize;
        if (offset == 0) _blocks.Add(new T[_blockSize]);
        _blocks[_blocks.Count - 1][offset] = frame;
        Count++;
    }

    public IEnumerator<T> GetEnumerator()
    {
        int remaining = Count;
        foreach (T[] block in _blocks)
        {
            int count = Math.Min(remaining, _blockSize);
            for (int i = 0; i < count; i++) yield return block[i];
            remaining -= count;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
