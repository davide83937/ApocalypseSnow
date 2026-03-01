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
    
    public void HandleEggPickup(string eggTag, StateStruct stateStruct, 
        float deltaTime, ref string myEgg)
    {
        
        // NOVITÀ: Se è il pinguino remoto e il server dice che ha l'uovo, 
        // completiamo la raccolta immediatamente senza aspettare il timer locale.
        if (_tag.EndsWith("Red") && stateStruct.IsPressed(StateList.WithEgg)&& myEgg == null)
        {
            isWithEgg = true;
            timeTakingEgg = 0;
            myEgg = eggTag;
            eggTakenEventFunction(eggTag);
            return;
        } 
        
        //Console.WriteLine("HandleEggPickup scatenato");
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
    
    public void HandleEggDelivery(string platformTag,
        StateStruct stateStruct, float deltaTime, ref string myEgg)
    {
        // Verifica se il pinguino sta consegnando l'uovo alla piattaforma corretta
        bool isCorrectPlatform = (_tag == "penguin" && platformTag == "blueP") || 
                                 (_tag == "penguinRed" && platformTag == "redP");

        if (isCorrectPlatform && stateStruct.IsPressed(StateList.WithEgg) && stateStruct.IsPressed(StateList.PuttingEgg))
        {
            timePuttingEgg += deltaTime;
            //Console.WriteLine(timePuttingEgg);
            if (timePuttingEgg > 1)
            {
                deleteEgg(myEgg);
                timePuttingEgg = 0;
                isWithEgg = false;
                myEgg = null;
            }
        }
    }
    
    public void HandleObstacleCollision(int collisionType, ref Vector2 position)
    {
        const float bounceDistance = 5f;
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
        if ((stateStruct.IsPressed(StateList.WithEgg)&&stateStruct.JustPressed(StateList.TakingEgg))
            || (stateStruct.JustReleased(StateList.WithEgg)&&stateStruct.JustPressed(StateList.Freezing)))
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