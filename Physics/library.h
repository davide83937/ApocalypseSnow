#pragma once

// Interfaccia pubblica della DLL fisica, condivisa da client C# e server Go.
// Quando compiliamo la DLL, CMake crea una copia in Shared/library.h.
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

    // Hitbox di input: x e y sono il CENTRO del rettangolo sul piano XY,
    // width e height sono le dimensioni, mentre tag identifica l'oggetto
    // minZ/maxZ rimangono nel protocollo dei chiamanti, ma la DLL
    // rileva soltanto la sovrapposizione XY; Z e' valutata da C# e Go.
    struct CollisionDataIn {
        char tag[16];
        float x;
        float y;
        int width;
        int height;
        float minZ;
        float maxZ;
    };

    // Risultato di UNA coppia di hitbox che si intersecano:
    // i tag identificano la coppia, type e' un CollisionSide riferito
    // all'oggetto myTag rispetto a otherTag; penetration e' la minima
    // sovrapposizione sul piano XY, utile per correggere la posizione.
    struct CollisionDataOut {
        char myTag[16];
        char otherTag[16];
        int type;
        float penetration;
    };

    // Da quale lato i due rettangoli si sono toccati sul terreno (XY).
    enum CollisionSide {
        COLLISION_NONE = 0,
        COLLISION_TOP = 1,    // Impatto dal lato superiore dell'ostacolo
        COLLISION_BOTTOM = 2, // Impatto dal lato inferiore
        COLLISION_LEFT = 3,   // Impatto dal lato sinistro
        COLLISION_RIGHT = 4   // Impatto dal lato destro
    };

// Coppia di float usata sia sul piano XY sia per vettori di direzione.
// Non ha metodi: la rappresentazione resta semplice e compatibile con C.
typedef struct {
        float x;
        float y;
    } Vector2;

    // Questi fattori servono a disegnare la prospettiva del tiro.
    // Rimangono costanti interne C++: Go usa le funzioni della DLL,
    // quindi non deve avere un'altra copia di questi numeri.
#ifdef __cplusplus
    // Ogni unità di spostamento sul piano Y produce
    // uno spostamento di 0,70 unità sullo schermo.
    const float PlanePerspectiveY = 0.70f;

    // Ogni unità di altezza fisica Z produce
    // uno spostamento verso l'alto di 0,35 unità sullo schermo.
    const float HeightProjection = 0.35f;
#endif

    // Aggiorna una coordinata usando posizione += velocita' * intervallo.
    PHYSICS_API void uniform_rectilinear_motion(float *position, float velocity, float deltaTime);
    // Normalizza in-place (velX, velY): diagonali e assi hanno modulo 1.
    // Un vettore nullo resta nullo, evitando divisioni per zero.
    PHYSICS_API void normalizeVelocity(float* velX, float* velY);

    // Dichiarazione storica: l'implementazione e' attualmente commentata
    // in library.cpp. Non usare questa API finche' non viene ripristinata.
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
    // Confronta tutte le coppie i<j soltanto in XY, SENZA
    // spostare gli oggetti. Esclude coppie con tag identici.
    // Il chiamante deve allocare dataOut per ALMENO count*(count-1)/2
    // risultati potenziali. Questa API non restituisce il numero di hit:
    // vengono scritte solo le prime celle corrispondenti alle collisioni.
    PHYSICS_API void check_collisions(struct CollisionDataIn* data, struct CollisionDataOut* dataOut,int count);


    // API storica non implementata nella DLL attuale; conservata come
    // dichiarazione ma da non usare nella logica corrente.
    PHYSICS_API float calculate_ball_scale_only(
        float startX, float startY,
        float finalX, float finalY,
        float posX,
        float startSpeedX,
        float L, float K,
        float currentScale
    );

    // Distanza euclidea e interpolazioni lineari con t limitato a [0,1].
    PHYSICS_API float Distance(Vector2 a, Vector2 b);
    PHYSICS_API Vector2 Lerp(Vector2 a, Vector2 b, float t);
    PHYSICS_API float LerpFloat(float v0, float v1, float t);

    // Proietta il moto nel mondo (delta XY + quota Z) nelle coordinate
    // dello schermo, usando prospettiva del piano e altezza apparente.
    PHYSICS_API Vector2 calculateScreenPosition(Vector2 startPos, float worldDeltaX, float worldDeltaY, float z);
    // Produce la scala visuale della palla e scrive l'alpha tramite puntatore:
    // e' un ausilio al rendering, non al rilevamento delle collisioni.
    PHYSICS_API float calculateVisualScale(float z, float maxHeight, float scaleMin, float scaleMax, float* outAlpha);

    // Campiona il moto del proiettile all'istante t: spostamento sul
    // piano XY e quota Z, con quota finale limitata a zero.
    PHYSICS_API void calculate_parabolic_motion(
        float t,
        float groundVelocityWorldX, float groundVelocityWorldY,
        float startVerticalVelocity, float gravity,
        float* worldDeltaX, float* worldDeltaY, float* z
    );
#ifdef __cplusplus
}
#endif