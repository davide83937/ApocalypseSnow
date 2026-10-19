using System;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class PenguinShotHandler
{
    public readonly Game _gameContext;
    private readonly string _tag;

    private int _ammo;
    public int Ammo { get => _ammo; set => _ammo = value; }

    private const float ReloadDurationSeconds = 3f;

    // Charge locale canonico in secondi
    public float pressedTime;
    public float _reloadTime;

    // ===== Parametri tiro =====
    private const float ThetaLeftDeg = 30f;
    private const float ThetaRightDeg = 60f;
    private const float MaxChargeSeconds = 1.5f;
    private const float DesiredFullChargeFlightTimeLowShot = 0.5f;
    private const float PlanePerspectiveY = 0.70f;
    private const float MuzzleOffsetY = 18f;
    private const float MuzzleDistance = 22f;

    private float _currentThetaDeg = ThetaLeftDeg;
    private float _currentVerticalSpeed;

    // Tipo di carica “armato” dall’input unificato.
    // Non legge più il mouse direttamente.
    private ShotType? _armedShotType = null;

    public PenguinShotHandler(Game gameContext, string tag)
    {
        _gameContext = gameContext;
        _tag = tag;
        _ammo = 100;
    }

    public void Reload(StateStruct stateStruct, float deltaTime)
    {
        if (!stateStruct.IsPressed(StateList.Reload))
            return;

        _reloadTime += deltaTime;

        if (_reloadTime >= ReloadDurationSeconds)
        {
            _ammo++;
            _reloadTime = 0f;
        }
    }

    public void ChargeShot(StateStruct stateStruct, float deltaTime)
    {
        // Il remoto non accumula charge locale.
        if (_tag.EndsWith("Red"))
            return;

        bool isChargingLeft = stateStruct.IsPressed(StateList.ShootLeft);
        bool isChargingRight = stateStruct.IsPressed(StateList.ShootRight);

        bool canShoot =
            _gameContext.IsActive &&
            !stateStruct.IsPressed(StateList.Freezing) &&
            !stateStruct.IsPressed(StateList.WithEgg) &&
            !stateStruct.IsPressed(StateList.Reload) &&
            _ammo > 0;

        if (!canShoot)
        {
            ResetCharge();
            return;
        }

        // Se in questo tick il manager sta emettendo il bit di shoot,
        // lo shot handler si limita a consumarlo.
        if (isChargingLeft)
        {
            _armedShotType = ShotType.Left;
            _currentThetaDeg = ThetaLeftDeg;
            pressedTime += deltaTime;
        }
        else if (isChargingRight)
        {
            _armedShotType = ShotType.Right;
            _currentThetaDeg = ThetaRightDeg;
            pressedTime += deltaTime;
        }

        if (pressedTime > MaxChargeSeconds)
            pressedTime = MaxChargeSeconds;
    }

    private void ResetCharge()
    {
        pressedTime = 0f;
        _armedShotType = null;
    }

    private static float ComputeGravityForLowShot(float maxWorldRange)
    {
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);
        // g = (2*R*tan(theta))/T^2 
        return (2f * maxWorldRange * MathF.Tan(thetaLowRad)) /
               (DesiredFullChargeFlightTimeLowShot * DesiredFullChargeFlightTimeLowShot);
    }

    private static float ComputeMaxV0(float gravity)
    {
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);
        // v0 = g*T/2*sin(theta)
        return (gravity * DesiredFullChargeFlightTimeLowShot) /
               (2f * MathF.Sin(thetaLowRad));
    }

    private float ComputeMaxWorldRange(Vector2 muzzleBase)
    {
        var vp = _gameContext.GraphicsDevice.Viewport;

        Vector2[] corners =
        {
            new Vector2(0f, 0f),
            new Vector2(vp.Width, 0f),
            new Vector2(0f, vp.Height),
            new Vector2(vp.Width, vp.Height)
        };

        float maxWorldRangeSq = 0f;

        foreach (var corner in corners)
        {
            float dx = corner.X - muzzleBase.X;
            float dyWorld = (corner.Y - muzzleBase.Y) / PlanePerspectiveY;

            float distSq = dx * dx + dyWorld * dyWorld;
            if (distSq > maxWorldRangeSq)
                maxWorldRangeSq = distSq;
        }
        return MathF.Sqrt(maxWorldRangeSq);
    }

    public void Shot(StateStruct stateStruct, Vector2 mousePosition, Vector2 position, string tagBall, ShotType? remoteShotType = null)
    {
        bool isRemoteShot = _tag.EndsWith("Red");
        ShotType? shotTypeToFire = null;

        if (!isRemoteShot)
        {
            if (_ammo <= 0)
            {
                ResetCharge();
                return;
            }

            bool leftReleased = stateStruct.JustReleased(StateList.ShootLeft);
            bool rightReleased = stateStruct.JustReleased(StateList.ShootRight);

            // Se nessuno ha rilasciato, oppure non ho accumulato carica,
            // non devo sparare in questo frame.
            if ((!leftReleased && !rightReleased) || pressedTime <= 0f)
            {
                return;
            }

            // Il rilascio deve essere coerente con il tipo armato durante la carica.
            if (leftReleased && _armedShotType == ShotType.Left)
            {
                shotTypeToFire = ShotType.Left;
            }
            else if (rightReleased && _armedShotType == ShotType.Right)
            {
                shotTypeToFire = ShotType.Right;
            }
            else
            {
                // Caso anomalo: rilascio non coerente con il colpo armato.
                ResetCharge();
                return;
            }
        }
        else
        {
            shotTypeToFire = remoteShotType ?? ShotType.Left;

            if (pressedTime <= 0f)
            {
                _armedShotType = null;
                return;
            }
        }

        if (shotTypeToFire == ShotType.Left)
            _currentThetaDeg = ThetaLeftDeg;
        else
            _currentThetaDeg = ThetaRightDeg;

        Vector2 muzzleBase = new Vector2(position.X + 48f, position.Y + MuzzleOffsetY); 
       /*
       Siccome il gioco ha una visuale dall'alto ma inclinata,
       il mondo a schermo è "schiacciato" sull'asse Y (definito da PlanePerspectiveY = 0.70f).
       Se miri in diagonale, la distanza a schermo non corrisponde alla distanza nel mondo di gioco.
       Dividendo differenceY per 0.70f, il codice "stira" virtualmente il mondo facendolo tornare perfettamente quadrato,
       calcola la direzione corretta verso cui il mouse sta puntando nel mondo reale 3D,
       e infine normalizza il vettore (lo fa diventare di lunghezza 1, cioè una pura "direzione" senza forza).
       */
        float differenceX = mousePosition.X - muzzleBase.X;
        float differenceY = mousePosition.Y - muzzleBase.Y;
        differenceY /= PlanePerspectiveY;
        PhysicsAPI.normalizeVelocity(ref differenceX, ref differenceY);

        float maxWorldRange = ComputeMaxWorldRange(muzzleBase);
        float gravity = ComputeGravityForLowShot(maxWorldRange);
        float maxV0 = ComputeMaxV0(gravity);

        /*
         Trasforma il tempo di pressione in una percentuale da 0 a 1 (charge01).
         Usa la radice quadrata (MathF.Sqrt) per calcolare la potenza (power01). 
         Questo è un trucco classico di game design: crea una curva non lineare. 
         Significa che basta tenere premuto poco per avere subito un tiro decente (es. al 25% di tempo hai già il 50% della potenza), 
         ma per arrivare al 100% devi aspettare tutto il tempo. Rende i tiri rapidi molto più responsivi.
         Moltiplica questa percentuale per la velocità massima calcolata dalle tre funzioni che abbiamo visto prima, 
         ottenendo la velocità iniziale reale (v0).
         */
        float charge01 = MathHelper.Clamp(pressedTime / MaxChargeSeconds, 0f, 1f);
        float power01 = MathF.Sqrt(charge01);
        float v0 = power01 * maxV0;

        /*
         groundSpeed (Sull'asse orizzontale/terreno): Usa il Coseno dell'angolo. 
         Moltiplicato per la direzione normalizzata di prima, ti dice quanto velocemente la palla si sposterà sulla mappa.
         _currentVerticalSpeed (Sull'asse Z / altezza): Usa il Seno dell'angolo. 
         Questa è la velocità con cui la palla si "staccherà" da terra verso lo schermo.
         */
        float thetaRad = MathHelper.ToRadians(_currentThetaDeg);
        float groundSpeed = v0 * MathF.Cos(thetaRad);
        _currentVerticalSpeed = v0 * MathF.Sin(thetaRad);

        Vector2 startSpeed = new Vector2(differenceX, differenceY) * groundSpeed;

        /*
         Per evitare che il proiettile compaia esattamente dentro o dietro il personaggio 
         (rischiando di colpirlo istantaneamente o di sembrare brutto visivamente), 
         calcola un punto di spawn spostato in avanti di MuzzleDistance (22 pixel) in direzione del mouse.
         */
        Vector2 aimScreenDirection = mousePosition - muzzleBase;
        if (aimScreenDirection.LengthSquared() > 0.0001f)
            aimScreenDirection.Normalize();
        else
            aimScreenDirection = new Vector2(1f, 0f);
        Vector2 spawnPosition = muzzleBase + aimScreenDirection * MuzzleDistance;

        //Generazione palla e invio info sul tiro
        
        Ball b = new Ball(_gameContext, _tag, spawnPosition, startSpeed, _currentVerticalSpeed, tagBall, gravity);
        _gameContext.Components.Add(b);

        if (!isRemoteShot)
        {
            ShotStruct shotStruct = new ShotStruct
            {
                mouseX = mousePosition.X,
                mouseY = mousePosition.Y,
                charge = (int)MathF.Round(pressedTime * 1000f)
            };

            NetworkManager.Instance.SendShot(shotStruct);
        }

        pressedTime = 0f;
        _ammo--;
        _armedShotType = null;
    }
}