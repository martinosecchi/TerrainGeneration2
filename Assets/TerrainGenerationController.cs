using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TerrainGenerationController : MonoBehaviour
{
    /// <summary>
    ///     The single dimension that makes of the quadratic map that will be generated.
    /// </summary>
    public float Size = 75;

    /// <summary>
    ///     A percentage indicating how far the player has to cross into
    ///     an adjacent cell before new cells are loaded.
    /// </summary>
    public double Threshold;

    /// <summary>
    ///     The player.
    ///     This should be the top-most container of the player.
    /// </summary>
    public GameObject Player;

    /// <summary>
    ///     In instance of <see cref="ITerrainProvider"/>.
    /// </summary>
    public HeightmapProvider TerrainProvider;

    /// <summary>
    ///     The actual threshold in points/pixels(?) that the player needs to move into a new cell to trigger generation of new
    ///     terrain.
    ///     This is defined as:
    ///     <code>
    ///         [threshold in percentage] * 0,01 * [size of cell]
    ///     </code>
    /// </summary>
    /// <remarks>
    ///     Invariant: This value should never change between calls.
    /// </remarks>
    private double _threshold;

    private readonly Dictionary<Vector3, GameObject> _grid = new Dictionary<Vector3, GameObject>();

    /// <summary>
    ///     This is defined as:
    ///     <code>
    ///         ([width of cell] / 2) + _threshold
    ///     </code>
    /// </summary>
    /// <remarks>
    ///     Invariant: This value should never change between calls.
    /// </remarks>
    private double _boundaryX;

    /// <summary>
    ///     This is defined as:
    ///     <code>
    ///         ([height of cell] / 2) + _threshold
    ///     </code>
    /// </summary>
    /// <remarks>
    ///     Invariant: This value should never change between calls.
    /// </remarks>
    private double _boundaryY;

    /// <summary>
    ///     The center point of the current cell in which the player is.
    ///     Whenever the player crosses the <see cref="_threshold"/> into a new cell this value is updated.
    /// </summary>
    private Vector3 _spawnPoint;

    /// <summary>
    ///     The size of one cell in the 3x3 grid.
    ///     Both the width and the height of a cell is defined as:
    ///     <code>
    ///         [size of grid] / 3
    ///     </code>
    /// </summary>
    /// <remarks>
    ///     Invariant: This value should never change between calls.
    /// </remarks>
    private Vector3 _cellSize;

    /// <summary>
    ///     The specific implementation of IEqualityComparer we need for comparing two <see cref="Vector3" />.
    /// </summary>
    private readonly Comparer _vectorComparer = new Comparer();

    // Use this for initialization
    void Start ()
    {
        //TerrainProvider = new ColorProvider();
        _spawnPoint = new Vector3
        {
            x = Size / 2,
            y = Player.transform.position.y,
            z = Size / 2
        };
        _cellSize = new Vector3
        {
            x = Size / 3,
            y = Size / 3,
            z = Size / 3
        };

        Vector3 newPos = new Vector3 (_spawnPoint.x + _cellSize.x/2, _spawnPoint.y, _spawnPoint.z + _cellSize.z/2); //f#@king terrains start from the corner
        Player.transform.position = newPos;

        _threshold = _cellSize.x * (Threshold * 0.01);
        _boundaryX = _cellSize.x / 2 + _threshold;
        _boundaryY = _cellSize.z / 2 + _threshold;
        
        //TerrainProvider.PrefetchAroundPoint(_cellSize, _spawnPoint);
        GenerateFromSpawnPoint();
    }

    // Update is called once per frame
    void Update ()
    {
        var direction = GetDirection();
        switch (direction)
        {
            case Direction.None:
                return;
            case Direction.NorthEast:
                _spawnPoint = new Vector3
                {
                    x = _spawnPoint.x + _cellSize.x,
                    y = _spawnPoint.y,
                    z = _spawnPoint.z + _cellSize.z
                };
                break;
            case Direction.NorthWest:
                _spawnPoint = new Vector3
                {
                    x = _spawnPoint.x - _cellSize.x,
                    y = _spawnPoint.y,
                    z = _spawnPoint.z + _cellSize.z
                };
                break;
            case Direction.SouthEast:
                _spawnPoint = new Vector3
                {
                    x = _spawnPoint.x + _cellSize.x,
                    y = _spawnPoint.y,
                    z = _spawnPoint.z - _cellSize.z
                };
                break;
            case Direction.SouthWest:
                _spawnPoint = new Vector3
                {
                    x = _spawnPoint.x - _cellSize.x,
                    y = _spawnPoint.y,
                    z = _spawnPoint.z - _cellSize.z
                };
                break;
            case Direction.North:
                _spawnPoint = new Vector3
                {
                    x = _spawnPoint.x,
                    y = _spawnPoint.y,
                    z = _spawnPoint.z + _cellSize.z
                };
                break;
            case Direction.East:
                _spawnPoint = new Vector3
                {
                    x = _spawnPoint.x + _cellSize.x,
                    y = _spawnPoint.y,
                    z = _spawnPoint.z
                };
                break;
            case Direction.South:
                _spawnPoint = new Vector3
                {
                    x = _spawnPoint.x,
                    y = _spawnPoint.y,
                    z = _spawnPoint.z - _cellSize.z
                };
                break;
            case Direction.West:
                _spawnPoint = new Vector3
                {
                    x = _spawnPoint.x - _cellSize.x,
                    y = _spawnPoint.y,
                    z = _spawnPoint.z
                };
                break;
            default:
                print( "Do the mambo dance!!" );
                break;
        }

        GenerateFromSpawnPoint();
    }

    private Direction GetDirection()
    {
        var delta = new Vector3
        {
            x = Player.transform.position.x - _spawnPoint.x,
            z = Player.transform.position.z - _spawnPoint.z
        };

        if ( Math.Abs( delta.x ) > _boundaryX )
        {
            if ( Math.Abs( delta.z ) > _boundaryY )
            {
                if (delta.x > 0)
                {
                    return delta.z > 0 
                        ? Direction.NorthEast 
                        : Direction.SouthEast;
                }
                if (delta.x < 0)
                {
                    return delta.z > 0 
                        ? Direction.NorthWest 
                        : Direction.SouthWest;
                }
            }
            else
            {
                return delta.x > 0 
                    ? Direction.East 
                    : Direction.West;
            }
        }
        else if ( Math.Abs( delta.z ) > _boundaryY )
        {
            return delta.z > 0 
                ? Direction.North 
                : Direction.South;
        }

        return Direction.None;
    }

    private enum Direction
    {
        NorthWest = 0,
        North = 1,
        NorthEast = 2,
        West = 3,
        None = 4,
        East = 5,
        SouthWest = 6,
        South = 7,
        SouthEast = 8,
    }

    /// <summary>
    ///     Invariant (post): |cells| = 9
    /// </summary>
    private void GenerateFromSpawnPoint()
    {
        //_mapHandler.Refresh();
        RemoveObsoleteCells();

        var zOffset = _spawnPoint.z - _cellSize.z;
        var xOffset = _spawnPoint.x - _cellSize.x;
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var spawnPoint = new Vector3
                {
                    x = xOffset + ( column * _cellSize.x ),
                    y = 0,
                    z = zOffset + ( row * _cellSize.z )
                };

                /* We can't just call _grid.ContainsKey(), because we need 
                 * to use a special implementation of IEqualityComparer.
                 * See Comparer.
                 */
                if ( _grid.Keys.Contains( spawnPoint, _vectorComparer ) )
                {
                    continue;
                }
                var primitive = TerrainProvider.CreateTerrain( _cellSize, spawnPoint );
                _grid[spawnPoint] = primitive;
            }
        }
    }

    /// <summary>
    ///     Remove the cells that the player can (should) no longer see.
    /// </summary>
    private void RemoveObsoleteCells()
    {
        var upperRow = _spawnPoint + new Vector3( 0, 0, _cellSize.z );
        var e1 = _grid.Keys.Where( v => v.z > upperRow.z );

        var lowerRow = _spawnPoint - new Vector3( 0, 0, _cellSize.z );
        var e2 = _grid.Keys.Where( v => v.z < lowerRow.z );

        var leftColumn = _spawnPoint - new Vector3( _cellSize.x, 0, 0 );
        var e3 = _grid.Keys.Where( v => v.x < leftColumn.x );

        var rightColumn = _spawnPoint + new Vector3( _cellSize.x, 0, 0 );
        var e4 = _grid.Keys.Where( v => v.x > rightColumn.x );

        var obsoleteCells = e1.Union( e2 )
                              .Union( e3 )
                              .Union( e4 );


        var list = obsoleteCells.ToList();
        foreach (var v in list)
        {
            Destroy( _grid[v] );
            _grid.Remove( v );
        }
    }
}

public interface ITerrainProvider
{
    GameObject CreateTerrain(Vector3 cellSize, Vector3 center);
    void PrefetchAroundPoint(Vector3 cellSize, Vector3 spawnPoint);
}


class ColorProvider : ITerrainProvider
{
    private readonly List<Color> _colors = new List<Color>
    {
        Color.green,
        Color.cyan,
        Color.blue,
        Color.magenta,
        Color.red,
        Color.yellow,
        Color.green,
        Color.white,
        Color.white
    };

    private int _index;

    public GameObject CreateTerrain(Vector3 cellSize, Vector3 center )
    {
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        primitive.GetComponent<Renderer>()
                 .material = new Material(Shader.Find("Diffuse"))
                 {
                     color = _colors[_index % 9]
                 };
        primitive.transform.transform.localScale = new Vector3(cellSize.x, cellSize.y, cellSize.z);
        primitive.transform.transform.position = center;
        _index++;
        return primitive;
    }

    public void PrefetchAroundPoint (Vector3 cellSize, Vector3 spawnPoint){
        throw new NotImplementedException();
    }
}

internal class Comparer : IEqualityComparer<Vector3>
{
    public bool Equals(Vector3 x, Vector3 y)
    {
        return Mathf.Approximately( x.x, y.x ) && Mathf.Approximately( x.z, y.z );
    }

    public int GetHashCode(Vector3 obj)
    {
        throw new NotImplementedException();
    }
}

