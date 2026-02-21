#ifndef PHYSICS_LIBRARY_H
#define PHYSICS_LIBRARY_H

// Macro export (Windows only, minimale)
#ifdef _WIN32
    #define PHYSICS_API __declspec(dllexport)
#else
    #define PHYSICS_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

    struct CollisionDataIn {
        char tag[16];
        float x;
        float y;
        int width;
        int height;
    };

    struct CollisionDataOut {
        char myTag[16];
        char otherTag[16];
        int type;
    };

    // --- API ---
    PHYSICS_API const char* PhysicsBuildInfo();
    PHYSICS_API void uniform_rectilinear_motion(float *position, float velocity, float deltaTime);
    PHYSICS_API void normalizeVelocity(float* velX, float* velY);
    PHYSICS_API void parabolic_motion(
        float gravity,
        float start_positionX, float start_positionY,
        float* positionX, float* positionY,
        float start_velocityX, float start_velocityY,
        float gameTime
    );
    PHYSICS_API void check_collisions(
        struct CollisionDataIn* data,
        struct CollisionDataOut* dataOut,
        int count
    );

#ifdef __cplusplus
}
#endif

#endif // PHYSICS_LIBRARY_H