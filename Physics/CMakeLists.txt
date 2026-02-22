cmake_minimum_required(VERSION 4.1)
project(Physics)

set(CMAKE_CXX_STANDARD 20)

add_library(PhysicsDll SHARED library.cpp library.h)

#Serve per far capire all'header che stiamo COMPILANDO la DLL:
#quindi PHYSICS_API deve diventare __declspec(dllexport) e non dllimport.
target_compile_definitions(PhysicsDll PRIVATE PHYSICS_BUILD_DLL)

set_target_properties(PhysicsDll PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../Shared
        OUTPUT_NAME "PhysicsDll"
)

#Copia l'header nella cartella Shared dopo il build
add_custom_command(TARGET PhysicsDll POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${CMAKE_SOURCE_DIR}/library.h
        ${CMAKE_SOURCE_DIR}/../Shared/library.h
)