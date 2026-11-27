using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;


namespace ApocalypseSnow;

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

        
        //[DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
        //private static extern void check_collisions(CollisionRecordIn[] collisionRecordIn, [Out] CollisionRecordOut[] collisionRecordOut, int count);
        

        public void addObject(string tag, float x, float y, int w, int h,
            float minZ = 0f, float maxZ = float.MaxValue)
        {
            _collisionRecordIns.Add(new CollisionRecordIn(tag, x, y, w, h, minZ, maxZ));
        }

        public void modifyObject(string tag, float x, float y, int w, int h,
            float? minZ = null, float? maxZ = null)
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
                if (minZ.HasValue) record._minZ = minZ.Value;
                if (maxZ.HasValue) record._maxZ = maxZ.Value;
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
            int inputCount = inputData.Length*(inputData.Length-1)/2;
            resultsBuffer = new CollisionRecordOut[inputCount];
            
                PhysicsAPI.check_collisions(inputData, resultsBuffer, inputData.Length);
        }

        // I risultati dell'intero frame sono disponibili prima di inviare gli
        // eventi. Il pinguino puo' quindi riconoscere un ostacolo anche quando
        // il suo evento di collisione arriva prima di quello della palla.
        public bool ShotOverlapsObstacle(string shotTag, float shotHeight = 0f)
        {
            // Il C++ segnala tutte le sovrapposizioni XY.
            // Una palla sopra l'ostacolo non deve bloccare un HIT sul pinguino.
            if (shotHeight >= GameWorld.ObstacleCollisionHeight || resultsBuffer == null)
                return false;

            foreach (var result in resultsBuffer)
            {
                if (result._type <= 0)
                    continue;
                if ((result._myTag == shotTag && result._otherTag == "obstacle") ||
                    (result._otherTag == shotTag && result._myTag == "obstacle"))
                    return true;
            }
            return false;
        }

        public override void Update(GameTime gameTime)
        {
            SendToCpp();

            foreach (var elemento in resultsBuffer)
            {
                if (elemento._type > 0){ 
                    //Console.WriteLine("Collisione tra " + elemento._myTag + " e " + elemento._otherTag + " di tipo " + elemento._type);  
                    //Console.WriteLine(i);
                    //i++;
                    sendCollision(elemento);
                }
            }
            
            base.Update(gameTime);
        }

        protected virtual void sendCollision(CollisionRecordOut collisionRecordOut)
        {
            sendCollisionEvent?.Invoke(this, collisionRecordOut);
        }
        
        public void ClearAll()
        {
            // Svuota la lista dei record di input
            _collisionRecordIns?.Clear();

            // Opzionale: pulisci il buffer dei risultati resettandolo 
            // (non è strettamente necessario perché viene sovrascritto, ma è più pulito)
            if (resultsBuffer != null)
            {
                Array.Clear(resultsBuffer, 0, resultsBuffer.Length);
            }

            Console.WriteLine("CollisionManager: Liste pulite per la nuova partita.");
        }

        // Se una hitbox mobile penetra in un ostacolo fermo, la porta appena fuori.
        // Lavora su coordinate del CENTRO, non modifica i collider registrati.
        // Ogni passo di movimento (prediction, server e replay) applica questa regola.
        // TODO C++: spostare nella DLL l'intero risolutore (ciclo sugli ostacoli,
        // correzione minima per asse e massimo 8 passaggi). C# dovra' solo
        // passare la hitbox e gli ostacoli e ricevere il centro corretto.
        // Riutilizzare check_collisions senza modificare i collider registrati.
        public Vector2 ResolveObstacleOverlaps(Vector2 center, int width, int height)
        {
            const string candidateTag = "candidate";

            // Una correzione contro un muro puo' intersecare un secondo muro.
            // Ripetiamo fino alla separazione o a un limite finito (incastri impossibili).
            for (int pass = 0; pass < 8; pass++)
            {
                bool moved = false;
                foreach (var obstacle in _collisionRecordIns)
                {
                    if (obstacle._tag != "obstacle")
                        continue;

                    var input = new[]
                    {
                        new CollisionRecordIn(candidateTag, center.X, center.Y, width, height),
                        obstacle
                    };
                    var output = new CollisionRecordOut[1];
                    PhysicsAPI.check_collisions(input, output, 2);

                    if (output[0]._type == 0 || output[0]._penetration <= 0f)
                        continue;

                    float correction = output[0]._penetration *
                        (output[0]._myTag == candidateTag ? 1f : -1f);

                    switch (output[0]._type)
                    {
                        case 1: center.Y += correction; break; // primo sotto il secondo
                        case 2: center.Y -= correction; break; // primo sopra il secondo
                        case 3: center.X -= correction; break; // primo a sinistra
                        case 4: center.X += correction; break; // primo a destra
                    }
                    moved = true;
                }

                if (!moved)
                    break;
            }

            return center;
        }
    }
