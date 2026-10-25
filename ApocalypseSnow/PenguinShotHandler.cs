using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class PenguinShotHandler
{
    public readonly Game _gameContext;
    private readonly string _tag;
    private readonly uint _ownerPlayerId;

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
    public readonly record struct ShotLaunch(Vector2 Start, Vector2 GroundVelocity, float VerticalSpeed, float Gravity);

    public PenguinShotHandler(Game gameContext, string tag, uint ownerPlayerId)
    {
        _gameContext = gameContext;
        _tag = tag;
        _ownerPlayerId = ownerPlayerId;
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

    // Durata teorica su terreno piano (delega alla DLL C++)
    public static float PredictFullFlightSeconds(int chargeMs, ShotType type)
    {
        return ShotPhysicsAPI.PredictFullFlightSeconds(
            chargeMs, 
            MaxChargeSeconds, 
            DesiredFullChargeFlightTimeLowShot, 
            type == ShotType.Left ? ThetaLeftDeg : ThetaRightDeg, 
            ThetaLeftDeg
        );
    }

    public static float ComputeGravityForLowShot(float maxWorldRange)
    {
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);
        return ShotPhysicsAPI.ComputeGravityForLowShot(maxWorldRange, DesiredFullChargeFlightTimeLowShot, thetaLowRad);
    }

    private static float ComputeMaxV0(float gravity)
    {
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);
        return ShotPhysicsAPI.ComputeMaxV0(gravity, DesiredFullChargeFlightTimeLowShot, thetaLowRad);
    }

    public static float ComputeVerticalSpeed(float chargeSeconds, ShotType type, float gravity)
    {
        float thetaDeg = type == ShotType.Left ? ThetaLeftDeg : ThetaRightDeg;
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);
        float maxV0 = ComputeMaxV0(gravity);
        return ShotPhysicsAPI.ComputeVerticalSpeed(chargeSeconds, MaxChargeSeconds, maxV0, thetaDeg, ThetaLeftDeg);
    }

    public static float ComputeMaxWorldRange(Vector2 muzzleBase)
    {
        return ShotPhysicsAPI.ComputeMaxWorldRange(muzzleBase, GameWorld.Width, GameWorld.Height);
    }

    // Ricostruisce la stessa traiettoria delegando alla DLL C++
    public static ShotLaunch ComputeLaunch(Vector2 penguinPosition, Vector2 target, float chargeSeconds, ShotType type)
    {
        float thetaDeg = type == ShotType.Left ? ThetaLeftDeg : ThetaRightDeg;
        return ShotPhysicsAPI.ComputeLaunch(
            penguinPosition,
            target,
            chargeSeconds,
            MaxChargeSeconds,
            DesiredFullChargeFlightTimeLowShot,
            thetaDeg,
            ThetaLeftDeg,
            MuzzleOffsetY,
            MuzzleDistance,
            GameWorld.Width,
            GameWorld.Height
        );
    }

    // Per il locale, crea il tiro al rilascio del tasto armato e invia MsgShot.
    // Per il remoto, ricrea soltanto la palla usando i metadati ricevuti dal server.
    public void Shot(StateStruct stateStruct, Vector2 mousePosition, Vector2 position, string tagBall,
        ShotType? remoteShotType = null, ShotKey? remoteShotKey = null)
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

        // Tutta la matematica e' demandata alla DLL C++ tramite ComputeLaunch
        ShotLaunch launch = ComputeLaunch(position, mousePosition, pressedTime, shotTypeToFire.Value);
        _currentVerticalSpeed = launch.VerticalSpeed;

        // L'identita' della palla e' (giocatore, tick di tiro).
        ShotKey shotKey = isRemoteShot
            ? remoteShotKey ?? throw new ArgumentException("Remote shot must carry a ShotKey.", nameof(remoteShotKey))
            : new ShotKey(_ownerPlayerId, GameSession.LocalTick);

        // Creiamo la palla da mostrare localmente
        Ball b = new Ball(_gameContext, _tag, launch.Start, launch.GroundVelocity, launch.VerticalSpeed,
            shotKey.CollisionTag(tagBall), launch.Gravity, shotKey);
        _gameContext.Components.Add(b);
        
        b.MarkObservedStart(GameSession.LocalTick, isRemoteShot ? "REMOTE" : "OWNER");
        if (!isRemoteShot)
        {
            int chargeMs = (int)MathF.Round(pressedTime * 1000f);
            float expected = PredictFullFlightSeconds(chargeMs, shotTypeToFire.Value);
            ShotTrace.Log($"[SHOT_FLIGHT_A] key={shotKey} chargeMs={chargeMs} type={shotTypeToFire.Value} " +
                          $"actualDuration={b.FlightDurationSeconds:F7} theoreticalDuration={expected:F7} " +
                          $"durationDelta={b.FlightDurationSeconds - expected:F7} " +
                          $"flightTicks={ShotFlightTiming.LandingTicks(b.FlightDurationSeconds, GameSession.TickRateHz)}");
        }

        // Solo il proprietario invia MsgShot
        if (!isRemoteShot)
        {
            ShotStruct shotStruct = new ShotStruct
            {
                localTick = shotKey.ShotTick,
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