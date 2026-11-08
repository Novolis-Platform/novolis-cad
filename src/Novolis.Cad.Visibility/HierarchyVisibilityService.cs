using Novolis.Cad.Hierarchy;
using Novolis.Cad.Primitives;

namespace Novolis.Cad.Visibility;

/// <summary>
/// Sole authority for physical/reference object visibility. Local Inherit/Visible/Hidden;
/// effective visibility walks ancestors until an explicit state (root defaults Visible).
/// A descendant with explicit Visible overrides a Hidden ancestor.
/// </summary>
public sealed class HierarchyVisibilityService
{
    private readonly Dictionary<string, LocalVisibility> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _effectiveCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, string> _entityToNode = new();
    private readonly Stack<VisibilitySnapshot> _isolationStack = new();
    private readonly Stack<VisibilitySnapshot> _undo = new();
    private readonly Stack<VisibilitySnapshot> _redo = new();
    private HashSet<Guid>? _entityReveal;
    private ICadHierarchySource? _hierarchy;
    private string _rootId = "ROOT";
    private string _unclassifiedFallbackId = "DIAGNOSTICS";

    public event Action? Changed;

    public bool IsolationActive => _isolationStack.Count > 0 || _entityReveal is not null;
    public string? ActivePresetLabel { get; private set; }
    public bool HasEntityRevealFilter => _entityReveal is not null;

    public void BindHierarchy(
        ICadHierarchySource hierarchy,
        CadDocument? document = null,
        string? rootId = null,
        string? unclassifiedFallbackId = null)
    {
        _hierarchy = hierarchy;
        _rootId = rootId ?? hierarchy.RootId;
        if (!string.IsNullOrWhiteSpace(unclassifiedFallbackId))
            _unclassifiedFallbackId = unclassifiedFallbackId!;
        RebuildEntityMap(document);
        InvalidateCache();
        Changed?.Invoke();
    }

    public LocalVisibility GetLocal(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return LocalVisibility.Inherit;
        return _overrides.TryGetValue(nodeId, out var v) ? v : LocalVisibility.Inherit;
    }

    public bool IsEffectivelyVisible(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return true;
        if (_effectiveCache.TryGetValue(nodeId, out var cached))
            return cached;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = ResolveWalk(nodeId, visited);
        _effectiveCache[nodeId] = result;
        return result;
    }

    public bool IsEntityEffectivelyVisible(Guid entityId)
    {
        if (_entityReveal is not null && !_entityReveal.Contains(entityId))
            return false;

        if (_entityToNode.TryGetValue(entityId, out var nodeId))
            return IsEffectivelyVisible(nodeId);

        return IsEffectivelyVisible(_unclassifiedFallbackId);
    }

    public void SetEntityRevealFilter(IEnumerable<Guid>? entityIds)
    {
        _entityReveal = entityIds is null ? null : new HashSet<Guid>(entityIds);
        Changed?.Invoke();
    }

    public void ClearEntityRevealFilter()
    {
        if (_entityReveal is null)
            return;
        _entityReveal = null;
        Changed?.Invoke();
    }

    public void SetLocal(string nodeId, LocalVisibility state, bool recordUndo = true)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;
        if (recordUndo)
            PushUndo();

        if (state == LocalVisibility.Inherit)
            _overrides.Remove(nodeId);
        else
            _overrides[nodeId] = state;

        ActivePresetLabel = null;
        InvalidateCache();
        Changed?.Invoke();
    }

    public void ClearAllOverrides(bool recordUndo = true)
    {
        if (recordUndo)
            PushUndo();
        _overrides.Clear();
        ActivePresetLabel = null;
        InvalidateCache();
        Changed?.Invoke();
    }

    public void ApplySnapshot(VisibilitySnapshot snapshot, bool recordUndo = true, string? asPreset = null)
    {
        if (recordUndo)
            PushUndo();
        _overrides.Clear();
        _entityReveal = null;
        foreach (var (k, v) in snapshot.Overrides)
        {
            if (v != LocalVisibility.Inherit)
                _overrides[k] = v;
        }

        ActivePresetLabel = asPreset ?? snapshot.Label;
        InvalidateCache();
        Changed?.Invoke();
    }

    public VisibilitySnapshot CaptureSnapshot(string? label = null) =>
        new()
        {
            Label = label ?? ActivePresetLabel,
            Overrides = new Dictionary<string, LocalVisibility>(_overrides, StringComparer.OrdinalIgnoreCase),
        };

    public void BeginIsolation(string selectedNodeId)
    {
        if (_hierarchy is null || string.IsNullOrWhiteSpace(selectedNodeId))
            return;
        _isolationStack.Push(CaptureSnapshot("pre-isolation"));
        PushUndo();

        _overrides.Clear();
        _entityReveal = null;
        HideAllTopLevelExcept(selectedNodeId);
        EnsureAncestorsVisible(selectedNodeId);
        SetLocal(selectedNodeId, LocalVisibility.Visible, recordUndo: false);
        HideUnrelatedBranches(selectedNodeId);

        ActivePresetLabel = $"isolate:{selectedNodeId}";
        InvalidateCache();
        Changed?.Invoke();
    }

    public void EndIsolation()
    {
        if (_entityReveal is not null)
            ClearEntityRevealFilter();
        if (_isolationStack.Count == 0)
            return;
        var prior = _isolationStack.Pop();
        ApplySnapshot(prior, recordUndo: true, asPreset: prior.Label);
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;
        _redo.Push(CaptureSnapshot());
        ApplySnapshot(_undo.Pop(), recordUndo: false);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;
        _undo.Push(CaptureSnapshot());
        ApplySnapshot(_redo.Pop(), recordUndo: false);
    }

    public ChildVisibilitySummary SummarizeChildren(string nodeId)
    {
        if (_hierarchy is null || string.IsNullOrWhiteSpace(nodeId))
            return ChildVisibilitySummary.None;
        var aid = AssemblyId.Of(nodeId);
        if (!_hierarchy.TryGetAssembly(aid, out _))
            return ChildVisibilitySummary.None;

        var anyVis = false;
        var anyHid = false;
        foreach (var child in _hierarchy.ChildAssemblies(aid))
        {
            if (IsEffectivelyVisible(child.Id.Value))
                anyVis = true;
            else
                anyHid = true;
        }

        foreach (var part in _hierarchy.PartsOf(aid))
        {
            if (IsEffectivelyVisible(part.Id.Value))
                anyVis = true;
            else
                anyHid = true;
        }

        if (anyVis && anyHid)
            return ChildVisibilitySummary.Mixed;
        if (anyHid)
            return ChildVisibilitySummary.AllHidden;
        if (anyVis)
            return ChildVisibilitySummary.AllVisible;
        return ChildVisibilitySummary.None;
    }

    public string? InheritedFrom(string nodeId)
    {
        if (GetLocal(nodeId) != LocalVisibility.Inherit)
            return null;
        var parent = ParentOf(nodeId);
        while (parent is not null)
        {
            if (GetLocal(parent) != LocalVisibility.Inherit)
                return parent;
            parent = ParentOf(parent);
        }

        return _rootId;
    }

    public void RebuildEntityMap(CadDocument? document = null)
    {
        _entityToNode.Clear();
        if (_hierarchy is null)
            return;

        if (_hierarchy is CadHierarchyRegistry reg)
        {
            foreach (var part in reg.Parts)
            {
                foreach (var eid in reg.CollectEntityIds(part.Id))
                    _entityToNode[eid] = part.Id.Value;
            }

            foreach (var reference in reg.References)
            {
                if (reference.EntityId is { } eid)
                    _entityToNode[eid] = reference.Id;
            }

            foreach (var asm in reg.Assemblies)
            {
                foreach (var eid in reg.CollectEntityIds(asm.Id))
                {
                    if (!_entityToNode.ContainsKey(eid))
                        _entityToNode[eid] = asm.Id.Value;
                }
            }
        }

        if (document?.Entities is null)
            return;
        foreach (var e in document.Entities)
        {
            if (_entityToNode.ContainsKey(e.Id))
                continue;
            _entityToNode[e.Id] = _unclassifiedFallbackId;
        }
    }

    public string? SemanticNodeForEntity(Guid entityId) =>
        _entityToNode.TryGetValue(entityId, out var id) ? id : null;

    private bool ResolveWalk(string nodeId, HashSet<string> visited)
    {
        if (!visited.Add(nodeId))
            return true;

        var local = GetLocal(nodeId);
        if (local == LocalVisibility.Visible)
            return true;
        if (local == LocalVisibility.Hidden)
            return false;

        if (string.Equals(nodeId, _rootId, StringComparison.OrdinalIgnoreCase))
            return true;

        var parent = ParentOf(nodeId);
        if (parent is null)
            return true;
        return ResolveWalk(parent, visited);
    }

    private string? ParentOf(string nodeId) => _hierarchy?.ParentOf(nodeId);

    private void HideAllTopLevelExcept(string keepNodeId)
    {
        if (_hierarchy is null)
            return;
        foreach (var child in _hierarchy.ChildAssemblies(AssemblyId.Of(_rootId)))
        {
            if (IsAncestorOf(child.Id.Value, keepNodeId)
                || string.Equals(child.Id.Value, keepNodeId, StringComparison.OrdinalIgnoreCase))
                continue;
            _overrides[child.Id.Value] = LocalVisibility.Hidden;
        }
    }

    private void EnsureAncestorsVisible(string nodeId)
    {
        var p = ParentOf(nodeId);
        while (p is not null)
        {
            _overrides[p] = LocalVisibility.Visible;
            p = ParentOf(p);
        }

        _overrides[_rootId] = LocalVisibility.Visible;
    }

    private void HideUnrelatedBranches(string selectedNodeId)
    {
        if (_hierarchy is null)
            return;
        var current = selectedNodeId;
        while (true)
        {
            var parent = ParentOf(current);
            if (parent is null)
                break;
            var paid = AssemblyId.Of(parent);
            if (!_hierarchy.TryGetAssembly(paid, out _))
                break;
            foreach (var sib in _hierarchy.ChildAssemblies(paid))
            {
                if (string.Equals(sib.Id.Value, current, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (IsAncestorOf(sib.Id.Value, selectedNodeId))
                    continue;
                _overrides[sib.Id.Value] = LocalVisibility.Hidden;
            }

            foreach (var part in _hierarchy.PartsOf(paid))
            {
                if (string.Equals(part.Id.Value, current, StringComparison.OrdinalIgnoreCase))
                    continue;
                _overrides[part.Id.Value] = LocalVisibility.Hidden;
            }

            current = parent;
        }
    }

    private bool IsAncestorOf(string ancestorId, string descendantId)
    {
        var p = ParentOf(descendantId);
        while (p is not null)
        {
            if (string.Equals(p, ancestorId, StringComparison.OrdinalIgnoreCase))
                return true;
            p = ParentOf(p);
        }

        return false;
    }

    private void PushUndo()
    {
        _undo.Push(CaptureSnapshot());
        _redo.Clear();
    }

    private void InvalidateCache() => _effectiveCache.Clear();
}
