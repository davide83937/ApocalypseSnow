# Configurazione della sola libreria nativa di ApocalypseSnow.
# Si apre questa cartella (Physics) direttamente in CLion.
# CMake genera la DLL caricata sia dal client C# sia dal server Go.
cmake_minimum_required(VERSION 4.1)
project(Physics)

# Richiesto per la libreria standard moderna usata dai calcoli C++
# (per esempio std::numbers per convertire gradi in radianti).
set(CMAKE_CXX_STANDARD 20)

# Unica libreria CONDIVISA con fisica base e balistica, in file separati.
add_library(PhysicsDll SHARED
        library.cpp
        library.h
        shot_library.cpp
        shot_library.h
)
#message(STATUS "Compilatore C++: ${CMAKE_CXX_COMPILER}")
#message(STATUS "ID compilatore: ${CMAKE_CXX_COMPILER_ID}")
#message(STATUS "Versione: ${CMAKE_CXX_COMPILER_VERSION}")
# Mentre compiliamo la DLL, PHYSICS_BUILD_DLL fa espandere PHYSICS_API
# in __declspec(dllexport). Nei programmi che la usano resta dllimport.
# PRIVATE: non propaga la macro ai programmi collegati alla libreria.
target_compile_definitions(PhysicsDll PRIVATE PHYSICS_BUILD_DLL)

# La DLL finale si chiama libPhysicsDll.dll su MinGW e viene scritta
# in Shared (cartella comune alle sorgenti C#, C++ e Go).
set_target_properties(PhysicsDll PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../Shared
        OUTPUT_NAME "PhysicsDll"
)

# Dopo ogni build, copia in Shared gli header usati dal server Go.
# POST_BUILD garantisce l'ordine: prima la compilazione, poi le copie.
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

# Il compilatore MinGW genera anche build_native/libPhysicsDll.dll.a.
# $<TARGET_FILE:PhysicsDll> e' una generator expression di CMake:
# risolve al percorso esatto della DLL prodotta dalla build corrente.
# VERBATIM preserva correttamente gli argomenti e i percorsi con spazi.
# Il .csproj mantiene la sua copia da Shared per la PRIMA compilazione del client.
# Se una DLL e' bloccata dal gioco in esecuzione, il build segnala l'errore.
add_custom_command(TARGET PhysicsDll POST_BUILD
        COMMAND ${CMAKE_COMMAND}
            "-DPHYSICS_DLL=$<TARGET_FILE:PhysicsDll>"
            "-DPROJECT_ROOT=${CMAKE_SOURCE_DIR}/.."
            -P "${CMAKE_CURRENT_SOURCE_DIR}/sync_physics_dll.cmake"
        VERBATIM
)