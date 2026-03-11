package main

const (
	MoveHz    float32 = 30.0
	MoveDt    float32 = 1.0 / MoveHz
	MoveSpeed float32 = 200.0
)

func UniformMotion(position *float32, velocity, dt float32) {
	uniformRectilinearMotion(position, velocity, dt)
}

func NormalizeVelocity(velocityX, velocityY *float32) {
	normalizeVelocity(velocityX, velocityY)
}

func StepFromState(x, y float32, mask int32, dt float32) (float32, float32) {
	if (mask&Reload) != 0 || (mask&Freezing) != 0 || (mask&TakingEgg) != 0 || (mask&PuttingEgg) != 0 {
		return x, y
	}

	var dx float32 = 0
	var dy float32 = 0

	if (mask & Up) != 0 {
		dy -= 1
	}
	if (mask & Down) != 0 {
		dy += 1
	}
	if (mask & Left) != 0 {
		dx -= 1
	}
	if (mask & Right) != 0 {
		dx += 1
	}

	if dx == 0 && dy == 0 {
		return x, y
	}

	NormalizeVelocity(&dx, &dy)

	finalVx := dx * MoveSpeed
	finalVy := dy * MoveSpeed

	UniformMotion(&x, finalVx, dt)
	UniformMotion(&y, finalVy, dt)

	return x, y
}
