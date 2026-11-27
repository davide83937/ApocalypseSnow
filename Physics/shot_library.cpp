#include "shot_library.h"
#include <cmath>
#include <numbers>


// Soglia sul MODULO AL QUADRATO: evita normalizzazioni instabili
// quando il chiamante passa una direzione praticamente nulla.
constexpr float MinDirectionLengthSquared = 0.0001f;

// sin() e cos() ricevono radianti; la nostra API riceve gradi.
float radians(float degrees) {
    return degrees * (std::numbers::pi_v<float> / 180.0f);
}

// Calcolo del versore che identifica la direzione.
// Se lenSq è troppo piccolo, ritorniamo il versore dell'asse X.
Vector2 normalizedOrRight(Vector2 value) {
    const float lenSq = value.x * value.x + value.y * value.y;

    if (lenSq <= MinDirectionLengthSquared)
        return {1.0f, 0.0f};

    // Normalizzazione del vettore
    const float length = std::sqrt(lenSq);
    return {value.x / length, value.y / length};
}

// Scomposizione della velocita' iniziale:
// componente parallela al piano = speed * cos(angolo);
// componente verticale         = speed * sin(angolo).
ShotParameters ComputeShotParameters(Vector2 origin, Vector2 direction,
                                     float speed, float angleDegrees, float gravity) {
    const Vector2 unitDirection = normalizedOrRight(direction);
    const float angle = radians(angleDegrees);
    const float groundSpeed = speed * std::cos(angle);

    return ShotParameters{
        origin,
        {unitDirection.x * groundSpeed, unitDirection.y * groundSpeed},
        speed * std::sin(angle),
        gravity
    };
}

// z(t) = vZ * t - (g * t^2)/2: la radice positiva e' t = 2*vZ/g.
// La formula assume partenza a quota Z = 0 e ritorno alla stessa quota.
float ShotFlightDuration(float initialVerticalSpeed, float gravity) {
    return gravity > 0.0f ? 2.0f * initialVerticalSpeed / gravity : 0.0f;
}

// Al culmine del volo la velocita' verticale e' zero:
// hMax = vZ^2 / (2*g), partendo da Z = 0.
float ShotMaximumHeight(float initialVerticalSpeed, float gravity) {
    return gravity > 0.0f
         ? initialVerticalSpeed * initialVerticalSpeed / (2.0f * gravity) : 0.0f;
}
