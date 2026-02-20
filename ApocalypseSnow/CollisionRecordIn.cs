using System.Runtime.InteropServices;

namespace ApocalypseSnow;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CollisionRecordIn
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string _tag;
    public float _x;
    public float _y;
    public int _width;
    public int _height;

    public CollisionRecordIn(string tag, float x, float y, int width, int height)
    {
       
        this._tag = tag;
        this._y = y;
        this._x = x;
        this._width = width;
        this._height = height;
    }
}
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CollisionRecordOut
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string _myTag;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string _otherTag;
        public int _type;
    

    public CollisionRecordOut(string myTag, string otherTag, int type)
        {
            this._myTag = myTag;
            this._otherTag = otherTag;
            this._type = type;
        
    }
}