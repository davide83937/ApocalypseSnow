#include "library.h"
#include <cstring>
#include <cmath>
#include <algorithm>

// Integratore di moto rettilineo uniforme su un singolo asse.
// L'aggiornamento e' in-place; deltaTime e' espresso in secondi.
void uniform_rectilinear_motion(float *position, float velocity, float deltaTime) {
    *position = *position + (velocity * deltaTime);
}

// Porta la velocita' 2D a modulo 1 conservandone la direzione.
// Serve, per esempio, a non accelerare muovendosi in diagonale.
void normalizeVelocity(float* velX, float* velY) {
    // Calcoliamo la lunghezza del vettore (magnitudo)
    float length = std::sqrt(*velX * *velX + *velY * *velY);

    // Evitiamo la divisione per zero se il pinguino è fermo
    if (length > 0.0f) {
        *velX /= length;
        *velY /= length;
    }
}

/*void parabolic_motion(float gravity,float start_positionX, float start_positionY, float* positionX, float* positionY,  float start_velocityX, float start_velocityY, float gameTime) {
    *positionX = start_positionX + (start_velocityX * gameTime);
    *positionY = 0.5f * gravity * pow(gameTime, 2)+ start_velocityY*gameTime + start_positionY;
}*/



// Per ogni coppia i<j controlliamo la sovrapposizione AABB nel piano XY.
void check_collisions(CollisionDataIn* data, CollisionDataOut* dataOut, int count) {
    int found = 0;
    for (int i = 0; i < count; i++) {
        for (int j = i + 1; j < count; j++) {
            // In questa implementazione si escludono tag IDENTICI:
            // il filtro riguarda la stringa esatta, non una classe di oggetti.
            // Il collider nativo ignora completamente le quote Z.
            if (strcmp(data[i].tag, data[j].tag) != 0) {

                // Scarti tra i CENTRI delle hitbox (non tra i vertici).
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

                    // L'asse con penetrazione minima indica la correzione
                    // meno invasiva; la risoluzione e' lasciata al chiamante.
                    // Con overlapX == overlapY prevale l'asse verticale (else).
                    dataOut[found].penetration = std::min(overlapX, overlapY);
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


// Distanza euclidea: sqrt((ax-bx)^2 + (ay-by)^2).
float Distance(Vector2 a, Vector2 b) {
    float dx = a.x - b.x;
    float dy = a.y - b.y;
    return std::sqrt(dx * dx + dy * dy);
}

// Interpolazione lineare fra a e b; t e' limitato a [0,1].
// Per t=0 restituisce a, per t=1 restituisce b.
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

// Proiezione dal moto nel mondo alle coordinate dello schermo.
// X conserva lo spostamento reale. Su Y agiscono due effetti diversi:
// - lo spostamento sul piano viene compresso da PlanePerspectiveY;
// - la quota Z alza il proiettile sullo schermo (Y diminuisce).
// Client e server la usano per condividere la stessa posizione proiettata.
Vector2 calculateScreenPosition(Vector2 startPos, float worldDeltaX, float worldDeltaY, float z) {
    Vector2 screenPos;

    // X rimane lineare
    screenPos.x = startPos.x + worldDeltaX;

    // Y combina la prospettiva del piano e l'innalzamento dato dalla quota Z
    screenPos.y = startPos.y + (PlanePerspectiveY * worldDeltaY) - (HeightProjection * z);

    return screenPos;
}

// Solo resa grafica: l'altezza viene rapportata alla quota massima
// per ottenere un fattore di interpolazione e un alpha.
// Il limite superiore e' 1; i valori negativi non vengono corretti qui.
float calculateVisualScale(float z, float maxHeight, float scaleMin, float scaleMax, float* outAlpha) {
    float alpha = (maxHeight > 0.0001f) ? (z / maxHeight) : 0.0f;
    if (alpha > 1.0f) alpha = 1.0f;
    *outAlpha = alpha; // "Esporta" il valore
    return std::lerp(scaleMin, scaleMax, alpha);
}

// Moto del proiettile: uniforme sul piano XY, uniformemente accelerato in Z.
// La quota parte da zero, sale e poi scende sotto l'azione della gravita'.
// Dopo l'atterraggio z viene bloccata a 0; i delta XY non vengono arrestati:
// spetta al chiamante interrompere la simulazione al tick di arrivo.
void calculate_parabolic_motion(
    float t,
    float groundVelocityWorldX, float groundVelocityWorldY,
    float startVerticalVelocity, float gravity,
    float* worldDeltaX, float* worldDeltaY, float* z
) {
    *worldDeltaX = groundVelocityWorldX * t;
    *worldDeltaY = groundVelocityWorldY * t;

    *z = (startVerticalVelocity * t) - (0.5f * gravity * t * t);
    if (*z < 0.0f) {
        *z = 0.0f;
    }
}