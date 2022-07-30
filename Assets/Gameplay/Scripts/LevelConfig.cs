using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Gameplay.Scripts
{
[CreateAssetMenu(fileName = "New Level", menuName = "Gameplay/New Level")]
public class LevelConfig : ScriptableObject
{
    public List<BlockColor> colors;
    public List<Sprite> icons;
    public int2 size;
}
}