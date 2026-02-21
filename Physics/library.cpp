#include "library.h"
#include <cstring>
#include <cmath>
#include <iostream>

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

void check_collisions(CollisionDataIn* data, CollisionDataOut* dataOut, int count) {
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