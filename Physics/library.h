#ifndef PHYSICS_LIBRARY_H
#define PHYSICS_LIBRARY_H

struct Vector2 {
    float x;
    float y;
};
// Questo pezzo serve per rendere la funzione visibile a MonoGame (C#)
extern "C" {
    __declspec(dllexport) void uniform_rectilinear_motion(float *position, float velocity, float deltaTime);
    __declspec(dllexport) void normalizeVelocity(float* velX, float* velY);
    __declspec(dllexport) Vector2 parabolic_motion(float gravity, float start_positionX, float start_positionY, float start_velocityX, float start_velocityY, float gameTime);
}

#endif // PHYSICS_LIBRARY_H