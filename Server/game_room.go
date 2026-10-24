package main

import (
	"encoding/binary"
	"fmt"
	"log"
	"math"
	"math/rand"
	"time"
)

const (
	shotThetaLeftDeg                   = 30.0
	shotThetaRightDeg                  = 60.0
	shotMaxChargeSeconds               = 1.5
	shotDesiredFullChargeFlightTimeLow = 0.5

	shotPlanePerspectiveY              = 0.70
	shotMuzzleOffsetY                  = 18.0
	shotMuzzleDistance                 = 22.0
	shotColliderDisableHeightThreshold = 30.0

	shotColliderWidth  int32 = 20
	shotColliderHeight int32 = 25

	// Dimensioni logiche condivise con ApocalypseSnow/GameWorld.cs.
	gameWorldWidth  float32 = 800
	gameWorldHeight float32 = 480
)

// GameRoom rappresenta una partita 1v1 lato server.
type GameRoom struct {
	firstPlayerConnection  *PlayerConnection
	secondPlayerConnection *PlayerConnection

	firstPlayer  Player
	secondPlayer Player

	firstPlayerPendingEvents  []PlayerEvent
	secondPlayerPendingEvents []PlayerEvent

	Obstacles []Position

	collisionManager *CollisionManager

	activeShots []Shot
	nextShotID  uint32

	// Il server conta le consegne per ID, una sola volta per uovo.
	basePositions [2]Position
	totalEggs     int
	deliveredEggs map[int32]bool
	scores        [2]uint32
	matchFinished bool

	tickRateHz uint32
	matchTick  uint32
}

func clamp01(v float32) float32 {
	if v < 0 {
		return 0
	}
	if v > 1 {
		return 1
	}
	return v
}

func normalize2D(x, y float32) (float32, float32) {
	lenSq := x*x + y*y
	if lenSq <= 0.0001 {
		return 1, 0
	}
	invLen := float32(1.0 / math.Sqrt(float64(lenSq)))
	return x * invLen, y * invLen
}

// Stessa tolleranza in tick del client C#: evita +1 tick per rumore float.
func shotLandingTicks(durationSeconds float32, hz uint32) uint32 {
	return uint32(math.Ceil(float64(durationSeconds)*float64(hz) - 0.0001))
}

func shotCollisionTag(shotID uint32) string {
	return fmt.Sprintf("s%d", shotID)
}

func obstacleCollisionTag(index int) string {
	return fmt.Sprintf("o%d", index)
}

func isShotTag(tag string) bool {
	return len(tag) > 0 && tag[0] == 's'
}

func isObstacleTag(tag string) bool {
	return len(tag) > 0 && tag[0] == 'o'
}

func isPenguinTag(tag string) bool {
	return tag == playerCollisionTag(1) || tag == playerCollisionTag(2)
}

func findShotByTag(tag string, shots []Shot) (*Shot, bool) {
	if !isShotTag(tag) {
		return nil, false
	}

	var shotID uint32
	_, err := fmt.Sscanf(tag, "s%d", &shotID)
	if err != nil {
		return nil, false
	}

	for i := range shots {
		if shots[i].ShotID == shotID {
			return &shots[i], true
		}
	}

	return nil, false
}

// NewGameRoom crea una nuova stanza di gioco.
func NewGameRoom(firstPlayerConnection, secondPlayerConnection *PlayerConnection) *GameRoom {
	gameRoom := &GameRoom{
		firstPlayerConnection:     firstPlayerConnection,
		secondPlayerConnection:    secondPlayerConnection,
		tickRateHz:                uint32(MoveHz),
		firstPlayerPendingEvents:  make([]PlayerEvent, 0, 64),
		secondPlayerPendingEvents: make([]PlayerEvent, 0, 64),

		activeShots:      make([]Shot, 0, 64),
		nextShotID:       1,
		deliveredEggs:    make(map[int32]bool),
		collisionManager: NewCollisionManager(),

		Obstacles: []Position{
			{X: 100, Y: 100},
			{X: 500, Y: 200},
		},

		firstPlayer: Player{
			ID:       1,
			Position: Position{X: 100, Y: 300},
			LastMask: 0,
			LastSeqN: 0,
			RecvMask: 0,
			RecvSeqN: 0,
			LastRx:   time.Time{},
		},
		secondPlayer: Player{
			ID:       2,
			Position: Position{X: 550, Y: 25},
			LastMask: 0,
			LastSeqN: 0,
			RecvMask: 0,
			RecvSeqN: 0,
			LastRx:   time.Time{},
		},
	}

	gameRoom.basePositions = [2]Position{gameRoom.firstPlayer.Position, gameRoom.secondPlayer.Position}

	// Collider persistenti dei player
	gameRoom.collisionManager.AddObject(
		playerCollisionTag(gameRoom.firstPlayer.ID),
		gameRoom.firstPlayer.X+penguinHalfTextureFractionWidth,
		gameRoom.firstPlayer.Y+penguinHalfTextureFractionHeight,
		penguinTextureFractionWidth,
		penguinHalfTextureFractionHeight,
	)

	gameRoom.collisionManager.AddObject(
		playerCollisionTag(gameRoom.secondPlayer.ID),
		gameRoom.secondPlayer.X+penguinHalfTextureFractionWidth,
		gameRoom.secondPlayer.Y+penguinHalfTextureFractionHeight,
		penguinTextureFractionWidth,
		penguinHalfTextureFractionHeight,
	)

	// Collider persistenti degli ostacoli, con tag univoco
	for i, obs := range gameRoom.Obstacles {
		posCollX := obs.X + obstacleHalfTextureFractionWidth
		posCollY := obs.Y + obstacleHalfTextureFractionHeight

		gameRoom.collisionManager.AddObject(
			obstacleCollisionTag(i),
			posCollX,
			posCollY+obstacleColliderYOffset,
			obstacleHalfTextureFractionWidth+obstacleColliderExtraWidth,
			obstacleHalfTextureFractionHeight,
		)
	}

	return gameRoom
}

// Run è il loop principale della room.
func (gameRoom *GameRoom) Run() {
	gameRoom.matchTick = 0

	gameRoom.SendJoinAcknowledgements()
	gameRoom.CreateInitialEggs(5)
	gameRoom.sendSpawnObstacleToBothPlayers()

	tickDuration := time.Second / time.Duration(gameRoom.tickRateHz)
	ticker := time.NewTicker(tickDuration)
	defer ticker.Stop()

	lastTime := time.Now()
	var accumulator time.Duration

	for {
		select {
		case <-gameRoom.firstPlayerConnection.DisconnectChannel:
			log.Println("GameRoom: il giocatore 1 si è disconnesso. Chiudo la stanza.")
			gameRoom.secondPlayerConnection.CloseConnection()
			return

		case <-gameRoom.secondPlayerConnection.DisconnectChannel:
			log.Println("GameRoom: il giocatore 2 si è disconnesso. Chiudo la stanza.")
			gameRoom.firstPlayerConnection.CloseConnection()
			return

		case <-ticker.C:
			now := time.Now()
			elapsed := now.Sub(lastTime)
			lastTime = now

			accumulator += elapsed

			for accumulator >= tickDuration {
				gameRoom.Tick()
				accumulator -= tickDuration
			}
		}
	}
}

// Tick esegue un singolo passo logico del gioco.
func (gameRoom *GameRoom) Tick() {
	if gameRoom.matchFinished {
		return
	}
	gameRoom.matchTick++
	deltaTime := float32(1.0 / float32(gameRoom.tickRateHz))
	now := time.Now()

	// PLAYER 1
	gameRoom.drainEventChannelToPending(
		gameRoom.firstPlayerConnection,
		&gameRoom.firstPlayer,
		&gameRoom.firstPlayerPendingEvents,
		now,
	)

	gameRoom.applyMaturePendingEvents(
		&gameRoom.firstPlayer,
		&gameRoom.firstPlayerPendingEvents,
		gameRoom.secondPlayerConnection,
		deltaTime,
	)

	// PLAYER 2
	gameRoom.drainEventChannelToPending(
		gameRoom.secondPlayerConnection,
		&gameRoom.secondPlayer,
		&gameRoom.secondPlayerPendingEvents,
		now,
	)

	gameRoom.applyMaturePendingEvents(
		&gameRoom.secondPlayer,
		&gameRoom.secondPlayerPendingEvents,
		gameRoom.firstPlayerConnection,
		deltaTime,
	)

	// Sync collider server-side
	gameRoom.updatePlayerColliders()
	gameRoom.updateShotColliders()
	gameRoom.collisionManager.SendToCpp()
	gameRoom.resolveCollisionResults()

	// Lo stato remoto riflette il freeze server anche senza nuovi input.
	for _, player := range []*Player{&gameRoom.firstPlayer, &gameRoom.secondPlayer} {
		if gameRoom.isServerFrozen(player) {
			player.LastMask |= Freezing
		} else {
			player.LastMask &^= Freezing
		}
	}

	// Broadcast stato
	gameRoom.sendAuthStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.firstPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.firstPlayerConnection, gameRoom.secondPlayer)

	gameRoom.sendAuthStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.secondPlayer)
	gameRoom.sendRemoteStateToPlayer(gameRoom.secondPlayerConnection, gameRoom.firstPlayer)
}

// drainEventChannelToPending sposta eventi dalla channel di rete alla coda pending del player.
func (gameRoom *GameRoom) drainEventChannelToPending(
	playerConnection *PlayerConnection,
	player *Player,
	pending *[]PlayerEvent,
	now time.Time,
) (drain int, inserted int, droppedOld int) {
	for n := 0; n < maxDrainInputsPerTick; n++ {
		select {
		case event := <-playerConnection.EventChannel:
			drain++

			if event.Type == EventInput {
				player.LastRx = now
				player.RecvMask = event.Input.Mask
				player.RecvSeqN = event.Input.SeqN

				if event.Input.SeqN <= player.LastSeqN {
					droppedOld++
					continue
				}
			}

			*pending = append(*pending, event)
			inserted++

		default:
			return
		}
	}
	return
}

// updatePendingShot implementa la FSM del tiro lato server.
func (gameRoom *GameRoom) updatePendingShot(player *Player, mask int32, seqN uint32) {
	leftNow := (mask & ShootLeft) != 0
	rightNow := (mask & ShootRight) != 0

	leftPrev := (player.LastMask & ShootLeft) != 0
	rightPrev := (player.LastMask & ShootRight) != 0

	leftJustPressed := leftNow && !leftPrev
	rightJustPressed := rightNow && !rightPrev

	ps := &player.PendingShot

	// Se arriva Reload, annulliamo anche il tiro in attesa del pacchetto.
	if (mask & Reload) != 0 {
		if ps.Active {
			log.Printf("[SHOT CANCEL] owner=%d press=%d reloadTick=%d", player.ID, ps.PressTick, seqN)
			*ps = PendingShot{WaitingNeutral: leftNow || rightNow}
		} else if ps.WaitingNeutral && !leftNow && !rightNow {
			ps.WaitingNeutral = false
		}
		return
	}

	if ps.WaitingNeutral {
		if !leftNow && !rightNow {
			ps.WaitingNeutral = false
		}
		return
	}

	// Un rilascio annullato dal client (freeze predetto, perdita del focus, ecc.)
	// puo' non produrre MsgShot: una nuova pressione avvia una nuova carica.
	if ps.Active && ps.WaitingShotPacket && (leftJustPressed || rightJustPressed) {
		log.Printf("[SHOT ABANDONED] owner=%d releaseTick=%d newPressTick=%d", player.ID, ps.ReleaseTick, seqN)
		*ps = PendingShot{}
	}

	if !ps.Active {
		if leftJustPressed {
			ps.Active = true
			ps.Mode = ChargeLeft
			ps.PressTick = seqN
			ps.ReleaseTick = 0
			ps.WaitingShotPacket = false
			ps.ShotPacket = nil

			log.Printf("[SHOT PRESS] owner=%d mode=LEFT pressedTick=%d", player.ID, seqN)
			return
		}

		if rightJustPressed {
			ps.Active = true
			ps.Mode = ChargeRight
			ps.PressTick = seqN
			ps.ReleaseTick = 0
			ps.WaitingShotPacket = false
			ps.ShotPacket = nil

			log.Printf("[SHOT PRESS] owner=%d mode=RIGHT pressedTick=%d", player.ID, seqN)
			return
		}

		return
	}

	if ps.WaitingShotPacket {
		return
	}

	switch ps.Mode {
	case ChargeLeft:
		if !leftNow {
			ps.ReleaseTick = seqN
			ps.WaitingShotPacket = true
			log.Printf("[SHOT RELEASE] owner=%d mode=LEFT releasedTick=%d", player.ID, seqN)
		}

	case ChargeRight:
		if !rightNow {
			ps.ReleaseTick = seqN
			ps.WaitingShotPacket = true
			log.Printf("[SHOT RELEASE] owner=%d mode=RIGHT releasedTick=%d", player.ID, seqN)
		}
	}
}

// applyMaturePendingEvents applica tutti gli eventi con seq <= matchTick.
func (gameRoom *GameRoom) isServerFrozen(player *Player) bool {
	return gameRoom.matchTick >= player.FreezeStartTick && gameRoom.matchTick < player.FreezeEndTick
}

func (gameRoom *GameRoom) applyMaturePendingEvents(
	player *Player,
	pending *[]PlayerEvent,
	opponentConn *PlayerConnection,
	deltaTime float32,
) (applied int, futureCount int) {
	consumeCount := 0

	for consumeCount < len(*pending) {
		event := (*pending)[consumeCount]

		if event.SeqN() > gameRoom.matchTick {
			break
		}

		switch event.Type {
		case EventInput:
			inputState := event.Input

			if inputState.SeqN <= player.LastSeqN {
				consumeCount++
				continue
			}

			effectiveMask := inputState.Mask &^ Freezing
			if gameRoom.isServerFrozen(player) {
				effectiveMask |= Freezing
				effectiveMask &^= ShootLeft | ShootRight
			}
			gameRoom.updatePendingShot(player, effectiveMask, inputState.SeqN)

			newX, newY := StepFromState(player.X, player.Y, effectiveMask, deltaTime)
			player.X, player.Y = gameRoom.collisionManager.ResolvePlayerObstacleOverlaps(
				playerCollisionTag(player.ID), newX, newY,
			)

			player.LastMask = effectiveMask
			player.LastSeqN = inputState.SeqN
			player.RecvMask = inputState.Mask
			player.RecvSeqN = inputState.SeqN
			applied++

			ps := &player.PendingShot
			if ps.Active && ps.WaitingShotPacket && ps.ReleaseTick == inputState.SeqN {
				log.Printf(
					"[SHOT RELEASE AUTHPOS] owner=%d releaseClientTick=%d serverMatchTick=%d authPosAtRelease=(%.1f, %.1f) lastSeq=%d mask=%d",
					player.ID,
					ps.ReleaseTick,
					gameRoom.matchTick,
					player.X,
					player.Y,
					player.LastSeqN,
					player.LastMask,
				)
			}

			if ps.Active && ps.WaitingShotPacket && ps.ShotPacket != nil {
				shot := gameRoom.createShotFromEvent(player, *ps.ShotPacket)
				if shot.ShotID != 0 {
					gameRoom.forwardShotEventToOpponent(opponentConn, shot)
				}
				ps.ShotPacket = nil
			}

		case EventEggDelivered:
			gameRoom.recordEggDelivery(player, event.EggID)
			applied++

		case EventShot:
			shotEvent := event.Shot
			if gameRoom.isServerFrozen(player) {
				log.Printf("[SHOT REJECT FROZEN] owner=%d shotClientTick=%d serverMatchTick=%d freeze=[%d,%d)", player.ID, shotEvent.SeqN, gameRoom.matchTick, player.FreezeStartTick, player.FreezeEndTick)
				consumeCount++
				continue
			}
			ps := &player.PendingShot

			log.Printf(
				"[SHOT PKT] owner=%d shotClientTick=%d serverMatchTick=%d target=(%.1f, %.1f) localChargeMs=%d currentAuthPos=(%.1f, %.1f) releaseTickSeen=%d",
				player.ID,
				shotEvent.SeqN,
				gameRoom.matchTick,
				shotEvent.X,
				shotEvent.Y,
				shotEvent.Charge,
				player.X,
				player.Y,
				ps.ReleaseTick,
			)

			if ps.Active {
				copied := shotEvent
				ps.ShotPacket = &copied
			}

			if ps.Active && ps.WaitingShotPacket && ps.ShotPacket != nil {
				shot := gameRoom.createShotFromEvent(player, *ps.ShotPacket)
				if shot.ShotID != 0 {
					gameRoom.forwardShotEventToOpponent(opponentConn, shot)
				}
				ps.ShotPacket = nil
			}

			applied++
		}

		consumeCount++
	}

	if consumeCount > 0 {
		remaining := (*pending)[consumeCount:]
		copy((*pending), remaining)
		*pending = (*pending)[:len(remaining)]
	}

	futureCount = len(*pending)
	return
}

func (gameRoom *GameRoom) updatePlayerColliders() {
	gameRoom.collisionManager.ModifyObject(
		playerCollisionTag(gameRoom.firstPlayer.ID),
		gameRoom.firstPlayer.X+penguinHalfTextureFractionWidth,
		gameRoom.firstPlayer.Y+penguinHalfTextureFractionHeight,
		penguinTextureFractionWidth,
		penguinHalfTextureFractionHeight,
	)

	gameRoom.collisionManager.ModifyObject(
		playerCollisionTag(gameRoom.secondPlayer.ID),
		gameRoom.secondPlayer.X+penguinHalfTextureFractionWidth,
		gameRoom.secondPlayer.Y+penguinHalfTextureFractionHeight,
		penguinTextureFractionWidth,
		penguinHalfTextureFractionHeight,
	)
}

func (gameRoom *GameRoom) computeMaxWorldRange(muzzleBaseX, muzzleBaseY float32) float32 {
	corners := [][2]float32{
		{0, 0},
		{gameWorldWidth, 0},
		{0, gameWorldHeight},
		{gameWorldWidth, gameWorldHeight},
	}

	var maxWorldRangeSq float32 = 0

	for _, c := range corners {
		dx := c[0] - muzzleBaseX
		dyWorld := (c[1] - muzzleBaseY) / shotPlanePerspectiveY
		distSq := dx*dx + dyWorld*dyWorld
		if distSq > maxWorldRangeSq {
			maxWorldRangeSq = distSq
		}
	}

	return float32(math.Sqrt(float64(maxWorldRangeSq)))
}

func computeGravityForLowShot(maxWorldRange float32) float32 {
	thetaLowRad := float32(math.Pi * shotThetaLeftDeg / 180.0)
	return (2.0 * maxWorldRange * float32(math.Tan(float64(thetaLowRad)))) /
		(shotDesiredFullChargeFlightTimeLow * shotDesiredFullChargeFlightTimeLow)
}

func computeMaxV0(gravity float32) float32 {
	thetaLowRad := float32(math.Pi * shotThetaLeftDeg / 180.0)
	return (gravity * shotDesiredFullChargeFlightTimeLow) / (2.0 * float32(math.Sin(float64(thetaLowRad))))
}

func (gameRoom *GameRoom) buildServerShotRuntime(player *Player, shotEvent ShotEvent, shotType ShotType, authChargeMs uint32) (
	startX, startY float32,
	groundVelX, groundVelY float32,
	startVerticalSpeed float32,
	gravity float32,
	flightDuration float32,
) {
	muzzleBaseX := player.X + 48.0
	muzzleBaseY := player.Y + shotMuzzleOffsetY

	diffX := shotEvent.X - muzzleBaseX
	diffY := shotEvent.Y - muzzleBaseY
	diffY /= shotPlanePerspectiveY

	diffX, diffY = normalize2D(diffX, diffY)

	maxWorldRange := gameRoom.computeMaxWorldRange(muzzleBaseX, muzzleBaseY)
	gravity = computeGravityForLowShot(maxWorldRange)
	maxV0 := computeMaxV0(gravity)

	chargeSeconds := float32(authChargeMs) / 1000.0
	charge01 := clamp01(chargeSeconds / shotMaxChargeSeconds)
	power01 := float32(math.Sqrt(float64(charge01)))
	v0 := power01 * maxV0

	thetaDeg := float32(shotThetaLeftDeg)
	if shotType == ShotTypeRight {
		thetaDeg = shotThetaRightDeg
	}
	thetaRad := float32(math.Pi) * thetaDeg / 180.0

	groundSpeed := v0 * float32(math.Cos(float64(thetaRad)))
	startVerticalSpeed = v0 * float32(math.Sin(float64(thetaRad)))

	groundVelX = diffX * groundSpeed
	groundVelY = diffY * groundSpeed

	aimScreenDirX, aimScreenDirY := normalize2D(shotEvent.X-muzzleBaseX, shotEvent.Y-muzzleBaseY)

	startX = muzzleBaseX + aimScreenDirX*shotMuzzleDistance
	startY = muzzleBaseY + aimScreenDirY*shotMuzzleDistance

	if gravity > 0 {
		flightDuration = (2.0 * startVerticalSpeed) / gravity
	}

	return
}

func (gameRoom *GameRoom) computeShotScreenAndHeight(shot *Shot) (screenX, screenY, shadowX, shadowY, z float32, expired bool) {
	if !shot.IsAlive {
		return 0, 0, 0, 0, 0, true
	}

	elapsedTicks := gameRoom.matchTick - shot.SpawnTick
	t := float32(elapsedTicks) / float32(gameRoom.tickRateHz)

	if elapsedTicks >= shotLandingTicks(shot.FlightDuration, gameRoom.tickRateHz) {
		return 0, 0, 0, 0, 0, true
	}

	worldDeltaX := shot.GroundVelX * t
	worldDeltaY := shot.GroundVelY * t

	z = (shot.StartVerticalSpeed * t) - (0.5 * shot.Gravity * t * t)
	if z < 0 {
		z = 0
	}

	screenX = shot.StartX + worldDeltaX
	screenY = shot.StartY + (shotPlanePerspectiveY * worldDeltaY) - (0.35 * z)

	shadowX = shot.StartX + worldDeltaX
	shadowY = shot.StartY + (shotPlanePerspectiveY * worldDeltaY)

	return screenX, screenY, shadowX, shadowY, z, false
}

func (gameRoom *GameRoom) updateShotColliders() {
	for i := range gameRoom.activeShots {
		shot := &gameRoom.activeShots[i]

		if !shot.IsAlive {
			continue
		}

		_, _, shadowX, shadowY, z, expired := gameRoom.computeShotScreenAndHeight(shot)
		shotTag := shotCollisionTag(shot.ShotID)

		if expired {
			gameRoom.resolveShot(shot, ShotOutcomeExpired, 0)
			continue
		}

		log.Printf(
			"[SHOT POS] tick=%d shot=%s shadow=(%.1f, %.1f) z=%.1f",
			gameRoom.matchTick,
			shotTag,
			shadowX,
			shadowY,
			z,
		)

		if z >= shotColliderDisableHeightThreshold {
			gameRoom.collisionManager.ModifyObject(
				shotTag,
				-1000,
				-1000,
				shotColliderWidth,
				shotColliderHeight,
			)
		} else {
			posCollX := float32(int32(shadowX) + shotColliderWidth/2)
			posCollY := float32(int32(shadowY) + shotColliderHeight/2)

			gameRoom.collisionManager.ModifyObject(
				shotTag,
				posCollX,
				posCollY,
				shotColliderWidth,
				shotColliderHeight,
			)
		}
	}
}

// resolveShot chiude la palla e manda l'esito a entrambi, una sola volta.
func (gameRoom *GameRoom) resolveShot(shot *Shot, outcome ShotOutcome, hitPlayerID uint32) {
	if shot == nil || !shot.IsAlive {
		return
	}

	shot.IsAlive = false
	log.Printf("[SHOT_TRACE] role=SERVER event=RESOLVED key=%d:%d spawnTick=%d hitTick=%d flightTicks=%d outcome=%d victim=%d utc=%s", shot.OwnerPlayerID, shot.ClientShotTick, shot.SpawnTick, gameRoom.matchTick, gameRoom.matchTick-shot.SpawnTick, outcome, hitPlayerID, time.Now().UTC().Format(time.RFC3339Nano))
	shotTag := shotCollisionTag(shot.ShotID)
	gameRoom.collisionManager.RemoveObject(shotTag)

	// Per l'HIT invia anche la posizione e l'ACK del bersaglio al momento dell'impatto.
	var hitAck uint32
	var hitX, hitY float32
	if outcome == ShotOutcomePenguin {
		victim := &gameRoom.firstPlayer
		if hitPlayerID == 2 {
			victim = &gameRoom.secondPlayer
		}
		hitAck, hitX, hitY = victim.LastSeqN, victim.X, victim.Y
		// Il freeze dura 90 tick del server, senza finestre sugli input client.
		freezeEnd := gameRoom.matchTick + 90
		if freezeEnd > victim.FreezeEndTick {
			victim.FreezeStartTick = gameRoom.matchTick
			victim.FreezeEndTick = freezeEnd
		}
		victim.LastMask |= Freezing
		victim.PendingShot = PendingShot{}
	}

	packet := make([]byte, 34)
	packet[0] = MsgShotResolved
	binary.LittleEndian.PutUint32(packet[1:5], shot.OwnerPlayerID)
	binary.LittleEndian.PutUint32(packet[5:9], shot.ClientShotTick)
	packet[9] = byte(outcome)
	binary.LittleEndian.PutUint32(packet[10:14], hitPlayerID)
	binary.LittleEndian.PutUint32(packet[14:18], gameRoom.matchTick)
	binary.LittleEndian.PutUint32(packet[18:22], hitAck)
	binary.LittleEndian.PutUint32(packet[22:26], math.Float32bits(hitX))
	binary.LittleEndian.PutUint32(packet[26:30], math.Float32bits(hitY))
	binary.LittleEndian.PutUint32(packet[30:34], gameRoom.matchTick-shot.SpawnTick)

	// Terminal outcomes must not be silently dropped when outgoing queues are full.
	gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
	gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packet, true)

	log.Printf(
		"[SHOT RESOLVED] tick=%d shot=%s owner=%d clientTick=%d outcome=%d victim=%d",
		gameRoom.matchTick, shotTag, shot.OwnerPlayerID, shot.ClientShotTick, outcome, hitPlayerID,
	)
}

func (gameRoom *GameRoom) resolveCollisionResults() {
	results := gameRoom.collisionManager.Results()

	for _, result := range results {
		if result.Type <= 0 {
			continue
		}

		log.Printf(
			"[CPP COLLISION] tick=%d my=%s other=%s type=%d",
			gameRoom.matchTick,
			result.MyTag,
			result.OtherTag,
			result.Type,
		)

		// SHOT vs OBSTACLE
		if (isShotTag(result.MyTag) && isObstacleTag(result.OtherTag)) ||
			(isShotTag(result.OtherTag) && isObstacleTag(result.MyTag)) {

			var shotTag, obstacleTag string
			if isShotTag(result.MyTag) {
				shotTag = result.MyTag
				obstacleTag = result.OtherTag
			} else {
				shotTag = result.OtherTag
				obstacleTag = result.MyTag
			}

			shot, ok := findShotByTag(shotTag, gameRoom.activeShots)
			if !ok || !shot.IsAlive {
				continue
			}

			log.Printf(
				"[SHOT HIT OBSTACLE] tick=%d shot=%s owner=%d obstacle=%s",
				gameRoom.matchTick,
				shotTag,
				shot.OwnerPlayerID,
				obstacleTag,
			)

			gameRoom.resolveShot(shot, ShotOutcomeObstacle, 0)
			continue
		}

		// SHOT vs PENGUIN
		if (isShotTag(result.MyTag) && isPenguinTag(result.OtherTag)) ||
			(isShotTag(result.OtherTag) && isPenguinTag(result.MyTag)) {

			var shotTag, penguinTag string
			if isShotTag(result.MyTag) {
				shotTag = result.MyTag
				penguinTag = result.OtherTag
			} else {
				shotTag = result.OtherTag
				penguinTag = result.MyTag
			}

			shot, ok := findShotByTag(shotTag, gameRoom.activeShots)
			if !ok || !shot.IsAlive {
				continue
			}

			ownerTag := playerCollisionTag(shot.OwnerPlayerID)

			if penguinTag == ownerTag {
				log.Printf(
					"[SHOT SELF-COLLISION IGNORED] tick=%d shot=%s owner=%d penguin=%s",
					gameRoom.matchTick,
					shotTag,
					shot.OwnerPlayerID,
					penguinTag,
				)
				continue
			}

			log.Printf(
				"[SHOT HIT PENGUIN] tick=%d shot=%s owner=%d penguin=%s",
				gameRoom.matchTick,
				shotTag,
				shot.OwnerPlayerID,
				penguinTag,
			)

			hitPlayerID := uint32(1)
			if penguinTag == playerCollisionTag(2) {
				hitPlayerID = 2
			}
			gameRoom.resolveShot(shot, ShotOutcomePenguin, hitPlayerID)
		}
	}
}

// SendJoinAcknowledgements manda a ciascun client il proprio ID e gli spawn iniziali.
func (gameRoom *GameRoom) SendJoinAcknowledgements() {
	packetP1 := make([]byte, 25)
	packetP1[0] = MsgJoinAck
	binary.LittleEndian.PutUint32(packetP1[1:5], gameRoom.firstPlayer.ID)
	binary.LittleEndian.PutUint32(packetP1[5:9], math.Float32bits(gameRoom.firstPlayer.X))
	binary.LittleEndian.PutUint32(packetP1[9:13], math.Float32bits(gameRoom.firstPlayer.Y))
	binary.LittleEndian.PutUint32(packetP1[13:17], math.Float32bits(gameRoom.secondPlayer.X))
	binary.LittleEndian.PutUint32(packetP1[17:21], math.Float32bits(gameRoom.secondPlayer.Y))
	binary.LittleEndian.PutUint32(packetP1[21:25], gameRoom.tickRateHz)
	gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packetP1, true)

	packetP2 := make([]byte, 25)
	packetP2[0] = MsgJoinAck
	binary.LittleEndian.PutUint32(packetP2[1:5], gameRoom.secondPlayer.ID)
	binary.LittleEndian.PutUint32(packetP2[5:9], math.Float32bits(gameRoom.secondPlayer.X))
	binary.LittleEndian.PutUint32(packetP2[9:13], math.Float32bits(gameRoom.secondPlayer.Y))
	binary.LittleEndian.PutUint32(packetP2[13:17], math.Float32bits(gameRoom.firstPlayer.X))
	binary.LittleEndian.PutUint32(packetP2[17:21], math.Float32bits(gameRoom.firstPlayer.Y))
	binary.LittleEndian.PutUint32(packetP2[21:25], gameRoom.tickRateHz)
	gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packetP2, true)
}

// CreateInitialEggs genera un certo numero di uova iniziali.
func (gameRoom *GameRoom) CreateInitialEggs(eggCount int) {
	gameRoom.totalEggs = eggCount
	for eggID := 0; eggID < eggCount; eggID++ {
		eggX := float32(250 + rand.Intn(250))
		eggY := float32(rand.Intn(400))
		gameRoom.sendSpawnEggToBothPlayers(int32(eggID), eggX, eggY)
	}
}

// sendSpawnEggToBothPlayers manda un pacchetto spawn egg a entrambi i client.
func (gameRoom *GameRoom) sendSpawnEggToBothPlayers(eggID int32, eggX, eggY float32) {
	packet := make([]byte, 13)
	packet[0] = MsgSpawnEgg
	binary.LittleEndian.PutUint32(packet[1:5], uint32(eggID))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(eggX))
	binary.LittleEndian.PutUint32(packet[9:13], math.Float32bits(eggY))
	gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
	gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
}

// sendSpawnObstacleToBothPlayers manda a entrambi i client gli ostacoli iniziali.
func (gameRoom *GameRoom) sendSpawnObstacleToBothPlayers() {
	for i := 0; i < len(gameRoom.Obstacles); i++ {
		packet := make([]byte, 13)
		packet[0] = MsgSpawnObstacle

		fmt.Printf("Coordinate: %+v\n", gameRoom.Obstacles[i])

		binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(gameRoom.Obstacles[i].X))
		binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(gameRoom.Obstacles[i].Y))

		gameRoom.firstPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
		gameRoom.secondPlayerConnection.TryEnqueueOutgoingPacket(packet, true)
	}
}

// sendAuthStateToPlayer manda a un client il proprio stato autoritativo.
func (gameRoom *GameRoom) sendAuthStateToPlayer(targetConn *PlayerConnection, player Player) {
	packet := make([]byte, 13)
	packet[0] = MsgAuthState
	binary.LittleEndian.PutUint32(packet[1:5], player.LastSeqN)
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(player.X))
	binary.LittleEndian.PutUint32(packet[9:13], math.Float32bits(player.Y))
	targetConn.TryEnqueueOutgoingPacket(packet, false)
}

// sendRemoteStateToPlayer manda a un client lo stato remoto dell’avversario.
func (gameRoom *GameRoom) sendRemoteStateToPlayer(targetConn *PlayerConnection, opponent Player) {
	packet := make([]byte, 13)
	packet[0] = MsgRemoteState
	binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(opponent.X))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(opponent.Y))
	binary.LittleEndian.PutUint32(packet[9:13], uint32(opponent.LastMask))
	targetConn.TryEnqueueOutgoingPacket(packet, false)
}

// forwardShotEventToOpponent manda al client remoto l'evento shot da renderizzare.
func (gameRoom *GameRoom) forwardShotEventToOpponent(targetConn *PlayerConnection, shot Shot) {
	chargeMs := uint32(float64(shot.AuthCharge) * (1000.0 / float64(gameRoom.tickRateHz)))
	if chargeMs > chargeCap {
		chargeMs = chargeCap
	}

	packet := make([]byte, 22)
	packet[0] = MsgRemoteShot
	binary.LittleEndian.PutUint32(packet[1:5], math.Float32bits(shot.TargetX))
	binary.LittleEndian.PutUint32(packet[5:9], math.Float32bits(shot.TargetY))
	binary.LittleEndian.PutUint32(packet[9:13], chargeMs)
	packet[13] = byte(shot.Type)
	binary.LittleEndian.PutUint32(packet[14:18], shot.OwnerPlayerID)
	binary.LittleEndian.PutUint32(packet[18:22], shot.ClientShotTick)

	targetConn.TryEnqueueOutgoingPacket(packet, true)
}

// createShotFromEvent crea lo shot vero e proprio.
func (gameRoom *GameRoom) createShotFromEvent(player *Player, shotEvent ShotEvent) Shot {
	ps := &player.PendingShot

	if !ps.Active {
		log.Printf("[SHOT] owner=%d arrivato MsgShot ma nessuna carica attiva", player.ID)
		return Shot{}
	}

	if !ps.WaitingShotPacket {
		log.Printf("[SHOT] owner=%d arrivato MsgShot ma la carica non è stata ancora rilasciata", player.ID)
		return Shot{}
	}

	if ps.ReleaseTick < ps.PressTick {
		log.Printf("[SHOT] owner=%d releaseTick < pressTick", player.ID)
		return Shot{}
	}

	// Il pacchetto deve appartenere proprio a questo rilascio.
	if shotEvent.SeqN != ps.ReleaseTick {
		log.Printf("[SHOT REJECT] owner=%d shotTick=%d releaseTick=%d", player.ID, shotEvent.SeqN, ps.ReleaseTick)
		return Shot{}
	}

	authCharge := ps.ReleaseTick - ps.PressTick

	shotType := ShotTypeLeft
	if ps.Mode == ChargeRight {
		shotType = ShotTypeRight
	}

	authChargeMs := uint32(float64(authCharge) * (1000.0 / float64(gameRoom.tickRateHz)))
	deltaMs := int64(shotEvent.Charge) - int64(authChargeMs)

	// Integrazione dell'errore di quantizzazione.
	if deltaMs <= 34 {
		authChargeMs = uint32(int64(authChargeMs) + deltaMs)
	}

	// Cap finale.
	if authChargeMs > chargeCap {
		authChargeMs = chargeCap
	}

	startX, startY,
		groundVelX, groundVelY,
		startVerticalSpeed,
		gravity,
		flightDuration := gameRoom.buildServerShotRuntime(player, shotEvent, shotType, authChargeMs)

	log.Printf("[SHOT_TRACE] role=SERVER event=START key=%d:%d tick=%d utc=%s", player.ID, shotEvent.SeqN, gameRoom.matchTick, time.Now().UTC().Format(time.RFC3339Nano))
	log.Printf("[SHOT_FLIGHT_SERVER] key=%d:%d chargeMs=%d type=%d fullDuration=%.7f expectedLandingTicks=%d", player.ID, shotEvent.SeqN, authChargeMs, shotType, flightDuration, shotLandingTicks(flightDuration, gameRoom.tickRateHz))
	shot := Shot{
		ShotID:         gameRoom.nextShotID,
		ClientShotTick: shotEvent.SeqN,
		OwnerPlayerID:  player.ID,
		TargetX:        shotEvent.X,
		TargetY:        shotEvent.Y,
		LocalCharge:    shotEvent.Charge,
		PressedTick:    ps.PressTick,
		ReleasedTick:   ps.ReleaseTick,
		AuthCharge:     authCharge,
		Type:           shotType,

		StartX:             startX,
		StartY:             startY,
		SpawnTick:          gameRoom.matchTick,
		IsAlive:            true,
		GroundVelX:         groundVelX,
		GroundVelY:         groundVelY,
		StartVerticalSpeed: startVerticalSpeed,
		Gravity:            gravity,
		FlightDuration:     flightDuration,
	}

	gameRoom.nextShotID++
	gameRoom.activeShots = append(gameRoom.activeShots, shot)

	shotTag := shotCollisionTag(shot.ShotID)
	gameRoom.collisionManager.AddObject(
		shotTag,
		shot.StartX,
		shot.StartY,
		shotColliderWidth,
		shotColliderHeight,
	)

	log.Printf(
		"[SHOT CREATE] owner=%d shotId=%d serverMatchTick=%d press=%d release=%d shotPktSeq=%d currentAuthPosAtCreate=(%.1f, %.1f)",
		player.ID,
		shot.ShotID,
		gameRoom.matchTick,
		shot.PressedTick,
		shot.ReleasedTick,
		shotEvent.SeqN,
		player.X,
		player.Y,
	)

	log.Printf(
		"[SHOT CMP] owner=%d shotId=%d type=%d press=%d release=%d localChargeMs=%d authChargeTicks=%d authChargeMs=%d deltaMs=%d target=(%.1f, %.1f)",
		shot.OwnerPlayerID,
		shot.ShotID,
		shot.Type,
		shot.PressedTick,
		shot.ReleasedTick,
		shot.LocalCharge,
		shot.AuthCharge,
		authChargeMs,
		deltaMs,
		shot.TargetX,
		shot.TargetY,
	)

	// Reset FSM shot dopo la creazione.
	ps.Active = false
	ps.Mode = ChargeNone
	ps.PressTick = 0
	ps.ReleaseTick = 0
	ps.WaitingShotPacket = false
	ps.WaitingNeutral = true
	ps.ShotPacket = nil

	return shot
}
