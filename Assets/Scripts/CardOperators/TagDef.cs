// TagDef.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Robinson/Tags/TagDef", fileName = "Tag")]
public class TagDef : ScriptableObject
{
    public string id;               // машинный id: "Timid", "Ranged", ...
    public Sprite uiIcon;           // иконка тега (для карточки/бейджа/панелей)
    [TextArea] public string description; // ЧЕЛОВЕЧЕСКОЕ описание (показываем игроку)
}
