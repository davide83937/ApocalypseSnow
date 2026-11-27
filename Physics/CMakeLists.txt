cmake_minimum_required(VERSION 4.1)
project(Physics)

set(CMAKE_CXX_STANDARD 20)

# Unica libreria CONDIVISA con fisica base e balistica, in file separati.
# SHARED e' la scelta corretta: la DLL puo' essere caricata da C# e Go,
# ma rimane collegabile anche da eventuali altri progetti C++.
# Su MinGW viene generato anche un .dll.a, che NON dobbiamo distribuire.
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

# Il compilatore MinGW genera anche build_native/libPhysicsDll.dll.a.
# Questo .a e' una libreria di importazione: NON e' la fisica del gioco.
# Con '#cgo LDFLAGS: -L../Shared -l:libPhysicsDll.dll' Go usa la DLL
# direttamente, quindi il .a puo' rimanere tra i file temporanei di CMake.
# Verificato con 'go build -a' e test Go anche senza il .a.
# Dopo OGNI compilazione della DLL aggiorniamo anche le copie usate dal gioco:
# - Shared/libPhysicsDll.dll: file prodotto da questa build (copia di riferimento);
# - Server/libPhysicsDll.dll: serve quando avviamo il programma Go;
# - ApocalypseSnow/bin/.../libPhysicsDll.dll: serve al client C# gia' compilato.
# Lo script cerca tutte le cartelle Debug/Release del client esistenti,
# senza fissare a mano il percorso di una versione specifica di .NET.
# Il .csproj mantiene la sua copia da Shared per la PRIMA compilazione del client.
# Se una DLL e' bloccata dal gioco in esecuzione, il build segnala l'errore.
add_custom_command(TARGET PhysicsDll POST_BUILD
        COMMAND ${CMAKE_COMMAND}
            "-DPHYSICS_DLL=$<TARGET_FILE:PhysicsDll>"
            "-DPROJECT_ROOT=${CMAKE_SOURCE_DIR}/.."
            -P "${CMAKE_CURRENT_SOURCE_DIR}/sync_physics_dll.cmake"
        VERBATIM
)