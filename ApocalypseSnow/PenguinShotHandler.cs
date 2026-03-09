using System;

namespace ApocalypseSnow;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public class PenguinShotHandler
{
    public readonly Game _gameContext;
    private string _tag;
    private int _ammo;
    public int Ammo { get => _ammo; set => _ammo = value; }
    private static readonly int FrameReload;
    public float pressedTime;
    public float _reloadTime;

    // ===== Parametri tiro =====
    private const float ThetaLeftDeg = 30f;
    private const float ThetaRightDeg = 60f;

    // Charge reale in secondi
    private const float MaxChargeSeconds = 1.5f;

    // Full charge del tiro basso: corner opposto in circa 0.5 secondi
    private const float DesiredFullChargeFlightTimeLowShot = 0.5f;

    // Schiacciamento prospettico leggero del piano sull'asse Y
    private const float PlanePerspectiveY = 0.70f;

    // Punto di uscita del colpo
    private const float MuzzleOffsetY = 18f;
    private const float MuzzleDistance = 22f;

    private MouseState _previousMouseState;
    private MouseState _currentMouseState;

    // Angolo corrente selezionato dal tasto mouse
    private float _currentThetaDeg = ThetaLeftDeg;

    // Componente verticale iniziale del colpo corrente
    private float _currentVerticalSpeed;

    public PenguinShotHandler(Game gameContext, string tag)
    {
        _gameContext = gameContext;
        _tag = tag;
        _ammo = 100;

        _previousMouseState = Mouse.GetState();
        _currentMouseState = _previousMouseState;
    }

    static PenguinShotHandler()
    {
        FrameReload = 3;
    }

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
        // Il remoto non deve accumulare da solo il charge
        if (_tag.EndsWith("Red"))
            return;

        _currentMouseState = Mouse.GetState();

        bool leftDown = _currentMouseState.LeftButton == ButtonState.Pressed;
        bool rightDown = _currentMouseState.RightButton == ButtonState.Pressed;

        if ((!leftDown && !rightDown) || _ammo <= 0)
            return;

        // Il sinistro seleziona il tiro teso, il destro quello arcuato
        _currentThetaDeg = rightDown ? ThetaRightDeg : ThetaLeftDeg;

        // Charge in secondi reali
        pressedTime += deltaTime;

        if (pressedTime > MaxChargeSeconds)
            pressedTime = MaxChargeSeconds;
    }

    private static float ComputeGravityForLowShot(float maxWorldRange)
    {
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);

        // Impongo che il tiro a 30° full charge arrivi al range massimo in T secondi:
        // R = (v0^2 * sin(2theta)) / g
        // T = (2 * v0 * sin(theta)) / g
        // Eliminando v0:
        // g = (2 * R * tan(theta)) / T^2
        return (2f * maxWorldRange * MathF.Tan(thetaLowRad)) /
               (DesiredFullChargeFlightTimeLowShot * DesiredFullChargeFlightTimeLowShot);
    }

    private static float ComputeMaxV0(float gravity)
    {
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);

        // T = (2 * v0 * sin(theta)) / g  =>  v0 = g * T / (2 * sin(theta))
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

    private Vector2 FinalPoint(Vector2 startSpeed, Vector2 startPosition, float gravity)
    {
        float flightTime = (2f * _currentVerticalSpeed) / gravity;
        if (flightTime < 0f) flightTime = 0f;

        float worldDeltaX = startSpeed.X * flightTime;
        float worldDeltaY = startSpeed.Y * flightTime;

        float screenX = startPosition.X + worldDeltaX;
        float screenY = startPosition.Y + (PlanePerspectiveY * worldDeltaY);

        return new Vector2(screenX, screenY);
    }

    public void Shot(StateStruct stateStruct, Vector2 mousePosition, Vector2 position, string tagBall)
    {
        if (!_tag.EndsWith("Red"))
        {
            bool leftReleased =
                _previousMouseState.LeftButton == ButtonState.Pressed &&
                _currentMouseState.LeftButton == ButtonState.Released;

            bool rightReleased =
                _previousMouseState.RightButton == ButtonState.Pressed &&
                _currentMouseState.RightButton == ButtonState.Released;

            if ((!leftReleased && !rightReleased) || _ammo <= 0)
            {
                _previousMouseState = _currentMouseState;
                return;
            }

            _currentThetaDeg = rightReleased ? ThetaRightDeg : ThetaLeftDeg;
        }
        else
        {
            // TEMPORANEO:
            // il remoto non riceve ancora dal protocollo il tipo di arco
            _currentThetaDeg = ThetaLeftDeg;
        }

        if (pressedTime <= 0f)
        {
            _previousMouseState = _currentMouseState;
            return;
        }

        // ===== Punto di uscita del colpo =====
        Vector2 muzzleBase = new Vector2(position.X + 48f, position.Y + MuzzleOffsetY);

        // ===== 1) DIREZIONE SUL PIANO =====
        float differenceX = mousePosition.X - muzzleBase.X;
        float differenceY = mousePosition.Y - muzzleBase.Y;

        // Deproiezione leggera dell'asse Y per ottenere una direzione "world-like"
        differenceY /= PlanePerspectiveY;

        PhysicsAPI.normalizeVelocity(ref differenceX, ref differenceY);

        // ===== 2) POTENZA =====
        float maxWorldRange = ComputeMaxWorldRange(muzzleBase);
        float gravity = ComputeGravityForLowShot(maxWorldRange);
        float maxV0 = ComputeMaxV0(gravity);

        // Charge normalizzato 0..1
        float charge01 = MathHelper.Clamp(pressedTime / MaxChargeSeconds, 0f, 1f);

        // Qui sta la genialata:
        // la gittata diventa molto più lineare rispetto al tempo di carica
        float power01 = MathF.Sqrt(charge01);

        float v0 = power01 * maxV0;

        // ===== 3) ANGOLO FISSO =====
        float thetaRad = MathHelper.ToRadians(_currentThetaDeg);

        // ===== 4) SCOMPOSIZIONE =====
        float groundSpeed = v0 * MathF.Cos(thetaRad);
        _currentVerticalSpeed = v0 * MathF.Sin(thetaRad);

        // Velocità sul piano
        Vector2 startSpeed = new Vector2(differenceX, differenceY) * groundSpeed;

        // Spawn leggermente avanti rispetto alla mano
        Vector2 aimScreenDirection = mousePosition - muzzleBase;
        if (aimScreenDirection.LengthSquared() > 0.0001f)
            aimScreenDirection.Normalize();
        else
            aimScreenDirection = new Vector2(1f, 0f);

        Vector2 spawnPosition = muzzleBase + aimScreenDirection * MuzzleDistance;

        // Debug/tuning
        //Vector2 finalPosition = FinalPoint(startSpeed, spawnPosition, gravity);

        Ball b = new Ball(_gameContext, _tag, spawnPosition, startSpeed, _currentVerticalSpeed, tagBall, gravity);
        _gameContext.Components.Add(b);

        if (!_tag.EndsWith("Red"))
        {
            ShotStruct shotStruct = new ShotStruct();
            shotStruct.mouseX = mousePosition.X;
            shotStruct.mouseY = mousePosition.Y;

            // Inviamo il charge in millisecondi
            shotStruct.charge = (int)(pressedTime * 1000f);

            NetworkManager.Instance.SendShot(shotStruct);
        }

        pressedTime = 0f;
        _ammo--;
        _previousMouseState = _currentMouseState;
    }
}