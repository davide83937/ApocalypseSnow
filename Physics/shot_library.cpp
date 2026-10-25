#include "shot_library.h"
#include <cmath>
#include <algorithm>
#include <numbers>

static inline float ToRadians(float degrees) {
    return degrees * (std::numbers::pi_v<float> / 180.0f);
}

float PredictFullFlightSeconds(int chargeMs, float maxChargeSeconds, float desiredFlightTimeLow, float thetaDeg, float thetaLowDeg) {
    float chargeSeconds = std::clamp(chargeMs / 1000.0f, 0.0f, maxChargeSeconds);
    float theta = ToRadians(thetaDeg);
    float thetaLow = ToRadians(thetaLowDeg);

    return desiredFlightTimeLow *
           std::sqrt(chargeSeconds / maxChargeSeconds) *
           std::sin(theta) / std::sin(thetaLow);
}

float ComputeGravityForLowShot(float maxWorldRange, float desiredFlightTimeLow, float thetaLowRad) {
    return (2.0f * maxWorldRange * std::tan(thetaLowRad)) /
           (desiredFlightTimeLow * desiredFlightTimeLow);
}

float ComputeMaxV0(float gravity, float desiredFlightTimeLow, float thetaLowRad) {
    return (gravity * desiredFlightTimeLow) / (2.0f * std::sin(thetaLowRad));
}

float ComputeVerticalSpeed(float chargeSeconds, float maxChargeSeconds, float maxV0, float thetaDeg, float thetaLowDeg) {
    float charge01 = std::clamp(chargeSeconds / maxChargeSeconds, 0.0f, 1.0f);
    float power01 = std::sqrt(charge01);
    float v0 = power01 * maxV0;
    return v0 * std::sin(ToRadians(thetaDeg));
}

float ComputeMaxWorldRange(Vector2 muzzleBase, float gameWorldWidth, float gameWorldHeight) {
    Vector2 corners[4] = {
        { 0.0f, 0.0f },
        { gameWorldWidth, 0.0f },
        { 0.0f, gameWorldHeight },
        { gameWorldWidth, gameWorldHeight }
    };

    float maxWorldRangeSq = 0.0f;
    for (const auto& corner : corners) {
        float dx = corner.x - muzzleBase.x;
        float dyWorld = (corner.y - muzzleBase.y) / PlanePerspectiveY;
        float distSq = dx * dx + dyWorld * dyWorld;
        if (distSq > maxWorldRangeSq) {
            maxWorldRangeSq = distSq;
        }
    }
    return std::sqrt(maxWorldRangeSq);
}

ShotLaunch ComputeLaunch(
    Vector2 penguinPosition,
    Vector2 target,
    float chargeSeconds,
    float maxChargeSeconds,
    float desiredFlightTimeLow,
    float thetaDeg,
    float thetaLowDeg,
    float muzzleOffsetY,
    float muzzleDistance,
    float gameWorldWidth,
    float gameWorldHeight
) {
    Vector2 muzzleBase = { penguinPosition.x + 48.0f, penguinPosition.y + muzzleOffsetY };
    Vector2 screenDelta = { target.x - muzzleBase.x, target.y - muzzleBase.y };

    float differenceX = screenDelta.x;
    float differenceY = screenDelta.y / PlanePerspectiveY;
    normalizeVelocity(&differenceX, &differenceY);

    float maxRange = ComputeMaxWorldRange(muzzleBase, gameWorldWidth, gameWorldHeight);
    float thetaLowRad = ToRadians(thetaLowDeg);

    float gravity = ComputeGravityForLowShot(maxRange, desiredFlightTimeLow, thetaLowRad);
    float maxV0 = ComputeMaxV0(gravity, desiredFlightTimeLow, thetaLowRad);

    float charge01 = std::clamp(chargeSeconds / maxChargeSeconds, 0.0f, 1.0f);
    float v0 = std::sqrt(charge01) * maxV0;
    float thetaRad = ToRadians(thetaDeg);

    float groundSpeed = v0 * std::cos(thetaRad);
    Vector2 velocity = { differenceX * groundSpeed, differenceY * groundSpeed };
    float vz = ComputeVerticalSpeed(chargeSeconds, maxChargeSeconds, maxV0, thetaDeg, thetaLowDeg);

    Vector2 aimScreenDirection = screenDelta;
    float lenSq = aimScreenDirection.x * aimScreenDirection.x + aimScreenDirection.y * aimScreenDirection.y;
    if (lenSq > 0.0001f) {
        float invLen = 1.0f / std::sqrt(lenSq);
        aimScreenDirection.x *= invLen;
        aimScreenDirection.y *= invLen;
    } else {
        aimScreenDirection = { 1.0f, 0.0f };
    }

    Vector2 start = {
        muzzleBase.x + aimScreenDirection.x * muzzleDistance,
        muzzleBase.y + aimScreenDirection.y * muzzleDistance
    };

    ShotLaunch launch;
    launch.start = start;
    launch.groundVelocity = velocity;
    launch.verticalSpeed = vz;
    launch.gravity = gravity;

    return launch;
}