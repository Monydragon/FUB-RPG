using System;
using System.Collections.Generic;
using Fub.Enums;
using Fub.Interfaces.Actors;

namespace Fub.Implementations.Actors;

/// <summary>
/// Component that controls how an entity moves on the map
/// </summary>
public class MovementController
{
    private readonly IActor _actor;
    private readonly System.Random _random;
    
    public MovementBehavior Behavior { get; set; }
    public int RoamRadius { get; set; } = 5;
    public int HomeX { get; private set; }
    public int HomeY { get; private set; }
    public List<(int x, int y)> PatrolWaypoints { get; set; } = new();
    public int CurrentWaypointIndex { get; set; }
    public IActor? ChaseTarget { get; set; }
    public float MovementCooldown { get; set; } = 1.0f;
    public float TimeSinceLastMove { get; set; }
    
    public MovementController(IActor actor, MovementBehavior behavior = MovementBehavior.Stationary)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        _random = new System.Random(Guid.NewGuid().GetHashCode());
        Behavior = behavior;
        HomeX = actor.X;
        HomeY = actor.Y;
    }

    /// <summary>
    /// Sets the home position for roaming/guarding
    /// </summary>
    public void SetHomePosition(int x, int y)
    {
        HomeX = x;
        HomeY = y;
    }

    /// <summary>
    /// Updates movement logic - should be called each game tick
    /// </summary>
    public void Update(float deltaTime, Func<int, int, bool> canMoveTo)
    {
        TimeSinceLastMove += deltaTime;
        
        if (TimeSinceLastMove < MovementCooldown)
            return;

        bool moved = false;
        
        switch (Behavior)
        {
            case MovementBehavior.Roaming:
                moved = UpdateRoaming(canMoveTo);
                break;
            case MovementBehavior.Patrol:
                moved = UpdatePatrol(canMoveTo);
                break;
            case MovementBehavior.Chase:
                moved = UpdateChase(canMoveTo);
                break;
            case MovementBehavior.Flee:
                moved = UpdateFlee(canMoveTo);
                break;
            case MovementBehavior.Guard:
                moved = UpdateGuard(canMoveTo);
                break;
            case MovementBehavior.Stationary:
            default:
                // Don't move
                break;
        }

        if (moved)
        {
            TimeSinceLastMove = 0;
        }
    }

    private bool UpdateRoaming(Func<int, int, bool> canMoveTo)
    {
        // Check if we're within roam radius
        var distFromHome = Math.Abs(_actor.X - HomeX) + Math.Abs(_actor.Y - HomeY);
        
        // 70% chance to move randomly, 30% to stay still
        if (_random.NextDouble() > 0.7)
            return false;

        // If far from home, bias towards home
        if (distFromHome >= RoamRadius)
        {
            return MoveTowards(HomeX, HomeY, canMoveTo);
        }

        // Random movement in cardinal directions
        var directions = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };
        var shuffled = Shuffle(directions);

        foreach (var (dx, dy) in shuffled)
        {
            var nx = _actor.X + dx;
            var ny = _actor.Y + dy;
            
            // Check if still within roam radius
            var newDist = Math.Abs(nx - HomeX) + Math.Abs(ny - HomeY);
            if (newDist <= RoamRadius && canMoveTo(nx, ny))
            {
                _actor.TryMove(dx, dy);
                return true;
            }
        }

        return false;
    }

    private bool UpdatePatrol(Func<int, int, bool> canMoveTo)
    {
        if (PatrolWaypoints.Count == 0)
            return false;

        var target = PatrolWaypoints[CurrentWaypointIndex];
        
        // Check if we reached the waypoint
        if (_actor.X == target.x && _actor.Y == target.y)
        {
            // Move to next waypoint
            CurrentWaypointIndex = (CurrentWaypointIndex + 1) % PatrolWaypoints.Count;
            return false;
        }

        return MoveTowards(target.x, target.y, canMoveTo);
    }

    private bool UpdateChase(Func<int, int, bool> canMoveTo)
    {
        if (ChaseTarget == null)
            return false;

        return MoveTowards(ChaseTarget.X, ChaseTarget.Y, canMoveTo);
    }

    private bool UpdateFlee(Func<int, int, bool> canMoveTo)
    {
        if (ChaseTarget == null)
            return false;

        // Move away from target
        var dx = _actor.X - ChaseTarget.X;
        var dy = _actor.Y - ChaseTarget.Y;

        // Prioritize moving away on the axis with greater distance
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            var moveX = dx > 0 ? 1 : -1;
            if (canMoveTo(_actor.X + moveX, _actor.Y))
            {
                _actor.TryMove(moveX, 0);
                return true;
            }
        }
        else
        {
            var moveY = dy > 0 ? 1 : -1;
            if (canMoveTo(_actor.X, _actor.Y + moveY))
            {
                _actor.TryMove(0, moveY);
                return true;
            }
        }

        return false;
    }

    private bool UpdateGuard(Func<int, int, bool> canMoveTo)
    {
        // Return to home position if not there
        if (_actor.X != HomeX || _actor.Y != HomeY)
        {
            return MoveTowards(HomeX, HomeY, canMoveTo);
        }

        return false;
    }

    private bool MoveTowards(int targetX, int targetY, Func<int, int, bool> canMoveTo)
    {
        var dx = targetX - _actor.X;
        var dy = targetY - _actor.Y;

        // Prioritize moving on the axis with greater distance
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            var moveX = Math.Sign(dx);
            if (canMoveTo(_actor.X + moveX, _actor.Y))
            {
                _actor.TryMove(moveX, 0);
                return true;
            }
        }
        
        if (dy != 0)
        {
            var moveY = Math.Sign(dy);
            if (canMoveTo(_actor.X, _actor.Y + moveY))
            {
                _actor.TryMove(0, moveY);
                return true;
            }
        }

        // Try other axis if primary failed
        if (dx != 0 && Math.Abs(dx) <= Math.Abs(dy))
        {
            var moveX = Math.Sign(dx);
            if (canMoveTo(_actor.X + moveX, _actor.Y))
            {
                _actor.TryMove(moveX, 0);
                return true;
            }
        }

        return false;
    }

    private T[] Shuffle<T>(T[] array)
    {
        var result = (T[])array.Clone();
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }
}
