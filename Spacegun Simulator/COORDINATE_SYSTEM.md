# Coordinate System Standard

## Overview

This project uses a **right-handed Cartesian coordinate system** aligned with standard aerospace and 3D graphics conventions.

## Axes

| Axis | Positive Direction | Negative Direction |
|------|---|---|
| **X** | East | West |
| **Y** | North | South |
| **Z** | Up | Down |

## Azimuth (Bearing from North, Clockwise)

Azimuth is measured clockwise from North, matching standard compass/navigation conventions.

| Azimuth | Direction |
|---|---|
| **0°** | North (+Y) |
| **90°** | East (+X) |
| **180°** | South (-Y) |
| **270°** | West (-X) |

## Elevation (Angle from Horizontal Plane)

Elevation is measured from the horizontal plane (XY plane) toward the zenith or nadir.

| Elevation | Direction |
|---|---|
| **0°** | Horizontal (XY plane) |
| **90°** | Straight up (+Z) |
| **-90°** | Straight down (-Z) |
| **45°** | Halfway between horizontal and straight up |

## Conversion Formulas

### Angles to Cartesian (Polar to Rectangular)

Given elevation θ, azimuth φ, and distance r:

horizontal_distance = r × cos(θ)
z = r × sin(θ)
x = horizontal_distance × sin(φ)
y = horizontal_distance × cos(φ)

### Cartesian to Angles (Rectangular to Polar)

Given x, y, z:

elevation = arctan2(z, √(x² + y²))
azimuth = arctan2(x, y)
distance = √(x² + y² + z²)

Note: Azimuth must be normalized to [0°, 360°) range.

## Example Conversions

### Example 1: Target due North

- Azimuth: 0°
- Elevation: 30°
- Distance: 1000 km
- Result: (0, 866, 500) km

### Example 2: Target due East

- Azimuth: 90°
- Elevation: 30°
- Distance: 1000 km
- Result: (866, 0, 500) km

### Example 3: Target Northeast at Horizon

- Azimuth: 45°
- Elevation: 0°
- Distance: 1414 km
- Result: (1000, 1000, 0) km

## Implementation Notes

- Azimuth range: [0°, 360°)
- Elevation range: [-90°, 90°]
- All trigonometric calculations use radians internally
- Conversion from degrees to radians: radians = degrees × π / 180
- Conversion from radians to degrees: degrees = radians × 180 / π

## Code Implementation

All angle-to-Cartesian and Cartesian-to-angle conversions in the codebase use these formulas:

### Key Methods

- FiringSolution.AnglesToCartesian() - Converts (elevation, azimuth, distance) to (x, y, z)
- FiringSolution.CartesianToAngles() - Converts (x, y, z) to (elevation, azimuth)
- FireSimulator.CalculateProjectilePosition() - Calculates projectile trajectory in Cartesian space
- TargetMotionComputer.CalculatePositionAtTime() - Calculates target position using velocity vector

## References

- Right-handed Cartesian system: Standard in physics, aerospace, and 3D graphics
- Azimuth/bearing convention: Standard in navigation and compass bearings
- Elevation convention: Standard in aerospace and ballistics calculations