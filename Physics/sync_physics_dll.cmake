# Script di distribuzione invocato dal POST_BUILD in CMakeLists.txt.
# Viene eseguito con "cmake -P": e' uno script CMake, non un nuovo progetto.
# Riceve PHYSICS_DLL (percorso del binario compilato) e PROJECT_ROOT.
#
# La DLL nasce in Shared: quella e' la copia di riferimento.
# La stessa DLL deve arrivare al server e al client, senza dover
# ricompilare anche il progetto C# per aggiornare la fisica.
#
# Il client puo' avere piu' cartelle (Debug, Release, versioni .NET).
# Cerchiamo le cartelle che contengono gia' ApocalypseSnow.dll:
# NON creiamo directory vuote o percorsi con versioni .NET scritte a mano.
# Se il client non e' ancora stato compilato, il suo .csproj copiera'
# automaticamente la DLL da Shared quando verra' compilato.

# Prima di copiare, controlla i parametri: FATAL_ERROR interrompe
# immediatamente la fase POST_BUILD, invece di lasciare DLL incoerenti.
if(NOT DEFINED PHYSICS_DLL OR NOT EXISTS "${PHYSICS_DLL}")
    message(FATAL_ERROR "DLL C++ mancante: ${PHYSICS_DLL}")
endif()
if(NOT DEFINED PROJECT_ROOT)
    message(FATAL_ERROR "PROJECT_ROOT non specificato")
endif()

# Una copia accanto al programma Go: Windows la carica da qui all'avvio.
# ONLY_IF_DIFFERENT confronta il contenuto e evita copie superflue.
# RESULT permette di diagnosticare errori, inclusi file in uso.
set(_server_dll "${PROJECT_ROOT}/Server/libPhysicsDll.dll")
file(COPY_FILE "${PHYSICS_DLL}" "${_server_dll}"
    ONLY_IF_DIFFERENT RESULT _server_result)
if(NOT _server_result STREQUAL "0")
    message(FATAL_ERROR "Impossibile aggiornare la DLL del server: ${_server_result}")
endif()
message(STATUS "DLL C++ aggiornata: Server")

# Una copia in ogni cartella dove esiste gia' l'eseguibile del client.
# Es.: ApocalypseSnow/bin/Debug/net9.0-windows/.
# Ricerca ricorsivamente ApocalypseSnow.dll sotto bin: ogni file trovato
# identifica una cartella di output reale del client (.NET Debug/Release).
# La DLL nativa verra' copiata ACCANTO a quell'assembly gestito.
file(GLOB_RECURSE _client_assemblies LIST_DIRECTORIES FALSE
    "${PROJECT_ROOT}/ApocalypseSnow/bin/ApocalypseSnow.dll")
list(LENGTH _client_assemblies _client_count)

# Aggiorna tutte le configurazioni gia' compilate, non solo Debug.
# Se una copia fallisce, il build deve segnalarlo esplicitamente.
foreach(_client_assembly IN LISTS _client_assemblies)
    get_filename_component(_client_dir "${_client_assembly}" DIRECTORY)
    file(COPY_FILE "${PHYSICS_DLL}" "${_client_dir}/libPhysicsDll.dll"
        ONLY_IF_DIFFERENT RESULT _client_result)
    if(NOT _client_result STREQUAL "0")
        message(FATAL_ERROR
            "Impossibile aggiornare la DLL in ${_client_dir}: ${_client_result}. "
            "Chiudere il gioco se la DLL e' in uso.")
    endif()
    message(STATUS "DLL C++ aggiornata: ${_client_dir}")
endforeach()

# Primo avvio su repository pulito: nessun output C# esistente.
# Non e' un errore: al primo build del client sara' il .csproj a copiare Shared.
if(_client_count EQUAL 0)
    message(STATUS "Client non ancora compilato: il .csproj copiera' la DLL da Shared.")
endif()
