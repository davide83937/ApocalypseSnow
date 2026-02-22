namespace ApocalypseSnow;

public struct JoinStruct
{
    public MessageType Type;
    public string playerName;
    
    public JoinStruct(string playerName)
    {
        Type = MessageType.PlayerJoin;
        this.playerName = playerName;
    }
}