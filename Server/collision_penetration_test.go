package main

import (
	"math"
	"testing"
)

// I punti di contatto sono le stesse fixture usate nel test nativo del client C#.
func TestPenetrationResolutionMatchesClientAndStaysStable(t *testing.T) {
	cm := NewCollisionManager()
	cm.AddObject("o0", 150, 150, 100, 100)
	cm.AddObject("penguin1", 52, 150, penguinTextureFractionWidth, penguinHalfTextureFractionHeight)

	tests := []struct {
		name                     string
		centerX, centerY         float32
		wantCenterX, wantCenterY float32
	}{
		{"left", 80, 150, 52, 150},
		{"right", 220, 150, 248, 150},
		{"top", 150, 95, 150, 68},
		{"bottom", 150, 205, 150, 232},
		{"tangent", 248, 150, 248, 150},
	}
	for _, tc := range tests {
		x, y := cm.ResolvePlayerObstacleOverlaps("penguin1",
			tc.centerX-penguinHalfTextureFractionWidth,
			tc.centerY-penguinHalfTextureFractionHeight)
		wantX := tc.wantCenterX - penguinHalfTextureFractionWidth
		wantY := tc.wantCenterY - penguinHalfTextureFractionHeight
		if math.Abs(float64(x-wantX)) > 0.001 || math.Abs(float64(y-wantY)) > 0.001 {
			t.Errorf("%s: got (%.6f,%.6f), want (%.6f,%.6f)", tc.name, x, y, wantX, wantY)
		}
	}

	// 120 input consecutivi verso il muro: nessun tremolio, e niente collider spostati.
	x, y := float32(4), float32(86)
	for tick := 0; tick < 120; tick++ {
		x, y = cm.ResolvePlayerObstacleOverlaps("penguin1", x+MoveSpeed*MoveDt, y)
		if math.Abs(float64(x-4)) > 0.001 || math.Abs(float64(y-86)) > 0.001 {
			t.Fatalf("tick %d: pinguino trema: (%.6f,%.6f)", tick, x, y)
		}
	}
	if cm.collisionRecordIns[0].X != 150 || cm.collisionRecordIns[0].Y != 150 {
		t.Fatal("Il risolutore ha modificato il collider dell'ostacolo")
	}
}
