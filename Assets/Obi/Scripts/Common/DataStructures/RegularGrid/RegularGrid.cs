using System;
using System.Collections.Generic;
using UnityEngine;

namespace Obi
{
    public class RegularGrid<T>
    {
        private Dictionary<Vector3Int, List<T>> gridMap = new Dictionary<Vector3Int, List<T>>();
        private float cellSize;
        private Func<T, Vector3> getPosition;

        public RegularGrid(float cellSize, Func<T, Vector3> getPosition)
        {
            this.cellSize = cellSize;

            if (getPosition != null)
                this.getPosition = getPosition;
            else
                getPosition = (x) => { return Vector3.zero; };
        }

        public Vector3Int GetCellCoords(Vector3 pos)
        {
            return new Vector3Int(Mathf.FloorToInt(pos.x / cellSize),
                                  Mathf.FloorToInt(pos.y / cellSize),
                                  Mathf.FloorToInt(pos.z / cellSize));
        }

        public void AddElement(T elm)
        {
            var coords = GetCellCoords(getPosition(elm));
            if (gridMap.TryGetValue(coords, out List<T> cell))
                cell.Add(elm);
            else
                gridMap[coords] = new List<T> { elm };
        }

        public bool RemoveElement(T elm)
        {
            var coords = GetCellCoords(getPosition(elm));
            if (gridMap.TryGetValue(coords, out List<T> cell))
            {
                return cell.Remove(elm);
            }
            return false;
        }

        public IEnumerable<T> GetNeighborsEnumerator(T elm)
        {
            // if cells are infinitesimaly small,
            // elements should have no neighbors.
            if (cellSize < ObiUtils.epsilon)
                yield break;

            var position = getPosition(elm);
            var coords = GetCellCoords(position);
            List<T> cell;

            for (int x = -1; x <= 1; ++x)
                for (int y = -1; y <= 1; ++y)
                    for (int z = -1; z <= 1; ++z)
                    {
                        if (gridMap.TryGetValue(coords + new Vector3Int(x,y,z), out cell))
                        {
                            foreach(T n in cell)
                            {
                                if (n.Equals(elm))
                                    continue;

                                float dist = Vector3.Distance(position, getPosition(n));
                                if (dist <= cellSize)
                                    yield return n;
                            }
                        }
                    }
        }

        // TODO: single call passing position and element to ignore.
        public IEnumerable<T> GetNeighborsEnumerator(Vector3 position)
        {
            // if cells are infinitesimaly small,
            // elements should have no neighbors.
            if (cellSize < ObiUtils.epsilon)
                yield break;

            var coords = GetCellCoords(position);
            List<T> cell;

            for (int x = -1; x <= 1; ++x)
                for (int y = -1; y <= 1; ++y)
                    for (int z = -1; z <= 1; ++z)
                    {
                        if (gridMap.TryGetValue(coords + new Vector3Int(x, y, z), out cell))
                        {
                            foreach (T n in cell)
                            {
                                float dist = Vector3.Distance(position, getPosition(n));
                                if (dist <= cellSize)
                                    yield return n;
                            }
                        }
                    }
        }
    }
}
