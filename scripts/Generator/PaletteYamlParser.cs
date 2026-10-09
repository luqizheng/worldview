namespace WorldView.Generator;

/// <summary>
/// 解析 parchment_palette.yaml 的 ui.* 节点到强类型 <see cref="PaletteEntry"/> 列表。
/// 自写最小 yaml 解析（仅支持两层 mapping + string scalar），不依赖 YamlDotNet——
/// 因为 YamlDotNet 不能在 Generator analyzer 里随 dll 一起被 csc 加载（CS8785）。
/// 不读文件、不依赖 Godot 命名空间。
/// </summary>
public static class PaletteYamlParser
{
    /// <summary>Diagnostic 错误码</summary>
    public const string WVP001 = "WVP001";
    public const string WVP002 = "WVP002";
    public const string WVP003 = "WVP003";
    public const string WVP004 = "WVP004";
    public const string WVP005 = "WVP005";
    public const string WVP006 = "WVP006";

    /// <summary>
    /// 解析单个 ui 字段（key 形如 "ui.color.jrpg_bg" / "ui.metric.font_size_body"）。
    /// 失败时把诊断信息累积进 <paramref name="errors"/>。
    /// </summary>
    public static bool TryParseEntry(string key, string raw, List<string> errors, out PaletteEntry? entry)
    {
        entry = null;
        var s = raw.Trim();
        // 去引号（yaml 标量值常被 "..." 或 '...' 包裹）
        if (s.Length >= 2 && (s[0] == '"' || s[0] == '\'') && s[^1] == s[0])
        {
            s = s.Substring(1, s.Length - 2);
        }

        // 命名规则: ui.color.* 必须以 jrpg_ / accent_ 开头
        if (key.StartsWith("ui.color."))
        {
            var tail = key.Substring("ui.color.".Length);
            if (!System.Text.RegularExpressions.Regex.IsMatch(tail, "^(jrpg_|accent_)[a-z0-9_]+$"))
            {
                errors.Add($"{WVP006} {key} ui.color.* 命名必须以 jrpg_ 或 accent_ 开头");
                return false;
            }
        }

        // 路径必须落在允许的 5 段之下
        var root = key.Split('.')[0] + "." + key.Split('.')[1];
        if (root is not ("ui.color" or "ui.metric" or "ui.shadow" or "ui.modal" or "ui.animation"))
        {
            errors.Add($"{WVP005} {key} 路径必须在 ui.color / ui.metric / ui.shadow / ui.modal / ui.animation 之下");
            return false;
        }

        // 色：#RRGGBB 或 #RRGGBBAA（拒收 #fff 短格式）
        if (root == "ui.color")
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(s, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$"))
            {
                errors.Add($"{WVP004} {key} 色值格式错误（须为 #RRGGBB 或 #RRGGBBAA），got: {s}");
                return false;
            }
            entry = new PaletteEntry(key, s.ToLowerInvariant(), EntryKind.Color);
            return true;
        }

        // 量化 / 阴影 / 模态 / 动画：必须可解析为 int 或 float
        if (int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i))
        {
            entry = new PaletteEntry(key, i.ToString(System.Globalization.CultureInfo.InvariantCulture), EntryKind.Int);
            return true;
        }
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            entry = new PaletteEntry(key, d.ToString("R", System.Globalization.CultureInfo.InvariantCulture), EntryKind.Float);
            return true;
        }
        errors.Add($"{WVP003} {key} 值类型不是 int/float (got \"{s}\")");
        return false;
    }

    /// <summary>
    /// 从 yaml 文本解析 ui 节点下所有 token。
    /// 极简解析器：只识别顶层 mapping 的 "ui" key 下嵌套的二级 mapping（color/metric/shadow/modal/animation），
    /// 拒绝列表、复杂结构、跨行字符串。
    /// </summary>
    public static List<PaletteEntry> Parse(string yamlText, out List<string> errors)
    {
        errors = new List<string>();
        var result = new List<PaletteEntry>();
        if (string.IsNullOrWhiteSpace(yamlText))
        {
            errors.Add($"{WVP001} yaml 文本为空");
            return result;
        }

        // 1. 找到 "ui:" 顶层 key（必须是缩进为 0 的 mapping entry）
        int uiIndent = -1;
        int uiLine = -1;
        var lines = yamlText.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || line[0] == '#') continue;
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ') indent++;
            var trimmed = line.Substring(indent);
            if (indent == 0 && trimmed.StartsWith("ui:"))
            {
                uiIndent = 0;
                uiLine = i;
                break;
            }
        }
        if (uiLine < 0)
        {
            // 没有 ui 节点：空集合
            return result;
        }

        // 2. 解析 ui 下的二级 mapping：识别缩进为 2（相对 ui 缩进 +2）的 keys
        int childIndent = -1;
        var children = new Dictionary<string, List<(int Indent, string Key, string Value)>>();
        string? currentChild = null;
        for (int i = uiLine + 1; i < lines.Length; i++)
        {
            var raw = lines[i];
            if (raw.Length == 0) continue;
            // 跳过空行和注释
            var stripped = raw.TrimStart();
            if (stripped.Length == 0 || stripped[0] == '#') continue;

            int indent = raw.Length - stripped.Length;
            var content = stripped;
            if (indent == 0) break; // 退出 ui 节点
            if (childIndent < 0)
            {
                if (content.EndsWith(":"))
                {
                    childIndent = indent;
                    currentChild = content.Substring(0, content.Length - 1);
                    children[currentChild] = new List<(int, string, string)>();
                }
                continue;
            }
            if (indent == childIndent)
            {
                if (content.EndsWith(":"))
                {
                    currentChild = content.Substring(0, content.Length - 1);
                    children[currentChild] = new List<(int, string, string)>();
                }
                else
                {
                    // 单层 (ui.color: "#hex") 形式 — 暂时不支持
                    errors.Add($"{WVP002} {content} 顶层 ui 节点必须是 mapping（不接受单层字符串）");
                }
            }
            else if (indent > childIndent)
            {
                // 二级 key: value
                if (currentChild is null) continue;
                var colonIdx = content.IndexOf(':');
                if (colonIdx < 0) continue;
                var key = content.Substring(0, colonIdx).Trim();
                var value = content.Substring(colonIdx + 1).Trim();
                // 去掉行尾注释: 必须前面是空白,避免误删色值开头的 '#'
                int hashIdx = -1;
                for (int k = 0; k < value.Length; k++)
                {
                    if (value[k] == '#' && (k == 0 || char.IsWhiteSpace(value[k - 1])))
                    {
                        hashIdx = k;
                        break;
                    }
                }
                if (hashIdx >= 0) value = value.Substring(0, hashIdx).Trim();
                if (!children.TryGetValue(currentChild, out var list)) continue;
                list.Add((indent, key, value));
            }
            else
            {
                break;
            }
        }

        // 3. 把 children 展平为 fullKey -> value
        foreach (var (childName, entries) in children)
        {
            foreach (var (_, key, value) in entries)
            {
                if (value.Length == 0) continue;
                var fullKey = $"ui.{childName}.{key}";
                if (TryParseEntry(fullKey, value, errors, out var e) && e is not null) result.Add(e);
            }
        }

        return result;
    }
}

/// <summary>单条 token</summary>
public sealed record PaletteEntry(string Key, string Literal, EntryKind Kind);

public enum EntryKind { Color, Int, Float }
