namespace ApocalypseSnow;

[System.Flags]
public enum StateList
{
    None = 0,
    Up = 1 << 0,       // Sostituisce IsW
    Down = 1 << 1,     // Sostituisce IsS
    Left = 1 << 2,     // Sostituisce IsA
    Right = 1 << 3,    // Sostituisce IsD
    Reload = 1 << 4,   //  Sostituisce IsR
    Shoot = 1 << 5,    // Sostituisce IsLeft
    Moving = 1 << 6,    // Sostituisce IsMoving
}