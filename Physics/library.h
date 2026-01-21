#ifndef PHYSICS_LIBRARY_H
#define PHYSICS_LIBRARY_H

struct Vector2 {
    float x;
    float y;
};
// Questo pezzo serve per rendere la funzione visibile a MonoGame (C#)
extern "C" {
    __declspec(dllexport) float uniform_rectilinear_motion(float position, float velocity, float deltaTime);
    __declspec(dllexport) void normalizeVelocity(float* velX, float* velY);
}

#endif // PHYSICS_LIBRARY_H