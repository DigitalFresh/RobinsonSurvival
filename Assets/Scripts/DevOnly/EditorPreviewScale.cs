using UnityEngine;                          // Базовые типы Unity

#if UNITY_EDITOR                             // Компилировать только в редакторе
[ExecuteAlways]                              // Выполнять в Edit Mode и Play Mode
public class EditorPreviewScale : MonoBehaviour
{
    [Range(0.1f, 10f)]                       // Слайдер в инспекторе
    public float editorScale = 3f;           // Во сколько раз увеличить сетку в Scene-вью

    public bool onlyAffectInEdit = true;     // Масштабировать только вне Play Mode

    private void OnEnable()                  // Вызывается при включении компонента
    {
        ApplyScale();                        // Применить масштаб
    }

    private void OnValidate()                // При изменении полей в инспекторе
    {
        ApplyScale();                        // Применить изменения
    }

    private void ApplyScale()                // Применение логики масштаба
    {
        if (onlyAffectInEdit && Application.isPlaying)     // Если разрешено только в редакторе и сейчас Play
        {
            transform.localScale = Vector3.one;            // Сбрасываем масштаб в 1×
            return;                                        // Выходим
        }

        transform.localScale = Vector3.one * editorScale;  // Увеличиваем/уменьшаем по желанию
    }
}
#endif
