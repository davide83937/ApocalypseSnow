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
            // Rimuoviamo dalla logica delle collisioni
            CollisionManager.Instance.removeObject(eggToRemove._tag);
        
            // Rimuoviamo dai componenti di MonoGame
            Game.Components.Remove(eggToRemove);
        
            // Rimuoviamo dalla nostra lista privata (ORA è SICURO)
            _eggs.Remove(eggToRemove);

            // 3. Aggiorniamo il punteggio
            Penguin penguin = null;
            if (sender == _myPenguin._penguinColliderHandler) penguin = _myPenguin;
            else if (sender == _redPenguin._penguinColliderHandler) penguin = _redPenguin;

            if (penguin is { _tag: "penguin" }) 
            { _myPenguinScore++; }
            else
            {_redPenguinScore++;}
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
            foreach (Egg egg in _eggs)
            {
                if (egg._tag == penguin._myEgg)
                {
                    if (!Game.Components.Contains(egg)) Game.Components.Add(egg); // Evita duplicati
                    egg._position.X = penguin._position.X + 48;
                    egg._position.Y = penguin._position.Y + 100;
                
                    CollisionManager.Instance.addObject(egg._tag, egg._position.X, egg._position.Y, 
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