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
        private List<CollisionRecordOut> _collisionRecordOuts;
        

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
            _collisionRecordOuts = [];
        }

        
        [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void check_collisions(CollisionRecordIn *collisionRecordIn, CollisionRecordOut* collisionRecordOut, int count);
        
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
        
        public unsafe void SendToCpp()
        {
            // 1. Otteniamo l'array interno della lista (o la convertiamo in array)
            CollisionRecordIn[] inputData = _collisionRecordIns.ToArray(); 
            CollisionRecordOut[] outputResults = new CollisionRecordOut[100];

            if (inputData.Length == 0) return;

            // 2. Usiamo 'fixed' per bloccare l'array in memoria
            fixed (CollisionRecordIn* pIn = inputData)
            fixed (CollisionRecordOut* pOut = outputResults)    
            {
                // 3. Passiamo l'indirizzo di memoria e il numero di elementi al C++
                check_collisions(pIn, pOut, inputData.Length);
            }
            // Dopo il blocco 'fixed', il GC è libero di spostare di nuovo l'array
        }

        public override void Update(GameTime gameTime)
        {
            foreach (var elemento in _collisionRecordOuts)
            {
                Console.WriteLine(elemento._myTag);
                Console.WriteLine(elemento._otherTag); 
                Console.WriteLine(elemento._type); 
                Console.WriteLine("\n");
            }
            base.Update(gameTime);
        }
    }
}