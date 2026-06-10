# PlateData

Converted movement and geometry data for the tectonics simulator.

Source: Merdith et al. 2021 ESR v1.1  
Zenodo: https://zenodo.org/records/12525401

This source is useful for our project because it is a published GPlates plate reconstruction model. It includes plate rotations, static polygons, and topology boundaries, so it gives us real historical data instead of dummy movement values.

## Files

`MerdithPlateRotations.json`

Main movement input. Converted from `1000_0_rotfile_Merdith_etal.rot`. Each record stores the moving plate, time in Ma, rotation pole, rotation angle, and fixed reference plate.

`MerdithStaticPolygons.json`

Static polygon geometry converted from GPML. This helps connect plate IDs to real geographic regions and can be used for grid setup or visual checks.

`MerdithTopologyBoundaries.json`

Topology boundary lines converted from GPML. These records include boundary names, time ranges, left/right plate IDs when available, and lat/lon points.

## Notes

The JSON files are Unity-friendly, but they are not wired into gameplay yet. The next step is to write a loader that reads the rotation records and applies them to the simulation grid.
