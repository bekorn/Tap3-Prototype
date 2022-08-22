using UnityEngine;

namespace Gameplay.Scripts
{
[CreateAssetMenu(menuName = "Gameplay/BlockColor")]
public class BlockColor : ScriptableObject
{
    public Color color;

    public static implicit operator Color(BlockColor bc) => bc.color;
    
    public Color Darker => Color.Lerp(color, Color.black, 0.1f);
}
}