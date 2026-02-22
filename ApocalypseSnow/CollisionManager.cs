using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ApocalypseSnow
{
    public class CollisionManager : GameComponent
    {
        private static CollisionManager _instance;
        private List<CollisionRecordIn> _collisionRecordIns;
        private CollisionRecordOut[] resultsBuffer;
        public event EventHandler<CollisionRecordOut> sendCollisionEvent;
        private int i = 0;
     

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

        
        [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void check_collisions(CollisionRecordIn[] collisionRecordIn, [Out] CollisionRecordOut[] collisionRecordOut, int count);
        
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
        
        public void SendToCpp()
        {
            // 1. Otteniamo l'array interno della lista (o la convertiamo in array)
            CollisionRecordIn[] inputData = _collisionRecordIns.ToArray();
            resultsBuffer = new CollisionRecordOut[100];
            
                check_collisions(inputData, resultsBuffer, inputData.Length);
        }

        public override void Update(GameTime gameTime)
        {
            SendToCpp();

            foreach (var elemento in resultsBuffer)
            {
                if (elemento._type > 0){ 
                    //Console.WriteLine("Collisione tra " + elemento._myTag + " e " + elemento._otherTag + " di tipo " + elemento._type);  
                    //Console.WriteLine(i);
                    i++;
                    sendCollision(elemento);
                }
            }
            
            base.Update(gameTime);
        }

        protected virtual void sendCollision(CollisionRecordOut collisionRecordOut)
        {
            sendCollisionEvent?.Invoke(this, collisionRecordOut);
        }
    }
}