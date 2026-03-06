package main

// Tipi messaggi (wire format Little Endian)
const (
	MsgState       byte = 1 // 9B  [type][mask:int32][seq:uint32]
	MsgShot        byte = 2 // 17B [type][shotTick:uint32][x:float32][y:float32][charge:int32]
	MsgJoin        byte = 3 // 9B  [type][name:8B]
	MsgAuthState   byte = 4 // 13B [type][ack:uint32][x:float32][y:float32]
	MsgJoinAck     byte = 5 // 21B [type][playerId:uint32][spawnX:float32][spawnY:float32][oppX:float32][oppY:float32]
	MsgRemoteState byte = 6 // 13B [type][x:float32][y:float32][mask:int32]
	MsgRemoteShot  byte = 7 // 13B [type][x:int32][y:int32][charge:int32]
	MsgSpawnEgg    byte = 8 // 13B [type][id:int32][x:float32][y:float32]
)

// Input mask bit
const (
	Up         int32 = 1 << 0
	Down       int32 = 1 << 1
	Left       int32 = 1 << 2
	Right      int32 = 1 << 3
	Reload     int32 = 1 << 4
	Shoot      int32 = 1 << 5
	Moving     int32 = 1 << 6
	Freezing   int32 = 1 << 7
	TakingEgg  int32 = 1 << 8
	WithEgg    int32 = 1 << 9
	PuttingEgg int32 = 1 << 10
)

const (
	ListenAddr        = ":8080"
	chargeCap  uint32 = 200000

	// Quanti MsgState al massimo dreniamo (per player) in UN server-tick.
	// Noi teniamo SOLO l'ultimo input ricevuto (latest-wins) e poi facciamo 1 step fisico per tick.
	maxDrainInputsPerTick = 64

	// Se non arrivano input da troppo, stoppiamo il player (anti "ghiaccio" quando si perde il release).
	inputStaleTimeoutMs = 150
)
