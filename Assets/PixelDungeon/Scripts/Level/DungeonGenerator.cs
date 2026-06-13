using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelDungeon
{
    public class Dungeon
    {
        public List<Room> Rooms;
        public Room Start;
        public Vector2 StartPosition;
        public Transform Root;
    }

    /// <summary>
    /// Room-graph floor generator (design doc 3.6 / 4.4). Places rooms on a coarse grid, connects
    /// neighbours with corridors, paints floor+walls, then spawns Room objects, doorway doors and
    /// per-type content. Start room is the entrance; the graph-farthest room becomes the boss.
    /// </summary>
    public class DungeonGenerator
    {
        private const int RW = 13, RH = 9, GAP = 5;
        private const int PitchX = RW + GAP, PitchY = RH + GAP;

        public Dungeon Generate(int floorIndex, GameDatabase db)
        {
            var theme = db.GetTheme(floorIndex);
            var painter = new TilePainter(theme.floor, theme.wall);

            int roomCount = Mathf.Clamp(6 + floorIndex, 6, 10);
            var cells = PlaceCells(roomCount, out var edges);
            var types = AssignTypes(cells, edges);

            var rects = cells.ToDictionary(c => c, RectFor);

            var floor = new HashSet<Vector2Int>();
            foreach (var r in rects.Values) AddRect(floor, r);
            foreach (var (a, b) in edges) Carve(floor, rects[a], rects[b], a, b);

            painter.Paint(floor);

            var rooms = new List<Room>();
            Room start = null;
            foreach (var c in cells)
            {
                var go = new GameObject($"Room_{types[c]}_{c.x}_{c.y}");
                go.transform.SetParent(painter.Root, false);
                var room = go.AddComponent<Room>();
                room.Setup(rects[c], types[c], floorIndex);
                foreach (var dc in RingFloorCells(rects[c], floor))
                    room.Doors.Add(Door.Create(dc, theme.wall, painter.Root));
                rooms.Add(room);
                if (types[c] == RoomType.Start) start = room;
            }

            foreach (var r in rooms) r.Populate();

            return new Dungeon { Rooms = rooms, Start = start, StartPosition = start.Center, Root = painter.Root };
        }

        // ---------- Geometry ----------

        private static RectInt RectFor(Vector2Int c) => new(c.x * PitchX, c.y * PitchY, RW, RH);

        private static void AddRect(HashSet<Vector2Int> set, RectInt r)
        {
            for (int x = r.xMin; x < r.xMax; x++)
            for (int y = r.yMin; y < r.yMax; y++)
                set.Add(new Vector2Int(x, y));
        }

        private static void Carve(HashSet<Vector2Int> floor, RectInt A, RectInt B, Vector2Int a, Vector2Int b)
        {
            if (a.x != b.x)
            {
                var L = a.x < b.x ? A : B;
                var R = a.x < b.x ? B : A;
                int yc = L.yMin + RH / 2;
                for (int x = L.xMax; x <= R.xMin - 1; x++)
                    for (int dy = -1; dy <= 1; dy++) floor.Add(new Vector2Int(x, yc + dy));
            }
            else
            {
                var Bot = a.y < b.y ? A : B;
                var Top = a.y < b.y ? B : A;
                int xc = Bot.xMin + RW / 2;
                for (int y = Bot.yMax; y <= Top.yMin - 1; y++)
                    for (int dx = -1; dx <= 1; dx++) floor.Add(new Vector2Int(xc + dx, y));
            }
        }

        private static IEnumerable<Vector2Int> RingFloorCells(RectInt r, HashSet<Vector2Int> floor)
        {
            var ring = new List<Vector2Int>();
            for (int x = r.xMin - 1; x <= r.xMax; x++)
            {
                ring.Add(new Vector2Int(x, r.yMin - 1));
                ring.Add(new Vector2Int(x, r.yMax));
            }
            for (int y = r.yMin - 1; y <= r.yMax; y++)
            {
                ring.Add(new Vector2Int(r.xMin - 1, y));
                ring.Add(new Vector2Int(r.xMax, y));
            }
            return ring.Where(floor.Contains).Distinct();
        }

        // ---------- Graph ----------

        private static readonly Vector2Int[] Dirs =
            { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        private List<Vector2Int> PlaceCells(int count, out List<(Vector2Int, Vector2Int)> edges)
        {
            var placed = new List<Vector2Int> { Vector2Int.zero };
            var set = new HashSet<Vector2Int> { Vector2Int.zero };
            edges = new List<(Vector2Int, Vector2Int)>();
            var edgeSet = new HashSet<(Vector2Int, Vector2Int)>();

            int guard = 0;
            while (placed.Count < count && guard++ < 1000)
            {
                var from = placed[Random.Range(0, placed.Count)];
                var n = from + Dirs[Random.Range(0, 4)];
                if (set.Contains(n)) continue;
                set.Add(n);
                placed.Add(n);
                AddEdge(edges, edgeSet, from, n);
            }

            // A few extra connections between adjacent rooms to create loops.
            foreach (var c in placed)
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    if (set.Contains(n) && Random.value < 0.18f) AddEdge(edges, edgeSet, c, n);
                }

            return placed;
        }

        private static void AddEdge(List<(Vector2Int, Vector2Int)> edges, HashSet<(Vector2Int, Vector2Int)> seen,
            Vector2Int a, Vector2Int b)
        {
            var key = a.GetHashCode() <= b.GetHashCode() ? (a, b) : (b, a);
            if (seen.Add(key)) edges.Add((a, b));
        }

        private Dictionary<Vector2Int, RoomType> AssignTypes(List<Vector2Int> cells, List<(Vector2Int, Vector2Int)> edges)
        {
            var adj = cells.ToDictionary(c => c, _ => new List<Vector2Int>());
            foreach (var (a, b) in edges) { adj[a].Add(b); adj[b].Add(a); }

            var start = Vector2Int.zero;
            var dist = BFS(start, adj);
            var boss = cells.Where(c => c != start).OrderByDescending(c => dist[c]).First();

            var types = cells.ToDictionary(c => c, _ => RoomType.Combat);
            types[start] = RoomType.Start;
            types[boss] = RoomType.Boss;

            var leaves = cells.Where(c => c != start && c != boss && adj[c].Count == 1).ToList();
            var others = cells.Where(c => c != start && c != boss && adj[c].Count > 1).ToList();

            void Assign(RoomType t)
            {
                Vector2Int pick;
                if (leaves.Count > 0) { pick = leaves[Random.Range(0, leaves.Count)]; leaves.Remove(pick); }
                else if (others.Count > 0) { pick = others[Random.Range(0, others.Count)]; others.Remove(pick); }
                else return;
                types[pick] = t;
            }
            Assign(RoomType.Shop);
            Assign(RoomType.Treasure);

            return types;
        }

        private static Dictionary<Vector2Int, int> BFS(Vector2Int start, Dictionary<Vector2Int, List<Vector2Int>> adj)
        {
            var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var n in adj[c])
                    if (!dist.ContainsKey(n)) { dist[n] = dist[c] + 1; q.Enqueue(n); }
            }
            return dist;
        }
    }
}
