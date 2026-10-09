using System;
using System.Collections.Generic;
using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 表单容器（§4.5.2 契约 Form 校验 / 值快照）
///
/// <para>行容器,逐行 <see cref="Field"/> 子节点:</para>
/// <list type="bullet">
///   <item>行 separation 走 <c>ParagraphSpacing</c>(8)</item>
///   <item><see cref="Validate"/> 遍历所有 Field,TextField 走 <c>IsNullOrWhiteSpace</c> 校验</item>
///   <item><see cref="Snapshot"/> 回调把当前值以 <c>(label, value)</c> 元组列表形式抛给调用方</item>
/// </list>
///
/// <para>调用方拿到 snapshot 序列化为 yaml / json,或转 <see cref="WorldView.Domain.WorldSettings"/>。</para>
/// </summary>
public partial class JrpgForm : VBoxContainer
{
    /// <summary>校验失败事件(label + reason)。</summary>
    [Signal] public delegate void ValidationFailedEventHandler(string label, string reason);

    private readonly List<Field> _fields = new();

    /// <inheritdoc/>
    public JrpgForm()
    {
        AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.ParagraphSpacing);
    }

    /// <summary>语义化:添加一个 Field。</summary>
    public JrpgForm AddField(Field field)
    {
        _fields.Add(field);
        AddChild(field);
        return this;
    }

    /// <summary>语义化:批量添加 Fields。</summary>
    public JrpgForm AddFields(IEnumerable<Field> fields)
    {
        foreach (var f in fields) AddField(f);
        return this;
    }

    /// <summary>校验所有 Field:全部 <c>IsValid</c> 为 true 才返 true,否则 emit ValidationFailed。</summary>
    public bool Validate()
    {
        foreach (var f in _fields)
        {
            if (!f.IsValid)
            {
                EmitSignal(SignalName.ValidationFailed, f.Label, "必填字段为空");
                return false;
            }
        }
        return true;
    }

    /// <summary>值快照回调:把每个 Field 当前值以 (label, value) 元组列表抛给 handler。</summary>
    public void Snapshot(Action<IReadOnlyList<(string Label, string Value)>> handler)
    {
        var snap = new List<(string Label, string Value)>(_fields.Count);
        foreach (var f in _fields)
        {
            snap.Add((f.Label, f.Value));
        }
        handler(snap);
    }
}
