#ifndef PHYSICS_LIBRARY_H
#define PHYSICS_LIBRARY_H

#ifdef _WIN32
    #ifdef PHYSICS_BUILD_DLL
        #define PHYSICS_API __declspec(dllexport)
    #else
        #define PHYSICS_API __declspec(dllimport)
    #endif
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

    struct CollisionOrder {
        char myTag[16];
        char otherTag[16];
        short traceLeft;
        short traceRight;
        short traceUp;
        short traceDown;
    };

    // Aggiungi questo in library.h
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

    //mettiamo una pezza, la via pulita sarebbe usare extern qui e valorizzare le variabili all'interno del file cpp
    #define PlanePerspectiveY 0.70f
    #define HeightProjection 0.35f

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

    //PHYSICS_API void check_collisions2(CollisionDataIn* data, CollisionDataOut2* dataOut, int count);

#ifdef __cplusplus
}
#endif

#endif // PHYSICS_LIBRARY_H