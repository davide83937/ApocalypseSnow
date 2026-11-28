using System;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class PenguinShotHandler
{
    public readonly Game _gameContext;
    private readonly string _tag;
    private readonly uint _ownerPlayerId;

    // Regole del lancio, mantenute coerenti con Server/shot_gameplay.go.
    private const float HandOffsetX = 48f;
    private const float HandOffsetY = 18f;
    private const float BallSpawnDistance = 22f;
    private const float PlanePerspectiveY = 0.70f;
    private const float MaxChargeSeconds = 1.5f;
    private const float MaxInitialSpeed = 1900f;
    private const float ShotGravity = 3800f;
    private const float MinDirectionLengthSquared = 0.0001f;

    private int _ammo;
    public int Ammo { get => _ammo; set => _ammo = value; }

    private const float ReloadDurationSeconds = 3f;

    // Charge locale canonico in secondi
    public float pressedTime;
    public float _reloadTime;

    // La durata massima della carica e' una regola di gioco.


    // Tipo di carica “armato” dall’input unificato.
    // Non legge più il mouse direttamente.
    private ShotType? _armedShotType = null;

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
            pressedTime += deltaTime;
        }
        else if (isChargingRight)
        {
            _armedShotType = ShotType.Right;
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

    public readonly record struct ShotParameters(Vector2 Start, Vector2 GroundVelocity, float VerticalSpeed, float Gravity);

    // Le regole di gioco determinano il lancio; la DLL C++ calcola la fisica.
    // Tenere le stesse formule e costanti di Server/shot_gameplay.go.
    public static ShotParameters ComputeShotParameters(Vector2 penguinPosition, Vector2 target, float chargeSeconds, ShotType type)
    {
        Vector2 handPosition = new(penguinPosition.X + HandOffsetX, penguinPosition.Y + HandOffsetY);
        Vector2 screenDelta = target - handPosition;

        float lenSq = screenDelta.X * screenDelta.X + screenDelta.Y * screenDelta.Y;
        Vector2 screenDirection = lenSq <= MinDirectionLengthSquared
            ? new Vector2(1f, 0f)
            : screenDelta * (1f / MathF.Sqrt(lenSq));

        Vector2 origin = handPosition + screenDirection * BallSpawnDistance;
        Vector2 worldDirection = new(screenDelta.X, screenDelta.Y / PlanePerspectiveY);
        float power = MathF.Sqrt(Math.Clamp(chargeSeconds / MaxChargeSeconds, 0f, 1f));
        float speed = power * MaxInitialSpeed;
        float angleDegrees = type == ShotType.Right ? 60f : 30f;

        return ShotPhysicsAPI.ComputeShotParameters(origin, worldDirection, speed, angleDegrees, ShotGravity);
    }

    // Per il locale, crea il tiro al rilascio del tasto armato e invia MsgShot.
    // Per il remoto, ricrea soltanto la palla usando i metadati ricevuti dal server.
    // La posizione e il freeze definitivi restano autoritativi; l'effetto della
    // palla (impatto visivo o dissolvenza) rimane esclusivamente locale.
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

        // La logica di gioco decide origine e parametri; C++ calcola la fisica.
        ShotParameters launch = ComputeShotParameters(position, mousePosition, pressedTime, shotTypeToFire.Value);

        // L'identita' della palla e' (giocatore, tick di tiro). Locale e server
        // devono riferirsi alla stessa chiave per collegare tiro ed esito.
        // Il remoto conserva la chiave originale; non ne genera una nuova.
        ShotKey shotKey = isRemoteShot
            ? remoteShotKey ?? throw new ArgumentException("Remote shot must carry a ShotKey.", nameof(remoteShotKey))
            : new ShotKey(_ownerPlayerId, GameSession.LocalTick);

        // Creiamo la palla da mostrare localmente e registriamo quando inizia
        // il suo volo su questa timeline, anche se proviene dall'avversario.
        Ball b = new Ball(_gameContext, _tag, launch.Start, launch.GroundVelocity, launch.VerticalSpeed,
            shotKey.CollisionTag(tagBall), launch.Gravity, shotKey);
        _gameContext.Components.Add(b);
        
        b.MarkObservedStart(GameSession.LocalTick);
        // Solo il proprietario invia MsgShot: il remoto sta gia' riproducendo
        // un tiro che il server ha ricevuto e autorizzato.
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