namespace Bomberman.Core.Tiles;

public class BombTile(GridPosition position, TileMap tileMap, int range)
    : Tile(position),
        IUpdatable
{
    private static readonly TimeSpan DetonateAfter = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ExplosionDuration = TimeSpan.FromSeconds(0.25);

    private TimeSpan _existingTime = TimeSpan.Zero;

    public bool Detonated { get; private set; }

    // A bomb is considered exploded only after the explosion tile is gone
    public bool Exploded => Detonated && tileMap.GetTile(Position) == null;

    public void Update(TimeSpan deltaTime)
    {
        _existingTime += deltaTime;

        if (_existingTime >= DetonateAfter)
            Explode();
    }

    /// <summary>
    /// Gets explosion paths in 4 directions based on the bomb range
    /// </summary>
    public IEnumerable<IEnumerable<GridPosition>> ExplosionPaths =>
        new Func<int, GridPosition>[]
        {
            distanceFromCenter => Position with { Row = Position.Row - distanceFromCenter },
            distanceFromCenter => Position with { Row = Position.Row + distanceFromCenter },
            distanceFromCenter => Position with { Column = Position.Column - distanceFromCenter },
            distanceFromCenter => Position with { Column = Position.Column + distanceFromCenter },
        }.Select(explosionPositionCalculationOnSpecificDirection =>
            Enumerable.Range(1, range).Select(explosionPositionCalculationOnSpecificDirection)
        );

    private void Explode()
    {
        Detonated = true;

        tileMap.RemoveTile(this);
        tileMap.PlaceTile(new ExplosionTile(Position, tileMap, ExplosionDuration));

        foreach (var explosionPath in ExplosionPaths)
        {
            foreach (var explosionPosition in explosionPath)
            {
                var tileToExplode = tileMap.GetTile(explosionPosition);

                if (tileToExplode is BombTile bombTile)
                {
                    // Chain reaction
                    bombTile.Explode();
                    break;
                }

                if (tileToExplode is BoxTile boxTile)
                {
                    tileMap.RemoveTile(boxTile);
                    tileMap.PlaceTile(
                        new ExplosionTile(boxTile.Position, tileMap, ExplosionDuration)
                    );
                    break;
                }

                // TODO: What if the explosion path intercepts an explosion path from another bomb?
                // Currently this bomb's explosion path will be blocked

                if (tileToExplode != null)
                    break;

                tileMap.PlaceTile(new ExplosionTile(explosionPosition, tileMap, ExplosionDuration));
            }
        }
    }

    public override object Clone() =>
        new BombTile(Position, tileMap, range)
        {
            _existingTime = _existingTime,
            Detonated = Detonated,
        };
}
