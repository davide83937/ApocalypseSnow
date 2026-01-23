#include "library.h"

#include <cmath>
#include <iostream>

float uniform_rectilinear_motion(float position, float velocity, float deltaTime) {
    return position + (velocity * deltaTime);
}

void normalizeVelocity(float* velX, float* velY) {
    // Calcoliamo la lunghezza del vettore (magnitudo)
    float length = std::sqrtf(*velX * *velX + *velY * *velY);

    // Evitiamo la divisione per zero se il pinguino è fermo
    if (length > 0.0f) {
        *velX /= length;
        *velY /= length;
    }
}

Vector2 parabolic_motion(float start_positionX, float start_positionY, float start_velocityX, float start_velocityY, float gameTime, float deltaTime) {
    gameTime += deltaTime;
    float positionX = start_positionX + (start_velocityX * gameTime);
    float positionY = -0.5f * 9.81f* pow(gameTime, 2)+ start_velocityY*gameTime + start_positionY;
    Vector2 p0 = {.x = positionX, .y = positionY};
    return p0;
}