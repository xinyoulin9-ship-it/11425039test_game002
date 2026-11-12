using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small, self-contained falling-block game. It draws with Unity's IMGUI so
/// the first version needs no imported art, prefabs, or scene wiring.
/// </summary>
public sealed class BlockDropGame : MonoBehaviour
{
    private const int Width = 10;
    private const int Height = 20;
    private const float DropInterval = 0.55f;

    private enum Shape { I, O, T, S, Z, J, L }
    private struct Piece
    {
        public Shape shape;
        public int rotation;
        public int x;
        public int y;
    }

    private readonly Shape[,] board = new Shape[Width, Height];
    private readonly bool[,] occupied = new bool[Width, Height];
    private readonly List<Shape> bag = new List<Shape>();
    private Piece active;
    private Shape next;
    private bool hasActive;
    private bool hasStarted;
    private bool gameOver;
    private bool paused;
    private int score;
    private int lines;
    private float dropTimer;
    private float repeatTimer;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle buttonStyle;

    private static readonly Vector2Int[][] BaseCells =
    {
        new[] { new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(3,1) }, // I
        new[] { new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(1,1), new Vector2Int(2,1) }, // O
        new[] { new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) }, // T
        new[] { new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1) }, // S
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(2,1) }, // Z
        new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) }, // J
        new[] { new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) }  // L
    };

    private static readonly Color[] PieceColors =
    {
        new Color(0.15f, 0.85f, 0.95f), new Color(0.98f, 0.82f, 0.20f),
        new Color(0.72f, 0.38f, 0.95f), new Color(0.30f, 0.85f, 0.48f),
        new Color(0.95f, 0.30f, 0.38f), new Color(0.30f, 0.52f, 0.98f),
        new Color(1.00f, 0.57f, 0.22f)
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateGame()
    {
        var existingGame = FindFirstObjectByType<BlockDropGame>();
        if (existingGame != null)
        {
            existingGame.PrepareStartScreen();
            return;
        }
        var gameObject = new GameObject("BlockDrop Game");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<BlockDropGame>();
    }

    private void Awake() => PrepareStartScreen();

    private void PrepareStartScreen()
    {
        Array.Clear(occupied, 0, occupied.Length);
        Array.Clear(board, 0, board.Length);
        bag.Clear();
        score = 0;
        lines = 0;
        gameOver = false;
        paused = false;
        hasStarted = false;
        hasActive = false;
        dropTimer = 0f;
        repeatTimer = 0f;
        next = DrawFromBag();
    }

    private void StartNewGame()
    {
        Array.Clear(occupied, 0, occupied.Length);
        Array.Clear(board, 0, board.Length);
        bag.Clear();
        score = 0;
        lines = 0;
        gameOver = false;
        paused = false;
        hasStarted = true;
        dropTimer = 0f;
        next = DrawFromBag();
        SpawnPiece();
    }

    private void Update()
    {
        if (!hasStarted)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartNewGame();
            return;
        }
        if (Input.GetKeyDown(KeyCode.R)) { StartNewGame(); return; }
        if (Input.GetKeyDown(KeyCode.P) && !gameOver) paused = !paused;
        if (gameOver || paused || !hasActive) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) TryMove(-1, 0);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) TryMove(1, 0);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) RotatePiece();
        if (Input.GetKeyDown(KeyCode.Space)) HardDrop();

        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
        {
            repeatTimer += Time.deltaTime;
            if (repeatTimer >= 0.07f) { StepDown(); repeatTimer = 0f; }
        }
        else repeatTimer = 0f;

        dropTimer += Time.deltaTime;
        float interval = Mathf.Max(0.08f, DropInterval - (lines / 10) * 0.045f);
        if (dropTimer >= interval) { StepDown(); dropTimer = 0f; }
    }

    private void OnGUI()
    {
        float cell = Mathf.Clamp(Mathf.Min(Screen.height / 22.5f, Screen.width / 28f), 16f, 52f);
        EnsureStyles(cell);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);

        float boardWidth = Width * cell;
        float boardHeight = Height * cell;
        float panelWidth = Mathf.Max(150f, cell * 6.2f);
        float gap = cell * 1.0f;
        float totalWidth = boardWidth + gap + panelWidth;
        float left = Mathf.Max(12f, (Screen.width - totalWidth) * 0.5f);
        float top = Mathf.Clamp((Screen.height - boardHeight) * 0.5f + cell * 0.65f, cell * 1.30f, Screen.height - boardHeight - cell * 0.15f);

        DrawText(new Rect(left, top - cell * 1.35f, totalWidth, cell), "BLOCK DROP", titleStyle);
        DrawBoard(left, top, cell);
        DrawPanel(left + boardWidth + gap, top, panelWidth, cell);

        if (!hasStarted)
        {
            Rect overlay = new Rect(left, top + boardHeight * 0.40f, boardWidth, cell * 2.1f);
            DrawRect(overlay, new Color(0.03f, 0.05f, 0.09f, 0.95f));
            DrawText(new Rect(overlay.x, overlay.y + 4f, overlay.width, cell * 0.85f), "準備好了嗎？", titleStyle);
            DrawText(new Rect(overlay.x, overlay.y + cell * 0.95f, overlay.width, cell * 0.75f), "按 Enter 開始遊戲", labelStyle);
        }
        else if (paused || gameOver)
        {
            Rect overlay = new Rect(left, top + boardHeight * 0.40f, boardWidth, cell * 1.8f);
            DrawRect(overlay, new Color(0.03f, 0.05f, 0.09f, 0.93f));
            string message = gameOver ? "遊戲結束" : "暫停中";
            DrawText(new Rect(overlay.x, overlay.y + 4f, overlay.width, cell * 0.85f), message, titleStyle);
            DrawText(new Rect(overlay.x, overlay.y + cell * 0.95f, overlay.width, cell * 0.65f), "按 R 重新開始", smallStyle);
        }
    }

    private void DrawBoard(float x, float y, float cell)
    {
        DrawRect(new Rect(x - 3, y - 3, Width * cell + 6, Height * cell + 6), new Color(0.15f, 0.20f, 0.30f));
        for (int row = 0; row < Height; row++)
        for (int col = 0; col < Width; col++)
        {
            Rect rect = new Rect(x + col * cell, y + row * cell, cell - 1, cell - 1);
            if (occupied[col, row]) DrawCell(rect, board[col, row], 1f);
            else DrawRect(rect, new Color(0.075f, 0.095f, 0.14f));
        }

        if (hasActive && !gameOver)
        {
            Piece ghost = active;
            while (CanPlace(ghost.x, ghost.y + 1, ghost.rotation)) ghost.y++;
            foreach (var c in GetCells(ghost.shape, ghost.rotation))
            {
                int gx = ghost.x + c.x, gy = ghost.y + c.y;
                if (gy >= 0 && gy < Height) DrawCell(new Rect(x + gx * cell, y + gy * cell, cell - 1, cell - 1), ghost.shape, 0.20f);
            }
            foreach (var c in GetCells(active.shape, active.rotation))
            {
                int bx = active.x + c.x, by = active.y + c.y;
                if (by >= 0 && by < Height) DrawCell(new Rect(x + bx * cell, y + by * cell, cell - 1, cell - 1), active.shape, 1f);
            }
        }
    }

    private void DrawPanel(float x, float y, float width, float cell)
    {
        DrawText(new Rect(x, y, width, 28f), "分數", labelStyle);
        DrawText(new Rect(x, y + 30f, width, 36f), score.ToString("N0"), titleStyle);
        DrawText(new Rect(x, y + 78f, width, 28f), "消除列數　" + lines, labelStyle);
        DrawText(new Rect(x, y + 122f, width, 25f), "下一個", labelStyle);
        float previewCell = cell * 0.72f;
        Vector2Int[] nextCells = GetCells(next, 0);
        int minX = 9, minY = 9, maxX = 0, maxY = 0;
        foreach (var c in nextCells) { minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y); maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y); }
        float previewWidth = (maxX - minX + 1) * previewCell;
        float previewLeft = x + (width - previewWidth) * 0.5f;
        float previewTop = y + 158f;
        foreach (var c in nextCells)
            DrawCell(new Rect(previewLeft + (c.x - minX) * previewCell, previewTop + (c.y - minY) * previewCell, previewCell - 1, previewCell - 1), next, 1f);

        float controlsY = previewTop + 90f;
        DrawText(new Rect(x, controlsY, width, 22f), "操作方式", labelStyle);
        DrawText(new Rect(x, controlsY + 29f, width, 95f), "← → / A D　移動\n↑ / W　旋轉\n↓ / S　加速下降\n空白鍵　直接落下\nP　暫停", smallStyle);
        float buttonY = controlsY + 138f;
        if (GUI.Button(new Rect(x, buttonY, width, 36f), paused ? "繼續 (P)" : "暫停 (P)", buttonStyle)) paused = !paused;
        if (GUI.Button(new Rect(x, buttonY + 44f, width, 36f), "重新開始 (R)", buttonStyle)) StartNewGame();
    }

    private void EnsureStyles(float cell)
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.80f, 0.87f, 0.98f) } };
            smallStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.74f, 0.80f, 0.92f) } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }
        titleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(cell * 0.70f, 20f, 34f));
        labelStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(cell * 0.40f, 14f, 21f));
        smallStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(cell * 0.33f, 12f, 17f));
        buttonStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(cell * 0.36f, 13f, 18f));
    }

    private static void DrawText(Rect rect, string text, GUIStyle style) { GUI.color = Color.white; GUI.Label(rect, text, style); }
    private static void DrawRect(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
    private static void DrawCell(Rect rect, Shape shape, float alpha)
    {
        Color color = PieceColors[(int)shape];
        color.a = alpha;
        DrawRect(rect, new Color(0.02f, 0.04f, 0.07f, alpha));
        Rect inset = new Rect(rect.x + 2, rect.y + 2, Mathf.Max(1, rect.width - 4), Mathf.Max(1, rect.height - 4));
        DrawRect(inset, color);
        DrawRect(new Rect(inset.x + 2, inset.y + 2, Mathf.Max(1, inset.width - 4), 3), new Color(1f, 1f, 1f, 0.20f * alpha));
    }

    private static Vector2Int[] GetCells(Shape shape, int rotation)
    {
        var source = BaseCells[(int)shape];
        var result = new Vector2Int[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            int x = source[i].x, y = source[i].y;
            for (int r = 0; r < rotation; r++) { int oldX = x; x = 2 - y; y = oldX; }
            result[i] = new Vector2Int(x, y);
        }
        return result;
    }

    private bool CanPlace(int x, int y, int rotation)
    {
        foreach (var c in GetCells(active.shape, rotation))
        {
            int bx = x + c.x, by = y + c.y;
            if (bx < 0 || bx >= Width || by >= Height) return false;
            if (by >= 0 && occupied[bx, by]) return false;
        }
        return true;
    }

    private void TryMove(int dx, int dy)
    {
        if (CanPlace(active.x + dx, active.y + dy, active.rotation)) { active.x += dx; active.y += dy; }
    }

    private void RotatePiece()
    {
        int rotation = (active.rotation + 1) % 4;
        int[] kicks = { 0, -1, 1, -2, 2 };
        foreach (int kick in kicks)
        {
            if (CanPlace(active.x + kick, active.y, rotation)) { active.x += kick; active.rotation = rotation; return; }
            if (CanPlace(active.x + kick, active.y - 1, rotation)) { active.x += kick; active.y--; active.rotation = rotation; return; }
        }
    }

    private void StepDown()
    {
        if (CanPlace(active.x, active.y + 1, active.rotation)) active.y++;
        else LockPiece();
    }

    private void HardDrop()
    {
        while (CanPlace(active.x, active.y + 1, active.rotation)) active.y++;
        LockPiece();
    }

    private void LockPiece()
    {
        foreach (var c in GetCells(active.shape, active.rotation))
        {
            int bx = active.x + c.x, by = active.y + c.y;
            if (by < 0) { gameOver = true; hasActive = false; return; }
            occupied[bx, by] = true;
            board[bx, by] = active.shape;
        }
        hasActive = false;
        ClearLines();
        SpawnPiece();
    }

    private void ClearLines()
    {
        int cleared = 0;
        for (int y = Height - 1; y >= 0; y--)
        {
            bool full = true;
            for (int x = 0; x < Width; x++) if (!occupied[x, y]) { full = false; break; }
            if (!full) continue;
            cleared++;
            for (int row = y; row > 0; row--)
            for (int x = 0; x < Width; x++) { occupied[x, row] = occupied[x, row - 1]; board[x, row] = board[x, row - 1]; }
            for (int x = 0; x < Width; x++) { occupied[x, 0] = false; board[x, 0] = Shape.I; }
            y++;
        }
        if (cleared > 0)
        {
            int[] points = { 0, 100, 300, 500, 800 };
            score += points[cleared] * (1 + lines / 10);
            lines += cleared;
        }
    }

    private void SpawnPiece()
    {
        active = new Piece { shape = next, rotation = 0, x = 3, y = -1 };
        next = DrawFromBag();
        hasActive = true;
        if (!CanPlace(active.x, active.y, active.rotation)) { gameOver = true; hasActive = false; }
    }

    private Shape DrawFromBag()
    {
        if (bag.Count == 0)
        {
            foreach (Shape shape in Enum.GetValues(typeof(Shape))) bag.Add(shape);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Shape temp = bag[i]; bag[i] = bag[j]; bag[j] = temp;
            }
        }
        Shape result = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        return result;
    }
}
