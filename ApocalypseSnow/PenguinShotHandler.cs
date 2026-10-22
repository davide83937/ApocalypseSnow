﻿using System;
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

    // Durata teorica su terreno piano, con la stessa carica e gli stessi angoli
    // usati dal tiro reale. Indipendente dalla dimensione della viewport.
    public static float PredictFullFlightSeconds(int chargeMs, ShotType type)
    {
        float chargeSeconds = MathHelper.Clamp(chargeMs / 1000f, 0f, MaxChargeSeconds);
        float theta = MathHelper.ToRadians(type == ShotType.Left ? ThetaLeftDeg : ThetaRightDeg);
        float thetaLow = MathHelper.ToRadians(ThetaLeftDeg);
        
        //FORMULA TEMPO DI VOLO RICAVATA DAL MOTO DEL PROIETTILE(portare in c++)
        return DesiredFullChargeFlightTimeLowShot * MathF.Sqrt(chargeSeconds / MaxChargeSeconds) *
               MathF.Sin(theta) / MathF.Sin(thetaLow);
    }

    public static float ComputeGravityForLowShot(float maxWorldRange)
    {
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);
        //calcola il valore esatto della costante di accelerazione di
        //gravità g affinché un proiettile a piena carica copra l'intera
        //distanza della mappa
        return (2f * maxWorldRange * MathF.Tan(thetaLowRad)) /
               (DesiredFullChargeFlightTimeLowShot * DesiredFullChargeFlightTimeLowShot);
    }

    private static float ComputeMaxV0(float gravity)
    {
        /*
        Questa formula calcola il modulo della velocità iniziale
        massima (v(0,max)) con cui deve partire la palla a piena 
        carica per rispettare il tempo di volo prefissato di 
        0.5 secondi con l'alzo basso di 30°.   
        La derivazione algebrica discende direttamente dalla 
        cinematica del moto verticale uniformemente accelerato.
        */
        
        float thetaLowRad = MathHelper.ToRadians(ThetaLeftDeg);

        return (gravity * DesiredFullChargeFlightTimeLowShot) /
               (2f * MathF.Sin(thetaLowRad));
    }

    public static float ComputeVerticalSpeed(float chargeSeconds, ShotType type, float gravity)
    {
        /*
         Questa funzione calcola la componente verticale 
         iniziale della velocità (v_0z o v_z), ossia la 
         spinta verso l'alto lungo l'asse virtuale della 
         quota Z nel mondo 2.5D
         */
        float charge01 = MathHelper.Clamp(chargeSeconds / MaxChargeSeconds, 0f, 1f);
        float power01 = MathF.Sqrt(charge01);
        float v0 = power01 * ComputeMaxV0(gravity);
        float thetaDeg = type == ShotType.Left ? ThetaLeftDeg : ThetaRightDeg;
        return v0 * MathF.Sin(MathHelper.ToRadians(thetaDeg));
    }

    // La gittata massima usa gli angoli del mondo di gioco (dimensioni logiche),
    // non il viewport del singolo client. Riportiamo la Y allo spazio del terreno.
    public static float ComputeMaxWorldRange(Vector2 muzzleBase)
    {
        Vector2[] corners =
        {
            new Vector2(0f, 0f),
            new Vector2(GameWorld.Width, 0f),
            new Vector2(0f, GameWorld.Height),
            new Vector2(GameWorld.Width, GameWorld.Height)
        };

        float maxWorldRangeSq = 0f;

        foreach (var corner in corners)
        {
            /*
             Per ciascun angolo, calcola la distanza 
             orizzontale e verticale rispetto alla canna 
             del pinguino
             */
            //Sull'asse orizzontale $X$, lo spazio è in scala 1:1,
            //quindi si fa una semplice differenza
            float dx = corner.X - muzzleBase.X;
            //Sull'asse verticale $Y$, la visuale del gioco è
            //inclinata/isometrica (2.5D)
            //Dividendo per 0.70, il codice "stira" virtualmente
            //la coordinata verticale, ripristinando la distanza
            //metrica reale non deformata sul piano del terreno
            float dyWorld = (corner.Y - muzzleBase.Y) / PlanePerspectiveY;
            //Per calcolare la distanza in linea d'aria
            //(ipotenusa del triangolo rettangolo) si applica
            //il Teorema di Pitagora:distanza^2 = dx^2 + dy_{world}^2
            float distSq = dx * dx + dyWorld * dyWorld;
            //Siccome la radice quadrata è pesante per la CPU
            //confrontiamo direttamente i quadrati
            if (distSq > maxWorldRangeSq)
                maxWorldRangeSq = distSq;
        }
        //Una volta individuato qual è l'angolo con il
        //quadrato della distanza maggiore (maxWorldRangeSq),
        //esegue una singola radice quadrata finale
        return MathF.Sqrt(maxWorldRangeSq); 
        //Il valore restituito è la gittata massima reale
        //R_{max}, che viene poi passata a
        //ComputeGravityForLowShot per calibrare
        //l'accelerazione di gravità su misura per la mappa.   
    }

    public readonly record struct ShotLaunch(Vector2 Start, Vector2 GroundVelocity, float VerticalSpeed, float Gravity);

    // Ricostruisce la stessa traiettoria per il tiro locale e quello remoto.
    // I parametri sono verificati anche dai test di confronto con il server Go.
    public static ShotLaunch ComputeLaunch(Vector2 penguinPosition, Vector2 target, float chargeSeconds, ShotType type)
{
    Vector2 muzzleBase = new(penguinPosition.X + 48f, penguinPosition.Y + MuzzleOffsetY);
    
    // 1. Salviamo subito il vettore direzione puro a schermo (pixel del monitor)
    Vector2 screenDelta = target - muzzleBase;

    // Lo schermo comprime il piano di gioco lungo Y (fattore 0,70).
    // Compensiamo prima di normalizzare, cosi' la direzione sul terreno
    // non dipende dalla distorsione prospettica.
    float differenceX = screenDelta.X;
    float differenceY = screenDelta.Y / PlanePerspectiveY;
    PhysicsAPI.normalizeVelocity(ref differenceX, ref differenceY);

    // Taratura della parabola: la distanza massima del mondo definisce
    // la gravita' e la velocita' massima del tiro basso a piena carica.
    float gravity = ComputeGravityForLowShot(ComputeMaxWorldRange(muzzleBase));
    float maxV0 = ComputeMaxV0(gravity);
    
    // Carica normalizzata tra 0 e 1. La radice quadrata fa crescere
    // la potenza rapidamente all'inizio (25% di carica -> 50% di velocita')
    // senza superare la velocita' massima.
    float charge01 = MathHelper.Clamp(chargeSeconds / MaxChargeSeconds, 0f, 1f);
    float v0 = MathF.Sqrt(charge01) * maxV0;
    float thetaDeg = type == ShotType.Left ? ThetaLeftDeg : ThetaRightDeg;
    float thetaRad = MathHelper.ToRadians(thetaDeg);
    
    // Il coseno proietta v0 sul terreno (direzione del mouse).
    // Il seno, in ComputeVerticalSpeed, ricava la velocita' verticale Z:
    // il tiro alto sacrifica velocita' orizzontale per aumentare l'altezza.
    Vector2 velocity = new(differenceX, differenceY);
    velocity *= v0 * MathF.Cos(thetaRad);
    float vz = ComputeVerticalSpeed(chargeSeconds, type, gravity);

    // Il punto di spawn e' spostato di MuzzleDistance verso il mouse
    // nello spazio dello schermo, per non far partire la palla dal centro del pinguino. 
    // Riutilizziamo screenDelta senza dover ricalcolare la sottrazione.
    //La soglia 0.0001f e il fallback (1f, 0f) servono unicamente a prevenire un errore
    //di divisione per zero se il cursore del mouse si trova esattamente sopra le coordinate della canna
    Vector2 aimScreenDirection = screenDelta;
    if (aimScreenDirection.LengthSquared() > 0.0001f)
        aimScreenDirection.Normalize();
    else
        aimScreenDirection = new Vector2(1f, 0f);
        
    Vector2 start = muzzleBase + aimScreenDirection * MuzzleDistance;
    
    return new ShotLaunch(start, velocity, vz, gravity);
}

    // Per il locale, crea il tiro al rilascio del tasto armato e invia MsgShot.
    // Per il remoto, ricrea soltanto la palla usando i metadati ricevuti dal server.
    // La posizione e il freeze definitivi restano autoritativi; l'effetto della
    // palla (HIT predetto o dissolvenza su MISS) rimane esclusivamente locale.
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

        // Tutta la matematica (prospettiva, carica, angolo e punto di spawn)
        // e' concentrata in ComputeLaunch, condiviso dai due tipi di tiro.
        ShotLaunch launch = ComputeLaunch(position, mousePosition, pressedTime, shotTypeToFire.Value);
        _currentVerticalSpeed = launch.VerticalSpeed;

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