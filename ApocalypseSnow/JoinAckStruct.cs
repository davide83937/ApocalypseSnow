namespace ApocalypseSnow;

public struct JoinAckStruct
{
    public MessageType Type;    // 1 byte
    public uint PlayerId;       // 4 byte
    public float SpawnX;        // 4 byte
    public float SpawnY;        // 4 byte
}