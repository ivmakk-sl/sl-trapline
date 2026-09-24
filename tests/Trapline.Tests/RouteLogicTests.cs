using System.Collections.Generic;
using Trapline;
using Xunit;

public class RouteLogicTests
{
    [Fact]
    public void BuildRoute_OneFloor_NearestNeighbourFromCharacter()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 1, X = 10, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 1, X = 1, Z = 0 },
        };

        var route = RouteLogic.BuildRoute(traps, characterFloor: 1, characterX: 0, characterZ: 0, floorCosts: new Dictionary<int, float>());

        Assert.Equal(new List<int> { 2, 1 }, route);
    }

    [Fact]
    public void BuildRoute_CharacterFloorGoesFirst()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 2, X = 0, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 1, X = 0, Z = 0 },
        };
        var floorCosts = new Dictionary<int, float> { { 2, 1f } };

        var route = RouteLogic.BuildRoute(traps, characterFloor: 1, characterX: 0, characterZ: 0, floorCosts: floorCosts);

        Assert.Equal(new List<int> { 2, 1 }, route);
    }

    [Fact]
    public void BuildRoute_NoTrapOnCharacterFloor_NearestFloorByCostComesFirst()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 2, X = 0, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 3, X = 0, Z = 0 },
        };
        var floorCosts = new Dictionary<int, float> { { 2, 5f }, { 3, 2f } };

        var route = RouteLogic.BuildRoute(traps, characterFloor: 1, characterX: 0, characterZ: 0, floorCosts: floorCosts);

        Assert.Equal(new List<int> { 2, 1 }, route);
    }

    [Fact]
    public void BuildRoute_EqualCost_FloorIdOrder()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 3, X = 0, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 2, X = 0, Z = 0 },
        };
        var floorCosts = new Dictionary<int, float> { { 2, 5f }, { 3, 5f } };

        var route = RouteLogic.BuildRoute(traps, characterFloor: 1, characterX: 0, characterZ: 0, floorCosts: floorCosts);

        Assert.Equal(new List<int> { 2, 1 }, route);
    }

    [Fact]
    public void BuildRoute_FloorMissingFromCostTable_SortsAfterFloorsWithCost()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 2, X = 0, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 3, X = 0, Z = 0 },
        };
        var floorCosts = new Dictionary<int, float> { { 3, 100f } };

        var route = RouteLogic.BuildRoute(traps, characterFloor: 1, characterX: 0, characterZ: 0, floorCosts: floorCosts);

        Assert.Equal(new List<int> { 2, 1 }, route);
    }

    [Fact]
    public void BuildRoute_LaterFloorStartsAtTrapNearestToLastTrap()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 1, X = 20, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 2, X = 10, Z = 0 },
            new RouteLogic.Trap { Id = 3, Floor = 2, X = 0, Z = 0 },
        };
        var floorCosts = new Dictionary<int, float> { { 2, 1f } };

        var route = RouteLogic.BuildRoute(traps, characterFloor: 1, characterX: 0, characterZ: 0, floorCosts: floorCosts);

        // Floor 1's only trap is at (20,0). Floor 2 starts from there, not from the character
        // position (0,0), so trap 2 at (10,0) is nearer than trap 3 at (0,0) and comes first.
        Assert.Equal(new List<int> { 1, 2, 3 }, route);
    }

    [Fact]
    public void BuildRoute_TiesByTrapId()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 5, Floor = 1, X = 1, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 1, X = 1, Z = 0 },
        };

        var route = RouteLogic.BuildRoute(traps, characterFloor: 1, characterX: 0, characterZ: 0, floorCosts: new Dictionary<int, float>());

        Assert.Equal(new List<int> { 2, 5 }, route);
    }

    [Fact]
    public void RouteState_FailInTheMiddle_ReturnsRemainingIdsAndEndsRoute()
    {
        var state = new RouteState();
        state.Start(new List<int> { 1, 2, 3 });
        state.MarkDone(1);

        var remaining = state.Fail(2);

        Assert.Equal(new List<int> { 3 }, remaining);
        Assert.False(state.IsActive);
    }

    [Fact]
    public void RouteState_FailOfTheLastTrap_ReturnsEmptyAndEndsRoute()
    {
        var state = new RouteState();
        state.Start(new List<int> { 1, 2 });
        state.MarkDone(1);

        var remaining = state.Fail(2);

        Assert.Empty(remaining);
        Assert.False(state.IsActive);
    }

    [Fact]
    public void RouteState_PickupOfTrapOutsideTheRoute_ChangesNothing()
    {
        var state = new RouteState();
        state.Start(new List<int> { 1, 2 });

        var remaining = state.Fail(99);
        state.MarkDone(99);

        Assert.Empty(remaining);
        Assert.True(state.IsActive);
        Assert.True(state.Contains(1));
        Assert.True(state.Contains(2));
    }

    [Fact]
    public void RouteState_NewRoute_ReplacesAnOldOne()
    {
        var state = new RouteState();
        state.Start(new List<int> { 1, 2 });

        state.Start(new List<int> { 3, 4 });

        Assert.False(state.Contains(1));
        Assert.False(state.Contains(2));
        Assert.True(state.Contains(3));
        Assert.True(state.Contains(4));
    }

    [Fact]
    public void RouteState_EndsAfterItsLastTrapIsDone()
    {
        var state = new RouteState();
        state.Start(new List<int> { 1, 2 });

        state.MarkDone(1);
        Assert.True(state.IsActive);

        state.MarkDone(2);

        Assert.False(state.IsActive);
    }
}
