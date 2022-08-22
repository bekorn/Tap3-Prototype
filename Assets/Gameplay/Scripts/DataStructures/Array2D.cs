using System;
using Unity.Mathematics;

namespace Gameplay.Scripts.DataStructures
{
public readonly struct Array2D<T>
{
    public readonly int height;
    public readonly T[] array;

    public Array2D(int2 size)
    {
        height = size.y;
        array = new T[size.x * size.y];
    }

    public bool IsInit => array is not null;
    public int Length => array.Length;

    public ref T this[int x] => ref array[x];
    public ref T this[int x, int y] => ref array[x * height + y];
    public ref T this[int2 idx] => ref array[idx.x * height + idx.y];

    public void Clear() => Array.Clear(array, 0, array.Length);

    public Enumerator GetEnumerator() => new(array);

    public struct Enumerator
    {
        readonly T[] array;
        int idx;

        internal Enumerator(T[] array) => (this.array, idx) = (array, -1);

        public bool MoveNext() => ++idx < array.Length;

        public ref T Current => ref array[idx];
    }
}

public static class Array2DUtility
{
    public static int2 Idx2Grid(int idx, int height) => new(idx / height, idx % height);
}
}