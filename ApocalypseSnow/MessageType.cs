namespace ApocalypseSnow;

public enum MessageType
{
    State = 1,   //Posizione del player locale
    Shot = 2,       //Shot del player locale
    PlayerJoin = 3, //Richiesta di join al server, usata per entrare in partita
    AuthState = 4, //Posizione autoritativa del player locale, usata per la reconciler
    JoinAck = 5,    //Ack del server alla richiesta di join, usato per iniziare la partita
    RemoteState = 6, //Posizione del player remoto, usato per aggiornare la posizione dell'avversario
    RemoteShot = 7,  //Shot del player remoto, usato per spawnare la palla dell'avversario 
    AuthShot = 8,    // Legacy reserved (server currently uses MsgSpawnEgg=8).
    ShotResolved = 11 // Server terminal outcome of a spawned shot (both players)
}