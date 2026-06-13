using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PixelDungeon
{
    /// <summary>
    /// Builds the floor and wall tilemaps for a generated floor (design doc 3.6 / 4.5). Floor tiles
    /// are painted on walkable cells; wall tiles are auto-placed on every cell bordering the floor,
    /// so non-door edges are always sealed. Wall tilemap carries the collider + WallMarker.
    /// </summary>
    public class TilePainter
    {
        public Transform Root { get; }
        public Tilemap Floor { get; }
        public Tilemap Wall { get; }

        private readonly TileBase _floorTile;
        private readonly TileBase _wallTile;

        public TilePainter(Sprite floorSprite, Sprite wallSprite)
        {
            var gridGo = new GameObject("Dungeon");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            Root = gridGo.transform;

            Floor = MakeLayer("Floor", -20, false, out _);
            Wall = MakeLayer("Wall", -10, true, out var wallGo);
            wallGo.AddComponent<WallMarker>();

            _floorTile = CreateTile(floorSprite, Tile.ColliderType.None);
            _wallTile = CreateTile(wallSprite, Tile.ColliderType.Grid);
        }

        private Tilemap MakeLayer(string name, int sortingOrder, bool collider, out GameObject go)
        {
            go = new GameObject(name);
            go.transform.SetParent(Root, false);
            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingOrder = sortingOrder;
            if (collider) go.AddComponent<TilemapCollider2D>();
            return tm;
        }

        private static TileBase CreateTile(Sprite sprite, Tile.ColliderType collider)
        {
            var t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = sprite;
            t.colliderType = collider;
            return t;
        }

        public void Paint(HashSet<Vector2Int> floorCells)
        {
            foreach (var c in floorCells)
                Floor.SetTile(new Vector3Int(c.x, c.y, 0), _floorTile);

            var wallCells = new HashSet<Vector2Int>();
            foreach (var c in floorCells)
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (!floorCells.Contains(n)) wallCells.Add(n);
                }

            foreach (var c in wallCells)
                Wall.SetTile(new Vector3Int(c.x, c.y, 0), _wallTile);
        }
    }
}
