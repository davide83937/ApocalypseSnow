namespace ApocalypseSnow;

public struct EndMatchStruct(bool result, int reason)
{
    private bool _result = result;
    private int _reason = reason;
}