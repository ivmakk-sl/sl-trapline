using System;

namespace Trapline
{
    // Decides on each frame whether the plugin pushes the page data, checks that the page still has the
    // page script, or does nothing. No game or BepInEx type here: the caller gives the real time.
    //
    // - A dirty tick builds the data. New JSON goes at once; the same JSON goes nowhere.
    // - With no push for one real second, a check goes.
    // - ForgetLastPush (a push that found no CoreUI1 frame, or a check that is not ok) makes the first tick
    //   one real second after the last send push the data again. It is the only retry: the page has none.
    // - A build that throws (for example between a save and the title screen) keeps the change pending and
    //   rethrows to the caller; the next build waits one real second.
    public sealed class PushSchedule
    {
        public enum Kind { None, Push, Check }

        public readonly struct Step : IEquatable<Step>
        {
            public readonly Kind Kind;
            // The JSON of a push; null for the other kinds.
            public readonly string Json;

            public Step(Kind kind, string json) { Kind = kind; Json = json; }

            public bool Equals(Step other) => Kind == other.Kind && Json == other.Json;
            public override bool Equals(object obj) => obj is Step other && Equals(other);
            public override int GetHashCode() => ((int)Kind * 397) ^ (Json?.GetHashCode() ?? 0);
            public override string ToString() => Kind + (Json == null ? "" : " " + Json);
        }

        private const float IntervalSeconds = 1f;
        private const float ObserverWindowSeconds = 60f;

        private string lastJson;
        private float lastSendAt = float.NegativeInfinity;
        private bool resend;
        // A change that is not built yet: it waits for the retry, or its build threw. The next build takes it.
        private bool pendingDirty;
        private float nextBuildAt = float.NegativeInfinity;

        private float observerWindowStart = float.NaN;
        private int observerRuns;
        private int observerPasses;

        public Step Tick(float now, bool dirty, Func<string> build)
        {
            pendingDirty |= dirty;
            bool due = now - lastSendAt >= IntervalSeconds;
            bool canBuild = now >= nextBuildAt;

            if (resend)
            {
                if (!due) return new Step(Kind.None, null);
                if (pendingDirty || lastJson == null)
                {
                    if (!canBuild) return new Step(Kind.None, null);
                    lastJson = Build(now, build);
                }
                resend = false;
                lastSendAt = now;
                return new Step(Kind.Push, lastJson);
            }

            if (pendingDirty && canBuild)
            {
                string json = Build(now, build);
                if (json != lastJson)
                {
                    lastJson = json;
                    lastSendAt = now;
                    return new Step(Kind.Push, json);
                }
            }

            if (lastJson != null && due)
            {
                lastSendAt = now;
                return new Step(Kind.Check, null);
            }
            return new Step(Kind.None, null);
        }

        // Builds the data. The change stays pending until a build succeeds.
        private string Build(float now, Func<string> build)
        {
            try
            {
                string json = build();
                pendingDirty = false;
                return json;
            }
            catch
            {
                nextBuildAt = now + IntervalSeconds;
                throw;
            }
        }

        // The last JSON (kept) is sent again by the first tick one real second after the last send.
        public void ForgetLastPush() => resend = true;

        // The last pushed JSON, or null before the first push.
        public string LastJson => lastJson;

        // Reads the answer of check(): "ok <runs> <passes>", or "ok" with no counts.
        public static (bool Ok, int ObserverRuns, int Passes) IsOk(string checkResult)
        {
            if (checkResult == null) return (false, 0, 0);
            if (checkResult == "ok") return (true, 0, 0);
            string[] parts = checkResult.Split(' ');
            if (parts.Length == 3 && parts[0] == "ok"
                && int.TryParse(parts[1], out int runs) && int.TryParse(parts[2], out int passes))
                return (true, runs, passes);
            return (false, 0, 0);
        }

        // Adds the counts of one check, and gives the sums once each real minute (else null).
        public (int Runs, int Passes)? ObserverSum(float now, int runs, int passes)
        {
            if (float.IsNaN(observerWindowStart)) observerWindowStart = now;
            observerRuns += runs;
            observerPasses += passes;
            if (now - observerWindowStart < ObserverWindowSeconds) return null;
            var sum = (observerRuns, observerPasses);
            observerWindowStart = now;
            observerRuns = 0;
            observerPasses = 0;
            return sum;
        }
    }
}
