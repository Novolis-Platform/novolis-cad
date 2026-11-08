using Novolis.Cad.Hierarchy;
using Novolis.Cad.Visibility;

namespace Novolis.Cad.Unit;

public class HierarchyVisibilityTests
{
    [Test]
    public async Task Child_Inherit_Under_Hidden_Parent_Is_Hidden()
    {
        var reg = BuildTree();
        var vis = new HierarchyVisibilityService();
        vis.BindHierarchy(reg, rootId: "ROOT");
        vis.SetLocal("ROOT", LocalVisibility.Visible, false);
        vis.SetLocal("PARENT", LocalVisibility.Hidden, false);
        vis.SetLocal("CHILD", LocalVisibility.Inherit, false);
        await Assert.That(vis.IsEffectivelyVisible("CHILD")).IsFalse();
    }

    [Test]
    public async Task Child_Visible_Overrides_Hidden_Parent()
    {
        var reg = BuildTree();
        var vis = new HierarchyVisibilityService();
        vis.BindHierarchy(reg, rootId: "ROOT");
        vis.SetLocal("PARENT", LocalVisibility.Hidden, false);
        vis.SetLocal("CHILD", LocalVisibility.Visible, false);
        await Assert.That(vis.IsEffectivelyVisible("CHILD")).IsTrue();
    }

    [Test]
    public async Task Isolation_Restores_Prior_Snapshot()
    {
        var reg = BuildTree();
        var vis = new HierarchyVisibilityService();
        vis.BindHierarchy(reg, rootId: "ROOT");
        vis.SetLocal("PARENT", LocalVisibility.Hidden, false);
        vis.BeginIsolation("CHILD");
        await Assert.That(vis.IsolationActive).IsTrue();
        await Assert.That(vis.IsEffectivelyVisible("CHILD")).IsTrue();
        vis.EndIsolation();
        await Assert.That(vis.IsolationActive).IsFalse();
        await Assert.That(vis.GetLocal("PARENT")).IsEqualTo(LocalVisibility.Hidden);
    }

    private static CadHierarchyRegistry BuildTree()
    {
        var reg = new CadHierarchyRegistry { RootId = "ROOT" };
        reg.RegisterAssembly(new CadHierarchyAssembly(AssemblyId.Of("ROOT"), "Root"));
        reg.RegisterAssembly(new CadHierarchyAssembly(
            AssemblyId.Of("PARENT"), "Parent", ParentAssemblyId: AssemblyId.Of("ROOT")));
        reg.RegisterAssembly(new CadHierarchyAssembly(
            AssemblyId.Of("CHILD"), "Child", ParentAssemblyId: AssemblyId.Of("PARENT")));
        return reg;
    }
}
