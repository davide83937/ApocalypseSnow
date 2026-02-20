using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ApocalypseSnow
{
    public class CollisionManager : GameComponent
    {
        private static CollisionManager _instance;
        private List<CollisionRecordIn> _collisionRecordIns;
        

        public static CollisionManager Instance
        {
            get
            {
                if (_instance == null)
                    throw new Exception("CollisionManager deve essere inizializzato in Game1 prima dell'uso!");
                return _instance;
            }
        }

        // Il costruttore deve accettare 'Game' e passarlo al padre tramite base(game)
        public CollisionManager(Game game) : base(game)
        {
            if (_instance != null)
                throw new Exception("Puoi creare solo una istanza di CollisionManager!");
            
            _instance = this;
            _collisionRecordIns = [];
        }

        public void addObject(string tag, float x, float y, int w, int h)
        {
            _collisionRecordIns.Add(new CollisionRecordIn(tag, x, y, w, h));
        }

        public void modifyObject(string tag, float x, float y, int w, int h)
        {
            int index = _collisionRecordIns.FindIndex(r => r._tag == tag);

            if (index != -1)
            {
                // Elemento trovato! Puoi usarlo per modificare la struct
                CollisionRecordIn record = _collisionRecordIns[index];
                record._tag = tag;
                record._x = x;
                record._y = y;
                record._width = w;
                record._height = h;
                _collisionRecordIns[index] = record;
                // ... logica di modifica ...
            }
        }

        public void removeObject(string tag)
        {
            _collisionRecordIns.RemoveAll(r => r._tag == tag);
        }

        public override void Update(GameTime gameTime)
        {
            foreach (var elemento in _collisionRecordIns)
            {
            
                Console.WriteLine(elemento); 
            }
            base.Update(gameTime);
        }
    }
}