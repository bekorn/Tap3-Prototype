using System;
using Unity.Mathematics;
using UnityEngine;

namespace Gameplay.Scripts
{
[CreateAssetMenu(fileName = "New Level", menuName = "Gameplay/Level")]
public class Level : ScriptableObject
{
    [Serializable]
    public struct BlockType
    {
        public BlockColor blockColor;
        public Sprite icon;
    }

    public Sprite cell;
    public Sprite block;
    public BlockType[] blockTypes;
    public int2 size;
}
}