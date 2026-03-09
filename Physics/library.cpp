#include "library.h"
#include <cstring>
#include <cmath>
#include <algorithm>


const char* PhysicsBuildInfo() {
    return "PhysicsDLL build: " __DATE__ " " __TIME__;
}

void uniform_rectilinear_motion(float *position, float velocity, float deltaTime) {
    *position = *position + (velocity * deltaTime);
}

void normalizeVelocity(float* velX, float* velY) {
    // Calcoliamo la lunghezza del vettore (magnitudo)
    float length = std::sqrt(*velX * *velX + *velY * *velY);

    // Evitiamo la divisione per zero se il pinguino è fermo
    if (length > 0.0f) {
        *velX /= length;
        *velY /= length;
    }
}

void parabolic_motion(float gravity,float start_positionX, float start_positionY, float* positionX, float* positionY,  float start_velocityX, float start_velocityY, float gameTime) {
    *positionX = start_positionX + (start_velocityX * gameTime);
    *positionY = 0.5f * gravity * pow(gameTime, 2)+ start_velocityY*gameTime + start_positionY;
}


void check_collisions(CollisionDataIn* data, CollisionDataOut* dataOut, int count) {
    int found = 0;
    for (int i = 0; i < count; i++) {
        for (int j = i + 1; j < count; j++) {
            // Controlla i tag per evitare collisioni tra oggetti dello stesso tipo (es. player vs player)
            if (strcmp(data[i].tag, data[j].tag) != 0) {

                // Distanza tra i centri
                float dx = data[i].x - data[j].x;
                float dy = data[i].y - data[j].y;

                // Somma delle mezze larghezze e mezze altezze
                float combinedHalfWidths = (data[i].width / 2.0f) + (data[j].width / 2.0f);
                float combinedHalfHeights = (data[i].height / 2.0f) + (data[j].height / 2.0f);

                // Verifica se c'è sovrapposizione su entrambi gli assi
                if (std::abs(dx) < combinedHalfWidths && std::abs(dy) < combinedHalfHeights) {

                    // Calcola quanto "profonda" è la collisione su ogni asse
                    float overlapX = combinedHalfWidths - std::abs(dx);
                    float overlapY = combinedHalfHeights - std::abs(dy);

                    strcpy(dataOut[found].myTag, data[i].tag);
                    strcpy(dataOut[found].otherTag, data[j].tag);

                    // La direzione della collisione è l'asse con la sovrapposizione minore
                    if (overlapX < overlapY) {
                        // Collisione Orizzontale (Left o Right)
                        if (dx > 0) {
                            dataOut[found].type = COLLISION_RIGHT; // L'oggetto i è a destra dell'oggetto j
                        } else {
                            dataOut[found].type = COLLISION_LEFT;  // L'oggetto i è a sinistra dell'oggetto j
                        }
                    } else {
                        // Collisione Verticale (Top o Bottom)
                        if (dy > 0) {
                            dataOut[found].type = COLLISION_TOP;    // L'oggetto i è sopra l'oggetto j
                        } else {
                            dataOut[found].type = COLLISION_BOTTOM; // L'oggetto i è sotto l'oggetto j
                        }
                    }
                    found++;
                }
            }
        }
    }
}


float calculate_ball_scale_only(float startX, float startY, float finalX, float finalY, float posX, float startSpeedX, float L, float K, float currentScale) {
    float differenceY = std::abs(startY - finalY);
    float x = L * (1.0f - std::exp(-K * differenceY));
    float midpoint = (finalX + startX + 48.0f) / 2.0f;

    if (startSpeedX > 0) { // Destra
        return (posX < midpoint) ? (currentScale + x) : (currentScale - x);
    } else { // Sinistra
        return (posX > midpoint) ? (currentScale + x) : (currentScale - x);
    }
}


float Distance(Vector2 a, Vector2 b) {
    float dx = a.x - b.x;
    float dy = a.y - b.y;
    return std::sqrt(dx * dx + dy * dy);
}

Vector2 Lerp(Vector2 a, Vector2 b, float t) {
    if (t < 0.0f) t = 0.0f;
    if (t > 1.0f) t = 1.0f;

    Vector2 result;
    result.x = a.x + (b.x - a.x) * t;
    result.y = a.y + (b.y - a.y) * t;
    return result;
}


float LerpFloat(float v0, float v1, float t) {
    // Assicura che t sia tra 0 e 1 come avviene nel codice C#
    t = std::clamp(t, 0.0f, 1.0f);
    return v0 + t * (v1 - v0);
}

Vector2 calculateScreenPosition(Vector2 startPos, float worldDeltaX, float worldDeltaY, float z) {
    Vector2 screenPos;

    // X rimane lineare
    screenPos.x = startPos.x + worldDeltaX;

    // Y combina la prospettiva del piano e l'innalzamento dato dalla quota Z
    screenPos.y = startPos.y + (PlanePerspectiveY * worldDeltaY) - (HeightProjection * z);

    return screenPos;
}

/**
 * Esempio di calcolo della scala visiva basata sull'altezza
 */
float calculateVisualScale(float z, float maxHeight, float scaleMin, float scaleMax, float* outAlpha) {
    float alpha = (maxHeight > 0.0001f) ? (z / maxHeight) : 0.0f;
    if (alpha > 1.0f) alpha = 1.0f;
    *outAlpha = alpha; // "Esporta" il valore
    return std::lerp(scaleMin, scaleMax, alpha);
}