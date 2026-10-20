using System;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class PenguinColliderHandler
{
    private string _tag;
    public event EventHandler<string> eggTakenEvent;
    public event EventHandler<string> eggDeleteEvent;
    public event EventHandler eggPutEvent;
    public bool isFrozen = false;
    public bool isWithEgg = false;
    private float timeTakingEgg = 0;
    private float timePuttingEgg = 0;
    private bool deleteEggBool = false;

    public PenguinColliderHandler(string myTag)
    {
        _tag = myTag;
        Console.WriteLine(_tag);
    }


    public void resetTakingTimer()
    {
        timeTakingEgg = 0;
    }   
    
    public void resetPuttingTimer()
    {
        timePuttingEgg = 0;
    }  
    
    // In PenguinColliderHandler.cs


    
    public void HandleEggPickup(string eggTag, StateStruct stateStruct, 
        float deltaTime, ref string myEgg)
    {
        
        // Il congelamento impedisce di iniziare o completare la raccolta.
        if (isFrozen || stateStruct.IsPressed(StateList.Freezing))
            return;

        // Il remoto raccoglie soltanto se WithEgg arriva dalla rete;
        // il tempo di pressione di E non viene simulato sul client avversario.
        if (_tag.EndsWith("Red"))
        {
            if (stateStruct.IsPressed(StateList.WithEgg) && myEgg == null)
            {
                isWithEgg = true;
                timeTakingEgg = 0;
                myEgg = eggTag;
                eggTakenEventFunction(eggTag);
            }
            return;
        }

        // Il locale deve tenere premuto E per oltre un secondo.
        if (stateStruct.IsPressed(StateList.TakingEgg) && !stateStruct.IsPressed(StateList.WithEgg))
        {
            //Console.WriteLine("HandleEggPickup conteggio in corso");
            //Console.WriteLine("HandleEggPickup conteggio in corso dt: "+deltaTime);
            timeTakingEgg += deltaTime;
            //Console.WriteLine("HandleEggPickup conteggio in corso tt: "+timeTakingEgg);
            //Console.WriteLine(t);
            if (timeTakingEgg > 1)
            {
                //Console.WriteLine("HandleEggPickup uovo settato: "+eggTag);
                isWithEgg = true;
                timeTakingEgg = 0;
                myEgg = eggTag;
                Console.WriteLine(eggTag);
                eggTakenEventFunction(eggTag);
            }
        }
    }
 
    public void HandleHitByBall(ref string myEgg)
    {
        if (isWithEgg)
        {
            eggPutEvent?.Invoke(this, EventArgs.Empty);
        }
        isFrozen = true;
        timeTakingEgg = 0;
        timePuttingEgg = 0;
        isWithEgg = false;
        myEgg = null;
    }
    
    public bool IsEnemyBall(string otherTag)
    {
        // Determina se la palla colpita è nemica in base al tag del pinguino corrente
        return (_tag == "penguinRed" && otherTag.StartsWith("Ball")) ||
               (_tag == "penguin" && otherTag.StartsWith("RedBall"));
    }
    
// NOTA: aggiungi 'ref' prima di StateStruct
    public void HandleEggDelivery(string platformTag, ref StateStruct stateStruct, float deltaTime, ref string myEgg)
    {
        bool isCorrectPlatform = (_tag == "penguin" && platformTag == "blueP") || 
                                 (_tag == "penguinRed" && platformTag == "redP");
    
        // TRUCCO: Se abbiamo già fatto punto (deleteEggBool = true), 
        // forziamo il gioco a credere che il tasto sia ancora premuto per finire gli 0.2s!
        if (deleteEggBool)
        {
            stateStruct.Current |= StateList.PuttingEgg;
        }

        if (isCorrectPlatform && stateStruct.IsPressed(StateList.WithEgg) && stateStruct.IsPressed(StateList.PuttingEgg))
        {
            timePuttingEgg += deltaTime;
        
            // 1. A 1.0 secondi diamo il punto in locale e chiudiamo il lucchetto
            if (timePuttingEgg > 1f && !deleteEggBool)
            {
                deleteEgg(myEgg);
                deleteEggBool = true;
            }

            // 2. A 1.2 secondi abbiamo finito di inviare il "falso" tasto premuto: resettiamo tutto
            if (timePuttingEgg > 1.2f)
            {
                timePuttingEgg = 0;
                isWithEgg = false;
                myEgg = null;
                deleteEggBool = false;
            }
        }
        else
        {
            // Se rilascia il tasto PRIMA di fare 1.0 secondi, si resetta tutto normalmente
            timePuttingEgg = 0;
            deleteEggBool = false;
        }
    }
    
    public void SyncStateWithNetwork(StateStruct stateStruct, ref string myEgg)
    {
        // Se siamo il pinguino remoto e il server dice che NON abbiamo più l'uovo,
        // ma localmente risultiamo ancora "WithEgg", forziamo il reset immediato.
        if (_tag.EndsWith("Red") && !stateStruct.IsPressed(StateList.WithEgg) && isWithEgg)
        {
            // Notificare PRIMA di cancellare il tag: EggsEvent.addEgg
            // usa ancora il riferimento all'uovo trasportato.
            eggPutEvent?.Invoke(this, EventArgs.Empty);

            isWithEgg = false;
            myEgg = null;
            timePuttingEgg = 0;
            timeTakingEgg = 0;
        }
    }
    
    public void HandleObstacleCollision(int collisionType, ref Vector2 position)
    {
        const float bounceDistance = 1f;
        switch (collisionType)
        {
            case 1: position.Y -= bounceDistance; break; // TOP
            case 2: position.Y += bounceDistance; break; // BOTTOM
            case 3: position.X += bounceDistance; break; // LEFT
            case 4: position.X -= bounceDistance; break; // RIGHT
        }
    }
    
    public virtual void eggTakenEventFunction(string tagEgg)
    {
        eggTakenEvent?.Invoke(this, tagEgg);
    }
    
    public void putEgg(StateStruct stateStruct, ref string tagEgg)
    {
        if ((stateStruct.IsPressed(StateList.WithEgg) && stateStruct.JustPressed(StateList.TakingEgg))
            || (stateStruct.JustReleased(StateList.WithEgg) && stateStruct.JustPressed(StateList.Freezing)))
        {
            eggPutEvent?.Invoke(this, EventArgs.Empty);
            isWithEgg = false;
            tagEgg = null;
        }
    }

    public void deleteEgg(string tagEgg)
    {
        eggDeleteEvent?.Invoke(this, tagEgg);
    }
}