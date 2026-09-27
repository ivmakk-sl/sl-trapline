using System;
using Trapline;
using Xunit;

public class PushScheduleTests
{
    // A build function that counts its calls and gives the JSON that the test sets.
    private sealed class Data
    {
        public string Json = "{\"a\":1}";
        public int Calls;
        public string Build() { Calls++; return Json; }
    }

    private static PushSchedule.Step Push(string json) => new PushSchedule.Step(PushSchedule.Kind.Push, json);
    private static readonly PushSchedule.Step None = new PushSchedule.Step(PushSchedule.Kind.None, null);
    private static readonly PushSchedule.Step Check = new PushSchedule.Step(PushSchedule.Kind.Check, null);

    [Fact]
    public void A_dirty_tick_pushes_the_new_json()
    {
        var s = new PushSchedule();
        var d = new Data();

        Assert.Equal(Push("{\"a\":1}"), s.Tick(10f, true, d.Build));
        Assert.Equal(1, d.Calls);
    }

    [Fact]
    public void A_dirty_tick_with_the_same_json_does_not_push()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);

        Assert.Equal(None, s.Tick(10.2f, true, d.Build));
        Assert.Equal(2, d.Calls);
    }

    [Fact]
    public void A_dirty_tick_with_changed_json_pushes_at_once()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        d.Json = "{\"a\":2}";

        Assert.Equal(Push("{\"a\":2}"), s.Tick(10.1f, true, d.Build));
    }

    [Fact]
    public void A_tick_with_nothing_dirty_does_not_build()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);

        s.Tick(10.5f, false, d.Build);
        s.Tick(12f, false, d.Build);

        Assert.Equal(1, d.Calls);
    }

    [Fact]
    public void The_check_comes_one_real_second_after_the_push_and_then_each_second()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);

        Assert.Equal(None, s.Tick(10.9f, false, d.Build));
        Assert.Equal(Check, s.Tick(11f, false, d.Build));
        Assert.Equal(None, s.Tick(11.5f, false, d.Build));
        Assert.Equal(Check, s.Tick(12.1f, false, d.Build));
    }

    [Fact]
    public void No_check_before_the_first_push()
    {
        var s = new PushSchedule();
        var d = new Data();

        Assert.Equal(None, s.Tick(10f, false, d.Build));
        Assert.Equal(None, s.Tick(20f, false, d.Build));
        Assert.Equal(0, d.Calls);
    }

    [Fact]
    public void After_ForgetLastPush_the_kept_json_goes_again_one_second_after_the_last_send()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        s.ForgetLastPush();

        Assert.Equal(None, s.Tick(10.5f, false, d.Build));
        Assert.Equal(Push("{\"a\":1}"), s.Tick(11f, false, d.Build));
        Assert.Equal(1, d.Calls);
    }

    [Fact]
    public void After_a_failed_check_the_push_comes_one_second_after_the_check()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        Assert.Equal(Check, s.Tick(11f, false, d.Build));
        s.ForgetLastPush();

        Assert.Equal(None, s.Tick(11.5f, false, d.Build));
        Assert.Equal(Push("{\"a\":1}"), s.Tick(12f, false, d.Build));
    }

    [Fact]
    public void While_the_retry_waits_a_dirty_tick_does_not_push_and_the_retry_sends_the_new_json()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        s.ForgetLastPush();
        d.Json = "{\"a\":2}";

        Assert.Equal(None, s.Tick(10.1f, true, d.Build));
        Assert.Equal(None, s.Tick(10.2f, true, d.Build));
        Assert.Equal(Push("{\"a\":2}"), s.Tick(11f, false, d.Build));
    }

    [Fact]
    public void In_the_retry_case_nothing_goes_more_often_than_once_each_second()
    {
        var s = new PushSchedule();
        var d = new Data();
        int sends = 0;
        // One tick each 1/60 s for 5 s. Each send fails (no CoreUI1 frame), and each frame is dirty.
        for (int frame = 0; frame <= 300; frame++)
        {
            d.Json = "{\"frame\":" + frame + "}";
            var step = s.Tick(10f + frame / 60f, true, d.Build);
            if (step.Kind == PushSchedule.Kind.None) continue;
            sends++;
            s.ForgetLastPush();
        }

        Assert.InRange(sends, 5, 6);
    }

    [Fact]
    public void A_build_that_throws_keeps_the_change_and_builds_again_one_second_later()
    {
        var s = new PushSchedule();
        int calls = 0;
        string Failing() { calls++; throw new InvalidOperationException("no world"); }

        Assert.Throws<InvalidOperationException>(() => s.Tick(10f, true, Failing));
        // No new trigger: the change stays pending, and the build waits one real second.
        Assert.Equal(None, s.Tick(10.5f, false, Failing));
        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() => s.Tick(11f, false, Failing));
        Assert.Equal(2, calls);

        var d = new Data();
        Assert.Equal(Push("{\"a\":1}"), s.Tick(12f, false, d.Build));
    }

    [Fact]
    public void A_retry_whose_build_throws_is_kept_for_the_next_second()
    {
        var s = new PushSchedule();
        var d = new Data();
        s.Tick(10f, true, d.Build);
        s.ForgetLastPush();

        Assert.Throws<InvalidOperationException>(() => s.Tick(11f, true, () => throw new InvalidOperationException("no world")));
        Assert.Equal(None, s.Tick(11.5f, false, d.Build));
        d.Json = "{\"a\":2}";
        Assert.Equal(Push("{\"a\":2}"), s.Tick(12f, false, d.Build));
    }

    [Theory]
    [InlineData("ok 3 1", true, 3, 1)]
    [InlineData("ok", true, 0, 0)]
    [InlineData("no script", false, 0, 0)]
    [InlineData("no CoreUI1 frame", false, 0, 0)]
    [InlineData("no observer", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void IsOk_reads_the_check_result(string result, bool ok, int runs, int passes)
    {
        Assert.Equal((ok, runs, passes), PushSchedule.IsOk(result));
    }

    [Fact]
    public void ObserverSum_gives_the_sums_once_each_real_minute()
    {
        var s = new PushSchedule();

        Assert.Null(s.ObserverSum(10f, 3, 1));
        Assert.Null(s.ObserverSum(40f, 4, 0));
        Assert.Equal((7, 1), s.ObserverSum(70f, 0, 0));
        Assert.Null(s.ObserverSum(71f, 2, 2));
        Assert.Equal((2, 2), s.ObserverSum(130f, 0, 0));
    }
}
