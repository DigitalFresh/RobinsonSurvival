using UnityEngine;                     // Базовые типы Unity

// Глобальное (простое) состояние ввода, чтобы системы знали, что сейчас идёт панорамирование.
public static class InputSharedState
{
    public static bool IsPanning = false; // true, когда пользователь тянет карту ПКМ
}
