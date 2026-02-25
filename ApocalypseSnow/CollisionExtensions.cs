using System;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public abstract class CollisionExtensions : DrawableGameComponent
{
    public string _tag;
    public Vector2 _position;

    protected CollisionExtensions(Game game, string tag, Vector2 position) : base(game)
    {
        _tag = tag;
        _position = position;
    }

    protected override void LoadContent()
    {
        // La sottoscrizione all'evento avviene una volta sola qui per tutti
        CollisionManager.Instance.sendCollisionEvent += BaseOnColliderEnter;
        base.LoadContent();
    }

    private void BaseOnColliderEnter(object context, CollisionRecordOut record)
    {
        // 1. Filtro universale: la collisione riguarda questo oggetto?
        if (record._myTag != _tag && record._otherTag != _tag) return;

        // 2. Identifico l'altro tag usando la logica comune
        string otherTag = (record._myTag == _tag) ? record._otherTag : record._myTag;
        //Console.WriteLine($"Collisione tra {_tag} e  {otherTag}");
        // 3. Chiamo il metodo specifico della classe figlia
        OnCollisionEnter(otherTag, record);
    }

    // Metodo che ogni classe figlia dovrà implementare
    protected abstract void OnCollisionEnter(string otherTag, CollisionRecordOut record);
}
