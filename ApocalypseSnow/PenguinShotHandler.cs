using System.Runtime.InteropServices;

namespace ApocalypseSnow;

using Microsoft.Xna.Framework;
public class PenguinShotHandler
{
    private readonly Game _gameContext;
    private string _tag;
    private int _ammo;
    public int Ammo{ get => _ammo; set => _ammo = value; }
    private static readonly int FrameReload;
    public float pressedTime;
    public float _reloadTime;

    public PenguinShotHandler(Game gameContext, string tag)
    {
        _gameContext = gameContext;
        _tag = tag;
        _ammo = 100;
    }
    static PenguinShotHandler()
    {
        FrameReload = 3;
    }
    
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parabolic_motion(float gravity, float startPositionX, float startPositionY, 
        out float positionX, out float positionY, float startVelocityX,
        float startVelocityY, float gameTime);
    
    
    public void Reload(StateStruct stateStruct, float deltaTime)
    {
        if (!stateStruct.IsPressed(StateList.Reload)) return;
        _reloadTime += deltaTime;
        
        if (_reloadTime > FrameReload) 
        {
            _ammo++;
            _reloadTime = 0f;
        }
    }
    
    public void ChargeShot(StateStruct stateStruct, float deltaTime)
    {
        if (!stateStruct.IsPressed(StateList.Shoot) || _ammo <= 0) return;
        
        deltaTime *= 100000;
        pressedTime += deltaTime;
        
        if (pressedTime > 200000)
        {
            pressedTime = 200000;
        }
        //Console.WriteLine(pressedTime);
    }
    
    
    
    
    private Vector2 FinalPoint(Vector2 startSpeed, Vector2 startPosition)
    {
        parabolic_motion(100f,
            startPosition.X + 48, 
            startPosition.Y, 
            out float x, out float y,
            startSpeed.X, 
            -startSpeed.Y, 
            0.5f // Il "tempo" finale desiderato
        );
        
        Vector2 pointFinale = new Vector2(x, y);
        return pointFinale;
    }
    
   
    
    public void Shot(StateStruct stateStruct, Vector2 mousePosition,  
        Vector2 position, string tagBall)
    {
        // 1. Verifichiamo se il tasto di sparo è stato rilasciato in questo frame (JustReleased)
        // 2. Verifichiamo se ci sono munizioni
        if (!_tag.EndsWith("Red"))
        {
            if (!stateStruct.JustReleased(StateList.Shoot) || _ammo <= 0) return;
        }
        //Vector2 mousePosition = _movementsManager.GetMousePosition();
    
        
        float differenceX = position.X+48 - mousePosition.X;
        float differenceY = position.Y - mousePosition.Y;
        
        normalizeVelocity(ref differenceX, ref differenceY);
        
        float coX = (differenceX / 150) * (-1);
        Vector2 startSpeed = new Vector2(coX, differenceY / 100) * pressedTime;
        
        Vector2 finalPosition = FinalPoint(startSpeed, position);
        
        //string tagBall =_animationManager._ballTag+ _countBall;
        Ball b = new Ball(_gameContext, _tag, position, startSpeed, finalPosition, tagBall);
        _gameContext.Components.Add(b);
        if (!_tag.EndsWith("Red"))
        {
            ShotStruct shotStruct = new ShotStruct();
            shotStruct.mouseX = (int)mousePosition.X;
            shotStruct.mouseY = (int)mousePosition.Y;
            shotStruct.charge = (int)pressedTime; // Inviamo il tempo di pressione

            // NOTA: Assicurati di avere un riferimento a _networkManager in questa classe, 
            // passandolo magari dal costruttore o tramite una Singleton/GameManager
            NetworkManager.Instance.SendShot(shotStruct);
        }

        pressedTime = 0;
        _ammo--;
    }
}