namespace ApocalypseSnow;

public struct ShotStruct
{
    public MessageType Type;
    public float mouseX;
    public float mouseY;
    public int charge; // <-- Nuova variabile aggiunta
    
    public ShotStruct()
    {
        Type = MessageType.Shot;
    }
}