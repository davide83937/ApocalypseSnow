using System;
using System.Collections.Generic; // Necessario per le Liste
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
    // Dopo un HIT il remoto ignora i vecchi WithEgg finche' la rete non comunica il rilascio.
    private bool _remotePickupWaitsForEggRelease;
    private float timeTakingEgg = 0;
    // Durate originali del deposito sulla piattaforma: 1 s di pressione + 0,2 s di completamento.
    private const float EggDeliverySeconds = 1f;
    private const float EggDeliveryCleanupSeconds = 0.2f;
    private float timePuttingEgg = 0;
    private bool deleteEggBool = false;

    // Variabile per memorizzare l'uovo con ID minore scelto
    private string _lockedEggTag = null; 

    public PenguinColliderHandler(string myTag)
    {
        _tag = myTag;
        Console.WriteLine(_tag);
    }

    public void resetTakingTimer()
    {
        timeTakingEgg = 0;
        _lockedEggTag = null; // Resetta l'uovo target
    }   
    
    public void resetPuttingTimer()
    {
        timePuttingEgg = 0;
    }  

    // Metodo helper per trovare l'uovo con ID minore nella lista
    private string GetLowestIdEgg(List<string> eggs)
    {
        string lowestEgg = eggs[0];
        int minId = int.Parse(lowestEgg.Replace("egg", ""));

        for (int i = 1; i < eggs.Count; i++)
        {
            int currentId = int.Parse(eggs[i].Replace("egg", ""));
            if (currentId < minId)
            {
                minId = currentId;
                lowestEgg = eggs[i];
            }
        }
        return lowestEgg;
    }

    // MODIFICATO: ora riceve la lista delle uova in collisione
    public void HandleEggPickup(List<string> collidingEggs, StateStruct stateStruct, float deltaTime, ref string myEgg)
    {
        // Il rilascio puo' arrivare mentre il remoto e' ancora congelato.
        // Va riconosciuto anche se il freeze impedisce la raccolta.
        if (_tag.EndsWith("Red") && !stateStruct.IsPressed(StateList.WithEgg))
            _remotePickupWaitsForEggRelease = false;

        if (isFrozen || stateStruct.IsPressed(StateList.Freezing))
        {
            _lockedEggTag = null;
            return;
        }

        // Il remoto raccoglie soltanto se WithEgg arriva dalla rete
        if (_tag.EndsWith("Red"))
        {
            // Un WithEgg ancora valido prima dell'HIT non e' una nuova raccolta.
            if (_remotePickupWaitsForEggRelease)
                return;

            if (stateStruct.IsPressed(StateList.WithEgg) && myEgg == null && collidingEggs.Count > 0)
            {
                // Anche in remoto prende quello con ID minore
                string targetEgg = GetLowestIdEgg(collidingEggs);
                isWithEgg = true;
                timeTakingEgg = 0;
                myEgg = targetEgg;
                eggTakenEventFunction(targetEgg);
            }
            return;
        }

        // --- LA TUA LOGICA QUI ---
        if (stateStruct.IsPressed(StateList.TakingEgg) && !stateStruct.IsPressed(StateList.WithEgg))
        {
            if (collidingEggs.Count > 0)
            {
                // Appena premo il tasto, se non ho bloccato un uovo, prendo quello con ID minore dalla lista
                if (_lockedEggTag == null)
                {
                    _lockedEggTag = GetLowestIdEgg(collidingEggs);
                }

                // Inizio il timer SOLO per l'uovo bloccato (e mi assicuro di esserci ancora in collisione)
                if (collidingEggs.Contains(_lockedEggTag))
                {
                    timeTakingEgg += deltaTime;

                    if (timeTakingEgg > 1)
                    {
                        isWithEgg = true;
                        timeTakingEgg = 0;
                        myEgg = _lockedEggTag;
                        Console.WriteLine(_lockedEggTag);
                        eggTakenEventFunction(_lockedEggTag);
                        _lockedEggTag = null; // Finito, lo resetto
                    }
                }
                else
                {
                    // Se esce dalla collisione con l'uovo bersaglio, resetta
                    timeTakingEgg = 0;
                    _lockedEggTag = null;
                }
            }
        }
        else
        {
            // Se rilascio il tasto prima, si resetta tutto
            timeTakingEgg = 0;
            _lockedEggTag = null;
        }
    }

    public void HandleHitByBall(ref string myEgg)
    {
        // La conferma dell'HIT invalida il precedente possesso del remoto.
        // Per un nuovo pickup serve prima uno stato di rete senza WithEgg.
        if (_tag.EndsWith("Red"))
            _remotePickupWaitsForEggRelease = true;

        if (isWithEgg)
        {
            eggPutEvent?.Invoke(this, EventArgs.Empty);
        }
        isFrozen = true;
        timeTakingEgg = 0;
        timePuttingEgg = 0;
        isWithEgg = false;
        myEgg = null;
        _lockedEggTag = null; // Resetta l'uovo target
    }
    
    // ... RESTO DEL CODICE IDENTICO A PRIMA (IsEnemyBall, HandleEggDelivery, ecc.) ...
    
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
            if (timePuttingEgg > EggDeliverySeconds && !deleteEggBool)
            {
                deleteEgg(myEgg);
                deleteEggBool = true;
            }

            // 2. A 1.2 secondi abbiamo finito di inviare il "falso" tasto premuto: resettiamo tutto
            if (timePuttingEgg > EggDeliverySeconds + EggDeliveryCleanupSeconds)
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
    
    public void SyncStateWithNetwork(StateStruct stateStruct, ref string myEgg, bool serverFrozen = false)
    {
        // Se siamo il pinguino remoto e il server dice che NON abbiamo più l'uovo,
        // ma localmente risultiamo ancora "WithEgg", forziamo il reset immediato.
        if (_tag.EndsWith("Red") && !stateStruct.IsPressed(StateList.WithEgg) && isWithEgg
            && !stateStruct.IsPressed(StateList.Freezing) && !serverFrozen)
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
        // Solo il rilascio volontario con E passa da qui.
        // La pallata provoca il drop esclusivamente tramite HandleConfirmedHit.
        if (stateStruct.IsPressed(StateList.WithEgg)
            && stateStruct.JustPressed(StateList.TakingEgg)
            && !stateStruct.IsPressed(StateList.Freezing))
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