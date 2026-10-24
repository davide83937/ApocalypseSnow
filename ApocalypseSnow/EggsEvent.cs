using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class EggsEvent:GameComponent
{
    Penguin _myPenguin;
    Penguin _redPenguin;
    public int _myPenguinScore = 0;
    public int _redPenguinScore = 0;
    public List<Egg>  _eggs;

    public EggsEvent(Game game, Penguin myPenguin, Penguin redPenguin) : base(game)
    {
        _myPenguin = myPenguin;
        _redPenguin = redPenguin;
        _eggs = new List<Egg>();
        _myPenguin._penguinColliderHandler.eggTakenEvent += removeEgg;
        _redPenguin._penguinColliderHandler.eggTakenEvent += removeEgg;
        _myPenguin._penguinColliderHandler.eggPutEvent += addEgg;
        _redPenguin._penguinColliderHandler.eggPutEvent += addEgg;
        _myPenguin._penguinColliderHandler.eggDeleteEvent += removeEggCompletaly;
        _redPenguin._penguinColliderHandler.eggDeleteEvent += removeEggCompletaly;
        NetworkManager.Instance.OnEggReceived += HandleEggSpawn;
    }
    
    private void removeEgg(object sender, string tagEgg)
    {
        Console.WriteLine("Removing egg " + tagEgg);
        foreach (Egg egg in _eggs)
        {
            if (egg._tag == tagEgg)
            {
                CollisionManager.Instance.removeObject(egg._tag);
                Game.Components.Remove(egg);
            }
        }
    }
    
    private void removeEggCompletaly(object sender, string tagEgg)
    {
        Egg eggToRemove = null;
        // 1. Cerchiamo l'uovo senza rimuoverlo subito
        foreach (Egg egg in _eggs)
        {
            if (egg._tag == tagEgg)
            {
                eggToRemove = egg;
                break; // Una volta trovato, usciamo dal ciclo
            }
        }

        // 2. Eseguiamo la rimozione fuori dal ciclo foreach
        if (eggToRemove != null)
        {
            // Solo il proprietario comunica al server l'ID della consegna;
            // il remoto non deve inviare un secondo punto per lo stesso uovo.
            if (sender == _myPenguin._penguinColliderHandler &&
                int.TryParse(eggToRemove._tag.Substring(3), out int eggId))
                NetworkManager.Instance.SendEggDelivered(eggId);

            // Rimuoviamo dalla logica delle collisioni
            CollisionManager.Instance.removeObject(eggToRemove._tag);
        
            // Rimuoviamo dai componenti di MonoGame
            Game.Components.Remove(eggToRemove);
        
            // Rimuoviamo dalla nostra lista privata (ORA è SICURO)
            _eggs.Remove(eggToRemove);

            // Il punteggio viene aggiornato solo dal MsgMatchStatus del server.
            // Questo evento locale rimuove esclusivamente lo sprite dell'uovo.
        }
    }

    
    private void addEgg(object sender, EventArgs e)
    {
        // Determiniamo quale pinguino ha lanciato l'evento tramite il suo handler
        Penguin penguin = null;
        if (sender == _myPenguin._penguinColliderHandler) penguin = _myPenguin;
        else if (sender == _redPenguin._penguinColliderHandler) penguin = _redPenguin;

        if (penguin != null)
        {
            ShotTrace.Log($"[EGG_DROP] penguin={penguin._tag} egg={penguin._myEgg ?? "none"} at=({penguin._position.X:F1},{penguin._position.Y:F1})");
            foreach (Egg egg in _eggs)
            {
                if (egg._tag == penguin._myEgg)
                {
                    ShotTrace.Log($"[EGG_DROP] found={egg._tag}");
                    if (!Game.Components.Contains(egg)) Game.Components.Add(egg); // Evita duplicati

                    // Il punto iniziale di rilascio e' vicino al pinguino.
                    // Se compenetra un ostacolo, il risolutore fisico sposta l'uovo
                    // esattamente quanto serve: nessuna lista di posizioni speciali.
                    Vector2 penguinCenter = penguin._position + new Vector2(
                        penguin._penguinInputHandler._animationManager.SourceRect.Width / 2f,
                        penguin._penguinInputHandler._animationManager.SourceRect.Height / 2f);
                    Vector2 eggHalfSize = new(egg._texture.Width / 2f, egg._texture.Height / 2f);
                    egg._position = penguinCenter + new Vector2(40, 60) - eggHalfSize;
                    egg.ResolveObstacleOverlaps();

                    ShotTrace.Log($"[EGG_DROP] placed={egg._tag} at=({egg._position.X:F1},{egg._position.Y:F1})");
                    CollisionManager.Instance.addObject(egg._tag,
                        egg._position.X + eggHalfSize.X, egg._position.Y + eggHalfSize.Y,
                        egg._texture.Width, egg._texture.Height);
                    break; 
                }
            }
        }
    }
    
    private void HandleEggSpawn(int id, float x, float y)
    {
        Vector2 position = new Vector2(x, y);
        Egg egg = new Egg(Game, position, "egg" + id);
        _eggs.Add(egg);
        Game.Components.Add(egg);
    }
    
    public void RemoveAllEggs()
    {
        foreach (var egg in _eggs)
        {
            Game.Components.Remove(egg);
        }
        _eggs.Clear();
    }
    
    
}