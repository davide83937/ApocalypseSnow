package main

/*
#cgo CFLAGS: -I../Shared
#cgo LDFLAGS: -L../Shared -l:libPhysicsDll.dll
#include "library.h"
*/
import "C"

import (
	"fmt"
	"net"
)

func main() {

	// Test DLL
fmt.Println(C.GoString(C.PhysicsBuildInfo()))

	// TCP minimale
	ln, err := net.Listen("tcp", ":8080")
	if err != nil {
		panic(err)
	}
	defer ln.Close()

	fmt.Println("Server listening on :8080")

	select {}
}