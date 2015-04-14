﻿using UnityEngine;
using System;
using System.Collections.Generic;


/// <summary>
/// Concrete implementation of the IGameModel abstract base class.
/// </summary>
public class GameModel : IGameModel
{
    /// <summary>
    /// Minimum room width and length.
    /// </summary>
    [Tooltip("Minimum room width and length.")]
    public int minRoomSize = 3;

    /// <summary>
    /// Maximum room width and length.
    /// </summary>
    [Tooltip("Maximum room width and length.")]
    public int maxRoomSize = 15;

    /// <summary>
    /// Average distance between the rooms.
    /// </summary>
    [Tooltip("Average distance between the rooms.")]
    public int roomSpread = 3;

    // TODO: Level dimensions of 1,1 do not work.
    /// <summary>
    /// The level dimensions.
    /// </summary>
    [Tooltip("Number of rooms in the x-direction.")]
    public int levelDimensionsX = 5;

    /// <summary>
    /// The level dimensions.
    /// </summary>
    [Tooltip("Number of rooms in the z-direction.")]
    public int levelDimensionsZ = 5;

    /// <summary>
    /// Split the level into a grid. A room can be placed in each grid location
    /// to ensure there is not room overlap.
    /// </summary>
    private int gridSize;

    /// <summary>
    /// Represents the relationship between each of the rooms.
    /// </summary>
    private IGraph<Room> roomGraph;

    private DungeonLayout[,] dungeonLayout;

    /// <summary>
    /// Represents the grid location of each of the rooms.
    /// </summary>
    private Room[,] rooms;

    /// <summary>
    /// Initialize the dungeon level.
    /// </summary>
    /// <param name="presenter">The game presenter.</param>
    public override void Initialize(Presenter presenter)
    {
        // Let the base class do its thing.
        base.Initialize(presenter);

        // Initialize the floor plan.
        gridSize = maxRoomSize + roomSpread;
        roomGraph = new RoomGraph<Room>();
        rooms = new Room[levelDimensionsX, levelDimensionsZ];

        int width = levelDimensionsX * gridSize;
        int length = levelDimensionsZ * gridSize;
        dungeonLayout = new DungeonLayout[width, length];

        GenerateRoomLayout();
        GenerateRoomConnections();
        GenerateRoomGraph();

        // Publish the floor plan message.
        FloorPlanMsg msg = new FloorPlanMsg();
        msg.RoomGraph = roomGraph;
        msg.DungeonLayout = dungeonLayout;
        msg.Width = width;
        msg.Length = length;
        presenter.PublishMsg(msg);
    }

    /// <summary>
    /// Determine the positions, widths and lengths of all the rooms.
    /// Mark the 2D array with the floor.
    /// </summary>
    private void GenerateRoomLayout()
    {
        // Generate a room in each grid location.
        System.Random rnd = new System.Random();
        for (int gridLocationX = 0; gridLocationX < levelDimensionsX; gridLocationX++)
        {
            for (int gridLocationZ = 0; gridLocationZ < levelDimensionsZ; gridLocationZ++)
            {
                // Generate a room width and length.
                int width = rnd.Next(minRoomSize, maxRoomSize);
                int length = rnd.Next(minRoomSize, maxRoomSize);

                // Position the room within the grid location.
                int positionX = rnd.Next(0, gridSize - width) + gridLocationX * gridSize;
                int positionZ = rnd.Next(0, gridSize - length) + gridLocationZ * gridSize;

                Room room = new Room(positionX, positionZ, width, length);
                rooms[gridLocationX, gridLocationZ] = room;

                for (int i = positionX; i < positionX + width; ++i)
                {
                    for (int j = positionZ; j < positionZ + length; ++j)
                    {
                        dungeonLayout[i, j] = DungeonLayout.Floor;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Connect the rooms together with flooring.
    /// This is a vey basic algorithm that is bound to fail if the rooms are
    /// too small. It serves as a basis only.
    /// </summary>
    private void GenerateRoomConnections()
    {
        IWeightedGraph<Location> grid = new DungeonGrid(levelDimensionsX * gridSize, levelDimensionsZ * gridSize);

        // Make the North-South room connections.
        for (int gridLocationZ = 0; gridLocationZ < levelDimensionsZ - 1; gridLocationZ++)
        {
            for (int gridLocationX = 0; gridLocationX < levelDimensionsX; gridLocationX++)
            {
                Room top = rooms[gridLocationX, gridLocationZ + 1];
                Room bottom = rooms[gridLocationX, gridLocationZ];

                int midTop = top.PositionX + (top.Width/2);
                int midBottom = bottom.PositionX + (bottom.Width / 2);

                PathFinder finder = new PathFinder(
                    grid,
                    new Location(midBottom, bottom.PositionZ + bottom.Length - 1),
                    new Location(midTop, top.PositionZ));

                List<Location> path = finder.GetPath();

                for (int i = 0; i < path.Count; ++i)
                {
                    dungeonLayout[path[i].x, path[i].y] = DungeonLayout.Floor;
                }
            }
        }

        // Make the East-West room connections.
        for (int gridLocationZ = 0; gridLocationZ < levelDimensionsZ; gridLocationZ++)
        {
            for (int gridLocationX = 0; gridLocationX < levelDimensionsX - 1; gridLocationX++)
            {
                Room left = rooms[gridLocationX, gridLocationZ];
                Room right = rooms[gridLocationX + 1, gridLocationZ];

                int midLeft = left.PositionZ + (left.Length / 2);
                int midRight = right.PositionZ + (right.Length / 2);

                PathFinder finder = new PathFinder(
                    grid,
                    new Location(left.PositionX + left.Width - 1, midLeft),
                    new Location(right.PositionX, midRight));

                List<Location> path = finder.GetPath();

                for (int i = 0; i < path.Count; ++i)
                {
                    dungeonLayout[path[i].x, path[i].y] = DungeonLayout.Floor;
                }
            }
        }
    }

    /// <summary>
    /// Create the room graph.
    /// </summary>
    /// <remarks>
    /// In general, a room may be connected to another room in an adjacent gridLocation.
    /// For example a room in grid location 0,0 may be connected to a room in 0,1 and 1,0.
    /// A room must have at least one connection.
    /// </remarks>
    private void GenerateRoomGraph()
    {
        // TODO: For now just create an edge between adjacent rooms.
        /*for (int gridLocationX = 0; gridLocationX < levelDimensionsX; gridLocationX++)
        {
            for (int gridLocationZ = 0; gridLocationZ < levelDimensionsZ - 1; gridLocationZ++)
            {
                roomGraph.AddEdge(rooms[gridLocationX, gridLocationZ], rooms[gridLocationX, gridLocationZ + 1]);
            }
        }

        for (int gridLocationZ = 0; gridLocationZ < levelDimensionsZ; gridLocationZ++)
        {
            for (int gridLocationX = 0; gridLocationX < levelDimensionsX - 1; gridLocationX++)
            {
                roomGraph.AddEdge(rooms[gridLocationX, gridLocationZ], rooms[gridLocationX + 1, gridLocationZ]);
            }
        }*/
    }

    public override void UpdateModel()
    {
        presenter.PublishMsg(new SampleMsg());
    }
}