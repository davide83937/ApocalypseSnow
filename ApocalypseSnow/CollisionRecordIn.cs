namespace ApocalypseSnow;

public struct CollisionRecordIn
{
    public float _x;
    public float _y;
    public string _tag;
    public int _width;
    public int _height;

    public CollisionRecordIn(string tag, float y, float x, int width, int height)
    {
        this._tag = tag;
        this._y = y;
        this._x = x;
        this._width = width;
        this._height = height;
    }
}

public struct CollisionRecordOut
    {
        public string _myTag;
        public string _otherTag;
        public int _type;
    

    public CollisionRecordOut(string myTag, string otherTag, int type)
        {
            this._myTag = myTag;
            this._otherTag = otherTag;
            this._type = type;
        
    }
}