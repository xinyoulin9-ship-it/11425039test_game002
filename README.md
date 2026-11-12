
# Block Drop

A beginner-friendly 2D falling-block puzzle game built with Unity 6. The game uses Unity's built-in IMGUI, so it has no external art or package dependencies.

## Play online（一次成功）

Play the game at [Block Drop on GitHub Pages](https://xinyoulin9-ship-it.github.io/11425039test_game001/).

## Controls

- Press **Enter** to start.
- **Left / Right** or **A / D**: move
- **Up** or **W**: rotate
- **Down** or **S**: move down faster
- **Space**: hard drop
- **P**: pause
- **R**: restart

## Included

A 10 by 20 board, all seven tetrominoes, collision and locking, line clears, score, next-piece preview, ghost landing position, pause, restart, and keyboard controls.

## Open in Unity

Add this repository folder as an existing project in Unity Hub and open it with Unity 6 `6000.0.83f1` or a compatible Unity 6 editor.

## Build for GitHub Pages

Open the project in Unity and select **Build → Build WebGL for GitHub Pages**. The web build is written to `docs/` and uses Brotli compression with Unity decompression fallback so it can run on static hosts such as GitHub Pages.
