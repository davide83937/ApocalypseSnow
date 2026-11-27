#pragma once

// Questo e' l'header originale della DLL C++.
// Quando compiliamo la DLL, CMake ne crea una copia in Shared/library.h.
// Il server Go usa quella copia: non serve un altro library.h dentro Server.
#ifdef PHYSICS_BUILD_DLL
    #define PHYSICS_API __declspec(dllexport)
#else
    #define PHYSICS_API __declspec(dllimport)
#endif

// C# chiama la DLL tramite P/Invoke e Go tramite cgo.
// extern "C" evita che C++ cambi i nomi delle funzioni esportate.
// Il controllo __cplusplus serve perche' Go legge questo header come codice C.
#ifdef __cplusplus
extern "C" {
#endif

    // Oggetto da controllare: il rettangolo sul terreno e la sua altezza.
    // minZ e maxZ indicano i limiti dal suolo, non i pixel dello sprite.
    // La palla e' trattata come un punto in altezza: minZ == maxZ.
    // Un ostacolo con minZ=0 e maxZ=90 viene superato a quota Z >= 90.
    struct CollisionDataIn {
        char tag[16];
        float x;
        float y;
        int width;
        int height;
        float minZ;
        float maxZ;
    };

    struct CollisionDataOut {
        char myTag[16];
        char otherTag[16];
        int type;
        float penetration;
    };

    struct CollisionOrder {
        char myTag[16];
        char otherTag[16];
        short traceLeft;
        short traceRight;
        short traceUp;
        short traceDown;
    };

    // Da quale lato i due rettangoli si sono toccati sul terreno (XY).
    enum CollisionSide {
        COLLISION_NONE = 0,
        COLLISION_TOP = 1,    // Impatto dal lato superiore dell'ostacolo
        COLLISION_BOTTOM = 2, // Impatto dal lato inferiore
        COLLISION_LEFT = 3,   // Impatto dal lato sinistro
        COLLISION_RIGHT = 4   // Impatto dal lato destro
    };

typedef struct {
        float x;
        float y;
    } Vector2;

    // Questi fattori servono a disegnare la prospettiva del tiro.
    // Rimangono costanti interne C++: Go usa le funzioni della DLL,
    // quindi non deve avere un'altra copia di questi numeri.
#ifdef __cplusplus
    const float PlanePerspectiveY = 0.70f;
    const float HeightProjection = 0.35f;
#endif

    PHYSICS_API void uniform_rectilinear_motion(float *position, float velocity, float deltaTime);
    PHYSICS_API void normalizeVelocity(float* velX, float* velY);
    PHYSICS_API void parabolic_motion(
        float gravity,
        float start_positionX, float start_positionY,
        float* positionX, float* positionY,
        float start_velocityX, float start_velocityY,
        float gameTime
    );
    // TODO C++: aggiungere qui un'API nativa di risoluzione delle compenetrazioni
    // per hitbox generiche (pinguino e uovo), che riusi check_collisions e
    // restituisca la posizione corretta senza modificare la scena.
    // Prevedere geometria condivisa del pinguino e costanti accessibili a C#/Go;
    // verificare prima le dimensioni effettive degli sprite sul client.
    PHYSICS_API void check_collisions(struct CollisionDataIn* data, struct CollisionDataOut* dataOut,int count);


    PHYSICS_API float calculate_ball_scale_only(
        float startX, float startY,
        float finalX, float finalY,
        float posX,
        float startSpeedX,
        float L, float K,
        float currentScale
    );

    PHYSICS_API float Distance(Vector2 a, Vector2 b);
    PHYSICS_API Vector2 Lerp(Vector2 a, Vector2 b, float t);
    PHYSICS_API float LerpFloat(float v0, float v1, float t);
    PHYSICS_API Vector2 calculateScreenPosition(Vector2 startPos, float worldDeltaX, float worldDeltaY, float z);
    PHYSICS_API float calculateVisualScale(float z, float maxHeight, float scaleMin, float scaleMax, float* outAlpha);
    PHYSICS_API void calculate_parabolic_motion(
        float t,
        float groundVelocityWorldX, float groundVelocityWorldY,
        float startVerticalVelocity, float gravity,
        float* worldDeltaX, float* worldDeltaY, float* z
    );
#ifdef __cplusplus
}
#endif