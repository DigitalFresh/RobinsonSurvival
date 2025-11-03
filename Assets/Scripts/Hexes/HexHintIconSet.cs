using UnityEngine;

/// Набор иконок для HexHintType (индексы совпадают с enum’ом: 0=None, 1=Enemy, 2=Info, 3=Food, 4=GoldStar, 5=SilverStar)
[CreateAssetMenu(menuName = "Robinson/Hex/HexHint Icon Set", fileName = "HexHintIconSet")]
public class HexHintIconSet : ScriptableObject
{
    [Tooltip("Иконки по индексам enum HexHintType: [0]=None, [1]=Enemy, [2]=Info, [3]=Food, [4]=GoldStar, [5]=SilverStar")]
    public Sprite[] icons;

    public Sprite Get(HexHintType t)
    {
        int i = (int)t;
        return (icons != null && i >= 0 && i < icons.Length) ? icons[i] : null;
    }
}
