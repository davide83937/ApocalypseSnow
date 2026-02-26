namespace ApocalypseSnow;

public struct ShotStruct
{
    public MessageType Type;
    public int mouseX;
    public int mouseY;
    public int charge; // <-- Nuova variabile aggiunta
    
    public ShotStruct()
    {
        Type = MessageType.Shot;
    }
}