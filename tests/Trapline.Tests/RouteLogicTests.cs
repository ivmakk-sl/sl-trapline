using System;
using System.Collections.Generic;
using Trapline;
using Xunit;

public class RouteLogicTests
{
    // The route of Take All comes from the route library (shared/route); these tests check the traps that go in and
    // the trap ids that come out, with the rules of the spec "Floor-by-floor route".
    private static List<int> Order(List<RouteLogic.Trap> traps, int characterFloor = 1, float x = 0, float z = 0,
        Func<int, int, int> floorCost = null,
        Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk = null) =>
        RouteLogic.Order(traps, characterFloor, x, 0, z, floorCost ?? ((a, b) => a == b ? 0 : 1), walk);

    [Fact]
    public void Order_OneFloor_NearestTrapFromTheCharacterFirst()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 1, X = 10, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 1, X = 1, Z = 0 },
        };

        Assert.Equal(new List<int> { 2, 1 }, Order(traps));
    }

    [Fact]
    public void Order_TrapsOnTwoFloors_TheFloorOfTheCharacterFirst()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 2, X = 0, Z = 0 },
            new RouteLogic.Trap { Id = 3, Floor = 1, X = 5, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 1, X = 0, Z = 0 },
        };

        Assert.Equal(new List<int> { 2, 3, 1 }, Order(traps));
    }

    [Fact]
    public void Order_NoPreyOnTheCurrentFloor_TheNearerFloorFirst()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 2, X = 0, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 3, X = 0, Z = 0 },
        };
        int Cost(int a, int b) => a == b ? 0 : (a, b) == (1, 2) || (a, b) == (2, 1) ? 5 : 2;

        Assert.Equal(new List<int> { 2, 1 }, Order(traps, floorCost: Cost));
    }

    [Fact]
    public void Order_EqualFloorCost_TheLowerFloorIdFirst()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 3, X = 0, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 2, X = 0, Z = 0 },
        };

        Assert.Equal(new List<int> { 2, 1 }, Order(traps, floorCost: (a, b) => a == b ? 0 : 5));
    }

    [Fact]
    public void Order_LaterFloorStartsAtTheLastTrapOfTheFloorBefore()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 1, X = 20, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 2, X = 10, Z = 0 },
            new RouteLogic.Trap { Id = 3, Floor = 2, X = 0, Z = 0 },
        };

        // Floor 1's only trap is at (20,0). Floor 2 starts from there, not from the character
        // position (0,0), so trap 2 at (10,0) is nearer than trap 3 at (0,0) and comes first.
        Assert.Equal(new List<int> { 1, 2, 3 }, Order(traps));
    }

    [Fact]
    public void Order_AWallBetweenTwoNearTraps_TheTrapOnTheWayFirst()
    {
        // Trap 2 is 1 m from trap 1 in a straight line, behind a wall: the walk from 1 to 2 goes around it past trap 3.
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 1, X = 1, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 1, X = 2, Z = 0 },
            new RouteLogic.Trap { Id = 3, Floor = 1, X = 10, Z = 0 },
        };
        double Wall((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
            (a.X == 1 && b.X == 2) || (a.X == 2 && b.X == 1) ? 100 : Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

        Assert.Equal(new List<int> { 1, 2, 3 }, Order(traps));
        Assert.Equal(new List<int> { 1, 3, 2 }, Order(traps, walk: Wall));
    }

    [Fact]
    public void Order_TwoEndsOfAFloor_NoWalkBack()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 1, Floor = 1, X = 20, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 1, X = 1, Z = 0 },
            new RouteLogic.Trap { Id = 3, Floor = 1, X = 21, Z = 0 },
            new RouteLogic.Trap { Id = 4, Floor = 1, X = 2, Z = 0 },
        };

        Assert.Equal(new List<int> { 2, 4, 1, 3 }, Order(traps));
    }

    [Fact]
    public void Order_TiesByTrapId()
    {
        var traps = new List<RouteLogic.Trap>
        {
            new RouteLogic.Trap { Id = 5, Floor = 1, X = 1, Z = 0 },
            new RouteLogic.Trap { Id = 2, Floor = 1, X = 1, Z = 0 },
        };

        Assert.Equal(new List<int> { 2, 5 }, Order(traps));
    }

    [Fact]
    public void Order_TheWalkHasTheHeightOfTheTrap()
    {
        var traps = new List<RouteLogic.Trap> { new RouteLogic.Trap { Id = 1, Floor = 1, X = 1, Y = 5, Z = 0 } };
        (float X, float Y, float Z)? seen = null;

        Order(traps, walk: (a, b) => { seen = b; return 1; });

        Assert.Equal((1f, 5f, 0f), seen);
    }

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(false, 3, true)]
    [InlineData(true, 3, true)]
    [InlineData(false, 0, false)]
    public void HasPrey_PreyInTheTrapOrInTheStorageBox(bool preyInTrap, int storeCount, bool expected)
    {
        Assert.Equal(expected, RouteLogic.HasPrey(preyInTrap, storeCount));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void UseStorePath_OnlyWhenTheStorageBoxHoldsPrey(int storeCount, bool expected)
    {
        Assert.Equal(expected, RouteLogic.UseStorePath(storeCount));
    }

    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 5, false)]
    [InlineData(true, 5, false)]
    public void PickupDone_OnlyWhenTheTrapAndTheStorageBoxAreEmpty(bool preyInTrapAfter, int storeCountAfter, bool expected)
    {
        Assert.Equal(expected, RouteLogic.PickupDone(preyInTrapAfter, storeCountAfter));
    }

    [Fact]
    public void PickupDone_PartTakeOfAStorageBox_IsAFailedPickup()
    {
        // The bag had room for 3 of 8 prey, so 5 prey stay in the storage box.
        Assert.False(RouteLogic.PickupDone(preyInTrapAfter: false, storeCountAfter: 8 - 3));
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
    public void RouteState_AfterFail_GivesTheRemainingIdsThatUsedTheStorageBoxPath()
    {
        var state = new RouteState();
        state.Start(new List<int> { 1, 2, 3, 4 });
        state.MarkStorePath(1);
        state.MarkStorePath(3);

        var remaining = state.Fail(1);

        Assert.Equal(new List<int> { 3 }, state.StorePathIds(remaining));
    }

    [Fact]
    public void RouteState_NewRoute_ForgetsTheStorageBoxPathOfTheOldRoute()
    {
        var state = new RouteState();
        state.Start(new List<int> { 1, 2 });
        state.MarkStorePath(2);

        state.Start(new List<int> { 2, 3 });
        var remaining = state.Fail(3);

        Assert.Empty(state.StorePathIds(remaining));
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
