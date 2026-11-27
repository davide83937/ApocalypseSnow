#include "shot_library.h"
#include <cmath>
#include <numbers>

namespace {
constexpr float MinDirectionLengthSquared = 0.0001f;

float radians(float degrees) {
    return degrees * (std::numbers::pi_v<float> / 180.0f);
}

Vector2 normalizedOrRight(Vector2 value) {
    const float lenSq = value.x * value.x + value.y * value.y;
    if (lenSq <= MinDirectionLengthSquared)
        return {1.0f, 0.0f};
    const float inverseLength = 1.0f / std::sqrt(lenSq);
    return {value.x * inverseLength, value.y * inverseLength};
}
} // namespace

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

float ShotFlightDuration(float initialVerticalSpeed, float gravity) {
    return gravity > 0.0f ? 2.0f * initialVerticalSpeed / gravity : 0.0f;
}

float ShotMaximumHeight(float initialVerticalSpeed, float gravity) {
    return gravity > 0.0f
         ? initialVerticalSpeed * initialVerticalSpeed / (2.0f * gravity) : 0.0f;
}
