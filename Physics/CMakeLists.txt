cmake_minimum_required(VERSION 4.1)
project(Physics)

set(CMAKE_CXX_STANDARD 20)

add_library(PhysicsDll SHARED library.cpp library.h)
set_target_properties(PhysicsDll PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../Shared
        OUTPUT_NAME "PhysicsDll"
)