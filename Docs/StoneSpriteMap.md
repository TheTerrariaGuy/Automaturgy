# Stone sprite map

Assign sprites in Assets/Rendering/ElementSprites.asset. Keys are `tile type * 100 + variant`. Base stone entries reference RockWall sprites; spent stone inherits them through the family fallback. Types 400 and 401 connect to each other. Lava (410/411), empty cells, and cells outside the grid do not connect.

`40000` / `40100` are the default surface keys. Clearing a shape entry uses its corresponding default. Shape entries take priority over these defaults.

## Surface shapes

Rows below are mini 3-by-3 neighborhoods, read top to bottom (north to south), with `/` separating rows. `@` is this tile, `#` is connected stone, `.` is an empty/non-stone neighbor, and `?` is a diagonal that does not affect this shape. A diagonal matters only when both adjacent cardinal neighbors are stone. This yields 47 distinct shapes, including thin L/T/cross connections, filled corners, inner corners, edges, and solid interiors.

These IDs describe topology, **not the numeric suffixes of RockWall sprite names**. Pick the artwork matching each neighborhood. The RockWall sheet includes its own south-facing rock faces. The scene uses Wall Height = 0 to display those tiles without a vertical offset.

| 400 key | 401 key | Neighborhood (N / center / S) | Connections / filled diagonals | Assigned sprite |
|---|---|---|---|---|
| 40002 | 40102 | `?.?/.@./?.?` | Isolated; filled diagonals: none | RockWall_34 |
| 40003 | 40103 | `?#?/.@./?.?` | N; filled diagonals: none | RockWall_23 |
| 40004 | 40104 | `?.?/.@#/?.?` | E; filled diagonals: none | RockWall_31 |
| 40005 | 40105 | `?#./.@#/?.?` | N, E; filled diagonals: none | RockWall_35 |
| 40006 | 40106 | `?##/.@#/?.?` | N, E; filled diagonals: NE | RockWall_20 |
| 40007 | 40107 | `?.?/.@./?#?` | S; filled diagonals: none | RockWall_3 |
| 40008 | 40108 | `?#?/.@./?#?` | N, S; filled diagonals: none | RockWall_13 |
| 40009 | 40109 | `?.?/.@#/?#.` | E, S; filled diagonals: none | RockWall_4 |
| 40010 | 40110 | `?#./.@#/?#.` | N, E, S; filled diagonals: none | RockWall_42 |
| 40011 | 40111 | `?##/.@#/?#.` | N, E, S; filled diagonals: NE | RockWall_14 |
| 40012 | 40112 | `?.?/.@#/?##` | E, S; filled diagonals: SE | RockWall_0 |
| 40013 | 40113 | `?#./.@#/?##` | N, E, S; filled diagonals: SE | RockWall_24 |
| 40014 | 40114 | `?##/.@#/?##` | N, E, S; filled diagonals: NE, SE | RockWall_10 |
| 40015 | 40115 | `?.?/#@./?.?` | W; filled diagonals: none | RockWall_33 |
| 40016 | 40116 | `.#?/#@./?.?` | N, W; filled diagonals: none | RockWall_38 |
| 40017 | 40117 | `?.?/#@#/?.?` | E, W; filled diagonals: none | RockWall_32 |
| 40018 | 40118 | `.#./#@#/?.?` | N, E, W; filled diagonals: none | RockWall_39 |
| 40019 | 40119 | `.##/#@#/?.?` | N, E, W; filled diagonals: NE | RockWall_37 |
| 40020 | 40120 | `?.?/#@./.#?` | S, W; filled diagonals: none | RockWall_7 |
| 40021 | 40121 | `.#?/#@./.#?` | N, S, W; filled diagonals: none | RockWall_46 |
| 40022 | 40122 | `?.?/#@#/.#.` | E, S, W; filled diagonals: none | RockWall_8 |
| 40023 | 40123 | `.#./#@#/.#.` | N, E, S, W; filled diagonals: none | RockWall_45 |
| 40024 | 40124 | `.##/#@#/.#.` | N, E, S, W; filled diagonals: NE | RockWall_40 |
| 40025 | 40125 | `?.?/#@#/.##` | E, S, W; filled diagonals: SE | RockWall_6 |
| 40026 | 40126 | `.#./#@#/.##` | N, E, S, W; filled diagonals: SE | RockWall_29 |
| 40027 | 40127 | `.##/#@#/.##` | N, E, S, W; filled diagonals: NE, SE | RockWall_44 |
| 40028 | 40128 | `?.?/#@./##?` | S, W; filled diagonals: SW | RockWall_2 |
| 40029 | 40129 | `.#?/#@./##?` | N, S, W; filled diagonals: SW | RockWall_27 |
| 40030 | 40130 | `?.?/#@#/##.` | E, S, W; filled diagonals: SW | RockWall_5 |
| 40031 | 40131 | `.#./#@#/##.` | N, E, S, W; filled diagonals: SW | RockWall_30 |
| 40032 | 40132 | `.##/#@#/##.` | N, E, S, W; filled diagonals: NE, SW | RockWall_19 |
| 40033 | 40133 | `?.?/#@#/###` | E, S, W; filled diagonals: SE, SW | RockWall_1 |
| 40034 | 40134 | `.#./#@#/###` | N, E, S, W; filled diagonals: SE, SW | RockWall_28 |
| 40035 | 40135 | `.##/#@#/###` | N, E, S, W; filled diagonals: NE, SE, SW | RockWall_26 |
| 40036 | 40136 | `##?/#@./?.?` | N, W; filled diagonals: NW | RockWall_22 |
| 40037 | 40137 | `##./#@#/?.?` | N, E, W; filled diagonals: NW | RockWall_36 |
| 40038 | 40138 | `###/#@#/?.?` | N, E, W; filled diagonals: NE, NW | RockWall_21 |
| 40039 | 40139 | `##?/#@./.#?` | N, S, W; filled diagonals: NW | RockWall_17 |
| 40040 | 40140 | `##./#@#/.#.` | N, E, S, W; filled diagonals: NW | RockWall_41 |
| 40041 | 40141 | `###/#@#/.#.` | N, E, S, W; filled diagonals: NE, NW | RockWall_18 |
| 40042 | 40142 | `##./#@#/.##` | N, E, S, W; filled diagonals: SE, NW | RockWall_9 |
| 40043 | 40143 | `###/#@#/.##` | N, E, S, W; filled diagonals: NE, SE, NW | RockWall_16 |
| 40044 | 40144 | `##?/#@./##?` | N, S, W; filled diagonals: SW, NW | RockWall_12 |
| 40045 | 40145 | `##./#@#/##.` | N, E, S, W; filled diagonals: SW, NW | RockWall_43 |
| 40046 | 40146 | `###/#@#/##.` | N, E, S, W; filled diagonals: NE, SW, NW | RockWall_15 |
| 40047 | 40147 | `##./#@#/###` | N, E, S, W; filled diagonals: SE, SW, NW | RockWall_25 |
| 40048 | 40148 | `###/#@#/###` | N, E, S, W; filled diagonals: NE, SE, SW, NW | RockWall_11 |

Changing a cell refreshes it and all eight neighbors, including diagonal corners. Repeated combat updates select shapes from the current gameplay grid, not partially updated Tile components.
