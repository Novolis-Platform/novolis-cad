using Novolis.Cad.Hierarchy;
using Novolis.Cad.Visibility;

namespace Novolis.Cad.Unit;

public class MultiDocumentIsolationTests
{
    [Test]
    public async Task Two_Registries_Have_Independent_Visibility()
    {
        var a = Build("A-ROOT", "A-CHILD");
        var b = Build("B-ROOT", "B-CHILD");

        var visA = new HierarchyVisibilityService();
        var visB = new HierarchyVisibilityService();
        visA.BindHierarchy(a, rootId: "A-ROOT");
        visB.BindHierarchy(b, rootId: "B-ROOT");

        visA.SetLocal("A-CHILD", LocalVisibility.Hidden, false);
        await Assert.That(visA.IsEffectivelyVisible("A-CHILD")).IsFalse();
        await Assert.That(visB.IsEffectivelyVisible("B-CHILD")).IsTrue();

        visB.SetLocal("B-CHILD", LocalVisibility.Hidden, false);
        await Assert.That(visB.IsEffectivelyVisible("B-CHILD")).IsFalse();
        await Assert.That(visA.GetLocal("A-CHILD")).IsEqualTo(LocalVisibility.Hidden);
    }

    private static CadHierarchyRegistry Build(string root, string child)
    {
        var reg = new CadHierarchyRegistry { RootId = root };
        reg.RegisterAssembly(new CadHierarchyAssembly(AssemblyId.Of(root), root));
        reg.RegisterAssembly(new CadHierarchyAssembly(
            AssemblyId.Of(child), child, ParentAssemblyId: AssemblyId.Of(root)));
        return reg;
    }
}
