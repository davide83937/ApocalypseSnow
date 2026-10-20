namespace ApocalypseSnow;

// Coordinate logiche condivise tra client C# e server Go.
// La fisica non deve variare con le dimensioni della finestra.
public static class GameWorld
{
    public const int Width = 800;
    public const int Height = 480;
    public const int ShotColliderWidth = 20;
    public const int ShotColliderHeight = 25;
}
