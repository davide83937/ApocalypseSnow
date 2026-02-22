namespace ApocalypseSnow;

public struct ShotStruct
{
    public MessageType Type;
    public int mouseX;
    public int mouseY;
    
    public ShotStruct()
    {
        Type = MessageType.Shot;
    }
}