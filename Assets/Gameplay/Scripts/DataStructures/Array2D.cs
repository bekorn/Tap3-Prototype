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

    public ref T this[int2 idx] => ref array[idx.x * height + idx.y];
    
    public ref T this[int x, int y] => ref array[x * height + y];

    public void Clear() => Array.Clear(array, 0, array.Length);
}
}