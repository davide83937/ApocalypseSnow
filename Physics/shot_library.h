#ifndef SHOT_PHYSICS_LIBRARY_H
#define SHOT_PHYSICS_LIBRARY_H

#include "library.h"

#ifdef __cplusplus
extern "C" {
#endif

// Stato iniziale di un proiettile, indipendente dagli oggetti del gioco.
typedef struct {
    Vector2 start;
    Vector2 groundVelocity;
    float verticalSpeed;
    float gravity;
} ShotParameters;

// La libreria riceve solo dati fisici: origine, direzione sul piano,
// modulo della velocita', angolo in gradi e gravita'.
// Non conosce sprite, pinguini, offset della mano o tipi di tiro.
// La direzione puo' essere non normalizzata (fallback verso destra).
PHYSICS_API ShotParameters ComputeShotParameters(
    Vector2 origin, Vector2 direction, float speed, float angleDegrees, float gravity);

// Durata del volo fino al ritorno a quota zero.
PHYSICS_API float ShotFlightDuration(float initialVerticalSpeed, float gravity);

// Altezza massima rispetto al punto di partenza.
PHYSICS_API float ShotMaximumHeight(float initialVerticalSpeed, float gravity);

#ifdef __cplusplus
}
#endif

#endif
