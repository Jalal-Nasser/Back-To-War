using UnityEngine;

namespace CossacksRTS.Net
{
    /// <summary>
    /// Deterministic world->network quantization helpers.
    ///
    /// Chosen scale:
    /// - 1 tile = 1000 milli-tiles (mt)
    /// - Serialized command positions are int32 in mt.
    /// - Commands snap to tile center by default.
    /// </summary>
    public static class Quantization
    {
        public const int MilliTilesPerTile = 1000;
        public const int HalfTileMilli = MilliTilesPerTile / 2;

        // Unity world units per tile in this project. Keep fixed across clients.
        public const float TileWorldUnits = 1f;

        public static QuantizedPosition2Int FromWorldPosition(float worldX, float worldZ)
        {
            int tileX = Mathf.FloorToInt(worldX / TileWorldUnits);
            int tileY = Mathf.FloorToInt(worldZ / TileWorldUnits);
            return SnapTileCenter(tileX, tileY);
        }

        public static QuantizedPosition2Int FromWorldPoint(Vector3 worldPoint)
        {
            return FromWorldPosition(worldPoint.x, worldPoint.z);
        }

        public static QuantizedPosition2Int SnapTileCenter(int tileX, int tileY)
        {
            return new QuantizedPosition2Int(
                tileX * MilliTilesPerTile + HalfTileMilli,
                tileY * MilliTilesPerTile + HalfTileMilli);
        }

        public static Vector3 ToWorldPoint(QuantizedPosition2Int pos, float y = 0f)
        {
            float worldX = pos.x_mt / (float)MilliTilesPerTile;
            float worldZ = pos.y_mt / (float)MilliTilesPerTile;
            return new Vector3(worldX, y, worldZ);
        }
    }
}
