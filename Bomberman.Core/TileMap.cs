﻿using System.Collections.Immutable;
using Bomberman.Core.Tiles;
using Bomberman.Core.Utilities;

namespace Bomberman.Core;

public class TileMap : IUpdatable
{
    public int Width { get; }
    public int Height { get; }

    private readonly Tile[][] _backgroundTiles;
    private readonly Tile?[][] _foregroundTiles;
    private readonly StatefulRandom _rnd = new(44);

    public ImmutableArray<Tile> Tiles =>
        [.. _backgroundTiles.Concat(_foregroundTiles).SelectMany(row => row).OfType<Tile>()];

    public TileMap(int width, int height)
    {
        Width = width;
        Height = height;

        _backgroundTiles = Enumerable
            .Range(0, height)
            .Select(row =>
                Enumerable
                    .Range(0, width)
                    .Select(column => new GridPosition(row, column))
                    .Select(gridPosition => new FloorTile(gridPosition))
                    .ToArray()
            )
            .ToArray<Tile[]>();

        _foregroundTiles = Enumerable.Range(0, height).Select(_ => new Tile?[width]).ToArray();
    }

    internal TileMap(TileMap original)
    {
        Width = original.Width;
        Height = original.Height;
        _rnd = new StatefulRandom(original._rnd);

        _backgroundTiles = original
            ._backgroundTiles.Select(row =>
                row.Select(originalTile => originalTile.Clone(this)).ToArray()
            )
            .ToArray();

        _foregroundTiles = original
            ._foregroundTiles.Select(row =>
                row.Select(originalTile => originalTile?.Clone(this)).ToArray()
            )
            .ToArray();
    }

    internal TileMap WithDefaultTileLayout(GridPosition start)
    {
        // Wall on top row
        _foregroundTiles[0] = Enumerable
            .Range(0, Width)
            .Select(column => new GridPosition(0, column))
            .Select(gridPosition => new LavaTile(gridPosition))
            .ToArray<Tile>();

        // Wall on bottom row
        _foregroundTiles[Height - 1] = Enumerable
            .Range(0, Width)
            .Select(column => new GridPosition(Height - 1, column))
            .Select(gridPosition => new LavaTile(gridPosition))
            .ToArray<Tile>();

        // Walls on left and right columns
        for (int row = 0; row < Height; row++)
        {
            _foregroundTiles[row][0] = new LavaTile(new GridPosition(row, 0));
            _foregroundTiles[row][Width - 1] = new LavaTile(new GridPosition(row, Width - 1));
        }

        for (int row = 1; row < Height - 1; row++)
        {
            for (int column = 1; column < Width - 1; column++)
            {
                _foregroundTiles[row][column] = RandomTile(new GridPosition(row, column));
            }
        }

        // Checker walls
        for (int row = 2; row < Height - 1; row += 2)
        for (int column = 2; column < Width - 1; column += 2)
            _foregroundTiles[row][column] = new WallTile(new GridPosition(row, column));

        // Clear around starting position to allow player to move
        foreach (var position in new[] { start }.Concat(start.Neighbours))
            _foregroundTiles[position.Row][position.Column] = null;

        return this;
    }

    public void Update(TimeSpan deltaTime)
    {
        foreach (var updatableTile in _foregroundTiles.SelectMany(row => row).OfType<IUpdatable>())
        {
            updatableTile.Update(deltaTime);
        }
    }

    internal Tile? GetTile(GridPosition gridPosition)
    {
        if (gridPosition.Row < 0 || gridPosition.Row >= Height)
            return null;

        if (gridPosition.Column < 0 || gridPosition.Column >= Width)
            return null;

        return _foregroundTiles[gridPosition.Row][gridPosition.Column];
    }

    internal void PlaceTile(Tile newTile)
    {
        var gridPosition = newTile.Position;
        var existingTile = _foregroundTiles[gridPosition.Row][gridPosition.Column];

        if (existingTile != null)
            throw new InvalidOperationException(
                $"This grid position is already taken ({gridPosition})"
            );

        _foregroundTiles[gridPosition.Row][gridPosition.Column] = newTile;
    }

    internal void RemoveTile(Tile tile)
    {
        if (_foregroundTiles[tile.Position.Row][tile.Position.Column] == null)
            throw new InvalidOperationException($"Tile is not in the tile map ({tile.Position})");

        if (_foregroundTiles[tile.Position.Row][tile.Position.Column] != tile)
            throw new InvalidOperationException(
                $"Tile in the tile map is not the specified tile ({tile.Position})"
            );

        _foregroundTiles[tile.Position.Row][tile.Position.Column] = null;
    }

    internal void Shift()
    {
        for (int row = 1; row < Height - 1; row++)
        {
            for (int column = 1; column < Width - 2; column++)
            {
                // _foregroundTiles[row][column] is not null only for the left most column of the play zone
                var removedTile = _foregroundTiles[row][column];
                if (removedTile is BombTile oldBombTile)
                {
                    // Force into a detonated state to unblock player bomb placement logic
                    oldBombTile.Detonated = true;
                }

                _foregroundTiles[row][column] = _foregroundTiles[row][column + 1];
                var shiftedTile = _foregroundTiles[row][column];

                // The next column iteration will have _foregroundTiles[row][column] set to null
                _foregroundTiles[row][column + 1] = null;

                if (shiftedTile != null)
                    shiftedTile.Position = new GridPosition(Row: row, Column: column);
            }
        }

        for (int row = 1; row < Height - 1; row++)
        {
            _foregroundTiles[row][Width - 2] = RandomTile(
                new GridPosition(Row: row, Column: Width - 2)
            );
        }

        for (int row = 2; row < Height - 1; row += 2)
        {
            if (GetTile(new GridPosition(row, Width - 2 - 1)) is not WallTile)
                _foregroundTiles[row][Width - 2] = new WallTile(new GridPosition(row, Width - 2));
        }
    }

    private Tile? RandomTile(GridPosition position) =>
        _rnd.NextDouble() switch
        {
            < 0.491 => new BoxTile(position),
            < 0.494 => new FireUpTile(position, this),
            < 0.497 => new SpeedUpTile(position, this),
            < 0.5 => new BombUpTile(position, this),
            < 0.6 => new CoinTile(position, this),
            _ => null,
        };
}
