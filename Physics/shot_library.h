#ifndef SHOT_PHYSICS_LIBRARY_H
#define SHOT_PHYSICS_LIBRARY_H

#include "library.h"

#ifdef _WIN32
    #ifdef PHYSICS_BUILD_DLL
        #define SHOT_PHYSICS_API __declspec(dllexport)
    #else
        #define SHOT_PHYSICS_API __declspec(dllimport)
    #endif
#else
    #define SHOT_PHYSICS_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
    Vector2 start;
    Vector2 groundVelocity;
    float verticalSpeed;
    float gravity;
} ShotLaunch;

SHOT_PHYSICS_API float PredictFullFlightSeconds(int chargeMs, float maxChargeSeconds, float desiredFlightTimeLow, float thetaDeg, float thetaLowDeg);
SHOT_PHYSICS_API float ComputeGravityForLowShot(float maxWorldRange, float desiredFlightTimeLow, float thetaLowRad);
SHOT_PHYSICS_API float ComputeMaxV0(float gravity, float desiredFlightTimeLow, float thetaLowRad);
SHOT_PHYSICS_API float ComputeVerticalSpeed(float chargeSeconds, float maxChargeSeconds, float maxV0, float thetaDeg, float thetaLowDeg);
SHOT_PHYSICS_API float ComputeMaxWorldRange(Vector2 muzzleBase, float gameWorldWidth, float gameWorldHeight);

SHOT_PHYSICS_API ShotLaunch ComputeLaunch(
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
);

#ifdef __cplusplus
}
#endif

#endif // SHOT_PHYSICS_LIBRARY_H