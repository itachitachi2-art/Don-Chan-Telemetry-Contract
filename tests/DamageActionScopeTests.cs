using System;
using DonChan.TelemetryProbe;

internal static class DamageActionScopeTests
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _checks++;
    }
    public static void Main()
    {
        Check(DamageActionScope.Current == null, "initial scope empty");
        var outer = DamageActionScope.Begin("session");
        string parentId = DamageActionScope.Current;
        Check(parentId.StartsWith("session:damage:"), "wire ID has session prefix");
        var inner = DamageActionScope.Begin("session");
        Check(DamageActionScope.Current != parentId, "nested damage has independent ID");
        Check(DamageActionScope.End(inner, null) == null, "successful finalizer returns no exception");
        Check(DamageActionScope.Current == parentId, "nested finalizer restores parent");
        DamageActionScope.End(null, null);
        Check(DamageActionScope.Current == parentId, "skipped prefix finalizer preserves parent");
        var failure = new InvalidOperationException("original game exception");
        Check(Object.ReferenceEquals(DamageActionScope.End(null, failure), failure), "skipped prefix preserves exception");
        Check(DamageActionScope.Current == parentId, "skipped exceptional prefix preserves parent");
        var failingInner = DamageActionScope.Begin("session");
        Check(Object.ReferenceEquals(DamageActionScope.End(failingInner, failure), failure), "exception identity preserved");
        Check(DamageActionScope.Current == parentId, "exceptional nested finalizer restores parent");
        DamageActionScope.End(outer, failure);
        Check(DamageActionScope.Current == null, "exceptional outer finalizer clears scope");
        var next = DamageActionScope.Begin("session");
        string nextId = DamageActionScope.Current;
        DamageActionScope.End(outer, failure);
        Check(DamageActionScope.Current == nextId, "repeated finalizer does not overwrite newer scope");
        DamageActionScope.End(next, null);
        Check(DamageActionScope.Current == null, "next operation leaves no stale ID");
        Console.WriteLine("Damage action scope: " + _checks + " assertions passed.");
    }
}
