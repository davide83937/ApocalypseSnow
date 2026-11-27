#ifndef SHOT_PHYSICS_LIBRARY_H
#define SHOT_PHYSICS_LIBRARY_H

// API della balistica: calcolo dei parametri iniziali e formule del volo.
// Le regole di ApocalypseSnow (carica, angoli 30/60, mano del pinguino)
// vengono stabilite nei chiamanti C#/Go, non in questo file.
#include "library.h"

#ifdef __cplusplus
extern "C" {
#endif

// Stato iniziale restituito a C# e Go. Tutti i campi usano float;
// non cambiare ordine o tipo senza aggiornare le strutture interoperabili.
typedef struct {
    Vector2 start;          // Punto di partenza sul piano XY.
    Vector2 groundVelocity; // Velocita' sul piano XY, in unita'/secondo.
    float verticalSpeed;    // Velocita' iniziale in Z (verso l'alto).
    float gravity;          // Accelerazione positiva, diretta verso il basso.
} ShotParameters;

// La libreria riceve solo dati fisici: origine, direzione sul piano,
// modulo della velocita', angolo in gradi e gravita'.
// La direzione puo' essere non normalizzata (fallback verso destra).
// angleDegrees e' l'angolo tra il piano e l'asse Z
// 90 = tutto in Z
PHYSICS_API ShotParameters ComputeShotParameters(
    Vector2 origin, Vector2 direction, float speed, float angleDegrees, float gravity);

// Durata teorica del volo fino al ritorno a quota zero:
// 2 * verticalSpeed / gravity (con gravita' positiva).
// Con gravity <= 0 viene restituito zero.
PHYSICS_API float ShotFlightDuration(float initialVerticalSpeed, float gravity);

// Altezza massima rispetto al punto di partenza, con gravita' positiva.
// Formula: verticalSpeed^2 / (2 * gravity); altrimenti restituisce zero.
PHYSICS_API float ShotMaximumHeight(float initialVerticalSpeed, float gravity);

#ifdef __cplusplus
}
#endif

#endif
