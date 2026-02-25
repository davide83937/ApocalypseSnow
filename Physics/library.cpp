#include "library.h"
#include <cstring>
#include <cmath>
#include <iostream>
#include <algorithm>


const char* PhysicsBuildInfo() {
    return "PhysicsDLL build: " __DATE__ " " __TIME__;
}

void uniform_rectilinear_motion(float *position, float velocity, float deltaTime) {
    *position = *position + (velocity * deltaTime);
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

void parabolic_motion(float gravity,float start_positionX, float start_positionY, float* positionX, float* positionY,  float start_velocityX, float start_velocityY, float gameTime) {
    *positionX = start_positionX + (start_velocityX * gameTime);
    *positionY = 0.5f * gravity * pow(gameTime, 2)+ start_velocityY*gameTime + start_positionY;
}
/*
void check_collisions1(CollisionDataIn* data, CollisionDataOut* dataOut, int count) {
    int found = 0;
    //std::cout << "Dentro il metodo check_collisions" << std::endl;
    for (int i = 0; i< count; i++) {
        int widthI = (data[i].width/2);
        int heightI = (data[i].height/2);
        int widthLeftI = data[i].x - widthI;
        int widthRightI = data[i].x + widthI;
        int heightUpI = data[i].y + heightI;
        int heightDownI = data[i].y - heightI;
        for (int j = i+1; j< count; j++) {
            if (strcmp(data[i].tag, data[j].tag) != 0) {
                int width = (data[j].width/2);
                int height = (data[j].height/2);
                int widthLeft = data[j].x - width;
                int widthRight = data[j].x + width;
                int heightUp = data[j].y + height;
                int heightDown = data[j].y - height;
                //std::cout << "WidthLeft è " << widthLeft << std::endl;
                //std::cout << "WidthRight è " << widthRight << std::endl;
                //std::cout << "heightUp è " << heightUp << std::endl;
                //std::cout << "heightDown è " << heightDown << std::endl;
                if (widthRightI >= widthLeft && widthLeftI <= widthRight && heightDownI <= heightUp && heightUpI >= heightDown) {
                    //std::cout << "Collisione rilevata" << std::endl;
                    //std::cout << "data[i]: " << data[i].x<< std::endl;
                    //std::cout << "WidthLeft è " << widthLeft << std::endl;
                    //std::cout << "WidthRight è " << widthRight << std::endl;
                    strcpy(dataOut[found].myTag, data[i].tag);
                    strcpy(dataOut[found].otherTag, data[j].tag);
                    dataOut[found].type = 1;
                    found++;
                }
            }
        }
    }
}
*/
/*
void check_collisions2(CollisionDataIn* data, CollisionDataOut* dataOut, CollisionOrder* dataOrder, int count, int countOrder) {
    int found = 0;
    bool isLeft = false;
    bool isRight = false;
    bool isUp = false;
    bool isDown = false;
    //std::cout << "Dentro il metodo check_collisions" << std::endl;
    for (int i = 0; i< count; i++) {
        int widthI = (data[i].width/2);
        int heightI = (data[i].height/2);
        int widthLeftI = data[i].x - widthI;
        int widthRightI = data[i].x + widthI;
        int heightUpI = data[i].y + heightI;
        int heightDownI = data[i].y - heightI;
        for (int j = i+1; j< count; j++) {
            if (strcmp(data[i].tag, data[j].tag) != 0) {
                isLeft = false;
                isRight = false;
                isUp = false;
                isDown = false;
                int width = (data[j].width/2);
                int height = (data[j].height/2);
                int widthLeft = data[j].x - width;
                int widthRight = data[j].x + width;
                int heightUp = data[j].y + height;
                int heightDown = data[j].y - height;
                //std::cout << "WidthLeft è " << widthLeft << std::endl;
                //std::cout << "WidthRight è " << widthRight << std::endl;
                //std::cout << "heightUp è " << heightUp << std::endl;
                //std::cout << "heightDown è " << heightDown << std::endl;
                if (widthRightI >= widthLeft) {isLeft = true;}
                if (widthLeftI <= widthRight) {isRight = true;}
                if (heightDownI <= heightUp) {isUp = true;}
                if (heightUpI >= heightDown) {isDown = true;}

                for (int k = 0; k < countOrder; k++) {
                    if (strcmp(dataOrder[k].myTag, data[i].tag) == 0 && strcmp(dataOrder[k].otherTag, data[j].tag) == 0) {
                        if (isLeft) {dataOrder[k].traceLeft++;}else{dataOrder[k].traceLeft=0;}
                        if (isRight) {dataOrder[k].traceRight++;}else{dataOrder[k].traceRight=0;}
                        if (isUp) {dataOrder[k].traceUp++;}else{dataOrder[k].traceUp=0;}
                        if (isDown) {dataOrder[k].traceDown++;}else{dataOrder[k].traceDown=0;}
                    }
                }

                if (isLeft && isRight && isUp && isDown) {
                    //std::cout << "Collisione rilevata" << std::endl;
                    //std::cout << "data[i]: " << data[i].x<< std::endl;
                    //std::cout << "WidthLeft è " << widthLeft << std::endl;
                    //std::cout << "WidthRight è " << widthRight << std::endl;
                    std::string quale = "n";
                    for (int k = 0; k < countOrder; k++) {
                        if (strcmp(dataOrder[k].myTag, data[i].tag) == 0 && strcmp(dataOrder[k].otherTag, data[j].tag) == 0) {
                            auto min_pair = std::min({
                                 std::pair{dataOrder->traceLeft, "l"},
                                 std::pair{dataOrder->traceRight, "r"},
                                 std::pair{dataOrder->traceDown, "d"},
                                 std::pair{dataOrder->traceUp, "u"}
                                                    });

                            int minimo = min_pair.first;
                            quale = min_pair.second;
                        }
                    }
                    if (strcmp(quale.c_str(), "l")==0) {dataOut[found].type = 1;}  //SINISTRA
                    if (strcmp(quale.c_str(), "r")==0) {dataOut[found].type = 2;}  //DESTRA
                    if (strcmp(quale.c_str(), "d")==0) {dataOut[found].type = 3;}  //BASSO
                    if (strcmp(quale.c_str(), "u")==0) {dataOut[found].type = 4;}  //ALTO
                    strcpy(dataOut[found].myTag, data[i].tag);
                    strcpy(dataOut[found].otherTag, data[j].tag);
                    //dataOut[found].type = 1;
                    found++;
                }


            }
        }
    }
}
*/
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
