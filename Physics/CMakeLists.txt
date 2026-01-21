cmake_minimum_required(VERSION 4.1)
project(Physics)

set(CMAKE_CXX_STANDARD 20)

add_library(PhysicsDll SHARED library.cpp library.h)
target_link_libraries(PhysicsDll PRIVATE "-static")
