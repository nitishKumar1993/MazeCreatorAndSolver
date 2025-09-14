using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to an empty GameObject. Press Play to generate a random grid and run the recursive backtracking solver.
/// OnDrawGizmos draws the grid and state. Use stepDelay to slow animation.
/// </summary>
public class MazeRecursiveVisualizer : MonoBehaviour
{
    [Header("Grid")]
    public int width = 21;   // odd or even works; keep it modest for visualization
    public int height = 15;
    public float cellSize = 0.6f;

    [Header("Random walls")]
    [Range(0f, 0.5f)] public float wallProbability = 0.25f; // density of random walls

    [Header("Solver/Visualization")]
    public Vector2Int start = new Vector2Int(0, 0);
    public Vector2Int goal;
    public float stepDelay = 0.05f; // pause between steps so you can see exploration

    // internal grid states
    private bool[,] isWall;
    private bool[,] visited;
    private bool[,] visiting;   // currently on recursion stack (entered but not returned)
    private bool[,] onPath;     // final path flagged after solution
    private Vector2Int[,] parent; // for reconstructing path
    private bool solved = false;
    private bool solving = false;

    void Start()
    {
        // clamp / ensure valid
        width = Mathf.Max(3, width);
        height = Mathf.Max(3, height);

        // default goal at bottom-right if not set
        goal = new Vector2Int(width - 1, height - 1);

        GenerateRandomGrid();
        StartCoroutine(RunSolver());
    }

    void GenerateRandomGrid()
    {
        isWall = new bool[width, height];
        visited = new bool[width, height];
        visiting = new bool[width, height];
        onPath = new bool[width, height];
        parent = new Vector2Int[width, height];

        System.Random rng = new System.Random();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                // Make borders walls for clarity
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    isWall[x, y] = false; // keep border walkable so solver can explore edges; you may toggle
                else
                    isWall[x, y] = (rng.NextDouble() < wallProbability);

                visited[x, y] = false;
                visiting[x, y] = false;
                onPath[x, y] = false;
                parent[x, y] = new Vector2Int(-1, -1);
            }

        // ensure start/goal are free
        start.x = Mathf.Clamp(start.x, 0, width - 1);
        start.y = Mathf.Clamp(start.y, 0, height - 1);
        goal = new Vector2Int(Mathf.Clamp(goal.x, 0, width - 1), Mathf.Clamp(goal.y, 0, height - 1));
        isWall[start.x, start.y] = false;
        isWall[goal.x, goal.y] = false;

        solved = false;
        solving = false;
    }

    IEnumerator RunSolver()
    {
        if (isWall == null) GenerateRandomGrid();
        solving = true;
        // clear any previous results
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                visited[x, y] = false;
                visiting[x, y] = false;
                onPath[x, y] = false;
                parent[x, y] = new Vector2Int(-1, -1);
            }
        solved = false;

        // start the recursive coroutine solver
        yield return StartCoroutine(SolveRecursive(start.x, start.y));

        // if solved reconstruct path
        if (solved)
        {
            ReconstructPath();
        }
        solving = false;
    }

    // Recursive solver implemented as a coroutine so we can yield each step for visualization.
    IEnumerator SolveRecursive(int x, int y)
    {
        // bounds / wall / visited checks
        if (x < 0 || y < 0 || x >= width || y >= height) yield break;
        if (isWall[x, y]) yield break;
        if (visited[x, y]) yield break;
        if (solved) yield break; // early exit if someone else found it

        // mark visiting (on recursion stack)
        visiting[x, y] = true;
        visited[x, y] = true;

        // Refresh view
        yield return new WaitForSeconds(stepDelay);

        // check goal
        if (x == goal.x && y == goal.y)
        {
            solved = true;
            yield break;
        }

        // choose directions in random order to make visual interesting
        List<Vector2Int> dirs = new List<Vector2Int>() {
            new Vector2Int(1,0),
            new Vector2Int(0,1),
            new Vector2Int(-1,0),
            new Vector2Int(0,-1)
        };
        // shuffle
        for (int i = 0; i < dirs.Count; i++)
        {
            int j = Random.Range(i, dirs.Count);
            var tmp = dirs[i]; dirs[i] = dirs[j]; dirs[j] = tmp;
        }

        foreach (var d in dirs)
        {
            int nx = x + d.x;
            int ny = y + d.y;

            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
            if (isWall[nx, ny]) continue;
            if (visited[nx, ny]) continue;
            if (solved) break;

            // register parent so we can reconstruct path if child finds goal
            parent[nx, ny] = new Vector2Int(x, y);

            // recursive call (as coroutine)
            yield return StartCoroutine(SolveRecursive(nx, ny));

            // if solved, bubble up immediately (do not unmark visiting so final path reconstruction can happen)
            if (solved) yield break;
        }

        // no neighbor led to solution -> backtrack: unmark visiting (remove from recursion stack)
        visiting[x, y] = false;

        // small delay so user sees backtrack visually
        yield return new WaitForSeconds(stepDelay);
    }

    void ReconstructPath()
    {
        Vector2Int cur = goal;
        while (cur.x >= 0 && cur.y >= 0)
        {
            onPath[cur.x, cur.y] = true;
            if (cur == start) break;
            Vector2Int p = parent[cur.x, cur.y];
            if (p.x == -1 && p.y == -1) break;
            cur = p;
        }
    }

    // Draw grid and states
    void OnDrawGizmos()
    {
        if (width <= 0 || height <= 0) return;
        // center grid around this object
        Vector3 origin = transform.position - new Vector3(width * cellSize * 0.5f, height * cellSize * 0.5f, 0);

        // draw each cell
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 cellCenter = origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0);

                // default color
                Color col = Color.white;

                if (isWall != null && isWall.GetLength(0) == width && isWall.GetLength(1) == height && isWall[x, y])
                    col = Color.black;
                else if (onPath != null && onPath.GetLength(0) == width && onPath.GetLength(1) == height && onPath[x, y])
                    col = Color.green;
                else if (visiting != null && visiting.GetLength(0) == width && visiting.GetLength(1) == height && visiting[x, y])
                    col = Color.yellow;
                else if (visited != null && visited.GetLength(0) == width && visited.GetLength(1) == height && visited[x, y])
                    col = Color.gray;
                else
                    col = Color.white * 0.9f;

                // start/goal highlight
                if (x == start.x && y == start.y) col = Color.blue;
                if (x == goal.x && y == goal.y) col = Color.red;

                Gizmos.color = col;
                Gizmos.DrawCube(cellCenter, new Vector3(cellSize * 0.95f, cellSize * 0.95f, 0.01f));

                // draw border
                Gizmos.color = Color.black * 0.35f;
                Gizmos.DrawWireCube(cellCenter, new Vector3(cellSize * 0.95f, cellSize * 0.95f, 0.01f));
            }
        }

#if UNITY_EDITOR
        // Safe, layout-less GUI drawing for Scene/Game view
        try
        {
            UnityEditor.Handles.BeginGUI();

            // Use explicit rects (no GUILayout) so Unity's layout state is not affected.
            float left = 10f, top = 10f, widthRect = 300f, lineHeight = 18f;
            GUI.Label(new Rect(left, top + 0 * lineHeight, widthRect, lineHeight), "Recursive Backtracking Visualizer");
            GUI.Label(new Rect(left, top + 1 * lineHeight, widthRect, lineHeight), $"Grid: {width} x {height}, walls={wallProbability:F2}");
            GUI.Label(new Rect(left, top + 2 * lineHeight, widthRect, lineHeight), $"Step delay: {stepDelay:F2}s");
            GUI.Label(new Rect(left, top + 3 * lineHeight, widthRect, lineHeight), "Colors:");
            GUI.Label(new Rect(left + 10f, top + 4 * lineHeight, widthRect, lineHeight), "Blue = Start");
            GUI.Label(new Rect(left + 10f, top + 5 * lineHeight, widthRect, lineHeight), "Red = Goal");
            GUI.Label(new Rect(left + 10f, top + 6 * lineHeight, widthRect, lineHeight), "Yellow = Visiting (recursion stack)");
            GUI.Label(new Rect(left + 10f, top + 7 * lineHeight, widthRect, lineHeight), "Gray = Visited (dead-end)");
            GUI.Label(new Rect(left + 10f, top + 8 * lineHeight, widthRect, lineHeight), "Green = Final path");

            UnityEditor.Handles.EndGUI();
        }
        catch (System.Exception)
        {
            // swallow to avoid spamming console in weird editor states
            // (you can log once if needed)
        }
#endif
    }

    // Optional: restart with new random walls at runtime (call from inspector button or other script)
    [ContextMenu("Regenerate & Solve")]
    void RegenerateAndSolve()
    {
        GenerateRandomGrid();
        StopAllCoroutines();
        StartCoroutine(RunSolver());
    }
}
