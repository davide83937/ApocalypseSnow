cmake_minimum_required(VERSION 4.1)
project(Physics)

set(CMAKE_CXX_STANDARD 20)

# Unica libreria dinamica che racchiude sia la fisica base che la balistica dello sparo,
# ma mantenendo i file sorgente separati per ordine logico.
add_library(PhysicsDll SHARED
        library.cpp
        library.h
        shot_library.cpp
        shot_library.h
)

target_compile_definitions(PhysicsDll PRIVATE PHYSICS_BUILD_DLL)

set_target_properties(PhysicsDll PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../Shared
        OUTPUT_NAME "PhysicsDll"
)

# Copia automatica di entrambi gli header nella cartella Shared
add_custom_command(TARGET PhysicsDll POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${CMAKE_SOURCE_DIR}/library.h
        ${CMAKE_SOURCE_DIR}/../Shared/library.h
)

add_custom_command(TARGET PhysicsDll POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${CMAKE_SOURCE_DIR}/shot_library.h
        ${CMAKE_SOURCE_DIR}/../Shared/shot_library.h
)