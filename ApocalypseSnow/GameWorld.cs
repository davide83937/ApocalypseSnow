namespace ApocalypseSnow;

// Coordinate logiche condivise tra client C# e server Go.
// La fisica non deve variare con le dimensioni della finestra.
// TODO C++: valutare una sola fonte nativa per dimensioni LOGICHE del mondo
// e hitbox dei tiri (duplicati in Server/game_room.go). Esporre getter a
// client e server; dimensioni della finestra e rendering restano in C#.
public static class GameWorld
{
    public const int Width = 800;
    public const int Height = 480;
    public const int ShotColliderWidth = 20;
    public const int ShotColliderHeight = 25;

    // Altezza FISICA usata per decidere se una palla colpisce un oggetto.
    // Parte da terra (Z = 0): il pinguino arriva a Z = 60, il muro a Z = 90.
    // Non sono le dimensioni in pixel dello sprite disegnato sullo schermo.
    public const float PenguinCollisionHeight = 60f;
    public const float ObstacleCollisionHeight = 90f;
}
