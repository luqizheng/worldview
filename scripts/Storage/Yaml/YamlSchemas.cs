using System.Collections;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using WorldView.Domain;

namespace WorldView.Storage.Yaml;

/// <summary>
/// YAML 全局命名约定 + GridCoord 的 [x, y] 数组序列化器。
/// MVP 规则：
/// <list type="bullet">
///   <item>C# PascalCase ↔ YAML snake_case（<c>UnderscoredNamingConvention</c>）</item>
///   <item><see cref="GridCoord"/> 序列化为 <c>[x, y]</c> 数组，反序列化时同样接受 <c>[x, y]</c>（spec.md §2.5）</item>
///   <item>未匹配字段忽略（<c>IgnoreUnmatchedProperties</c>）— 让 YAML 字段比 C# 模型多时仍能加载</item>
/// </list>
///
/// <para>所有 Domain 不可变 record 均提供显式无参构造器（YAML 反序列化由 YamlDotNet 默认
/// <c>Activator.CreateInstance</c> 完成），无需要额外对象工厂。</para>
/// </summary>
public static class YamlSchemas
{
    /// <summary>
    /// 主反序列化器（用于 World / MapDoc / LayerDoc / TerrainCellEntry / Building / MapIndexEntry）。
    /// </summary>
    public static IDeserializer BuildDeserializer() => new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .WithTypeConverter(new GridCoordYamlConverter())
        .WithTypeConverter(new ReadOnlyListYamlConverter())
        .Build();

    /// <summary>
    /// 主序列化器（写回 YAML）。
    /// </summary>
    public static ISerializer BuildSerializer() => new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new GridCoordYamlConverter())
        .WithTypeConverter(new ReadOnlyListYamlConverter())
        .Build();

    /// <summary>
    /// YamlDotNet 16.3.0 中 grid.{width,height,...} 等嵌套结构由 naming convention 自动转换 key。
    /// </summary>
    public static T Deserialize<T>(string yaml) => BuildDeserializer().Deserialize<T>(yaml);

    public static string Serialize<T>(T obj) => BuildSerializer().Serialize(obj);
}

/// <summary>
/// <see cref="GridCoord"/> ↔ YAML 数组 <c>[x, y]</c> 的双向转换器。
/// 原因：GridCoord 在 C# 是 <c>readonly record struct</c>，默认 name 记录就是结构体本身，
/// 但 spec.md §2.5 要求序列化为数组形式（紧凑 + 与 PositionField 一致）。
/// </summary>
internal sealed class GridCoordYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        // 同时接受 GridCoord 与 Nullable<GridCoord>
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(GridCoord);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        // 处理 null/空：parser 停在 null 标量（写入时 origin 为 null 会序列化为 null/empty）
        if (parser.Current is Scalar nullScalar && (nullScalar.Value == null || nullScalar.Value == "~" || nullScalar.Value == "null"))
        {
            parser.MoveNext();
            return null;
        }

        // 支持两种 YAML 形态：
        //   1) 序列形式：{ origin: [0, 0] }  (spec.md §2.5 标准形式)
        //   2) 映射形式：{ origin: { x: 0, y: 0 } }  (YAML 流映射写法)
        if (parser.Current is SequenceStart)
        {
            parser.MoveNext();
            if (parser.Current is not Scalar sx)
                throw new YamlException($"期望 GridCoord 第 1 项为整数标量，得到 {parser.Current?.GetType().Name ?? "EOF"}");
            parser.MoveNext();
            if (parser.Current is not Scalar sy)
                throw new YamlException($"期望 GridCoord 第 2 项为整数标量，得到 {parser.Current?.GetType().Name ?? "EOF"}");
            parser.MoveNext(); // skip sy
            parser.MoveNext(); // consume sequence end
            return new GridCoord(int.Parse(sx.Value), int.Parse(sy.Value));
        }
        if (parser.Current is MappingStart)
        {
            int x = 0, y = 0;
            parser.MoveNext();
            while (parser.Current is not MappingEnd)
            {
                if (parser.Current is not Scalar key)
                    throw new YamlException($"期望 GridCoord 映射 key 为标量，得到 {parser.Current?.GetType().Name ?? "EOF"}");
                parser.MoveNext();
                if (parser.Current is not Scalar v)
                    throw new YamlException($"期望 GridCoord 映射 value 为整数标量，得到 {parser.Current?.GetType().Name ?? "EOF"}");
                if (key.Value == "x") x = int.Parse(v.Value);
                else if (key.Value == "y") y = int.Parse(v.Value);
                parser.MoveNext();
            }
            parser.MoveNext(); // consume mapping end
            return new GridCoord(x, y);
        }
        throw new YamlException($"期望 GridCoord 应序列化为 [x, y] 或 {{x,y}}，但得到 {parser.Current?.GetType().Name ?? "EOF"}");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is null)
        {
            emitter.Emit(new Scalar(null));
            return;
        }
        if (value is not GridCoord c) throw new YamlException($"GridCoord 序列化收到非 GridCoord: {value?.GetType().Name ?? "null"}");
        emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Flow));
        serializer(c.X, typeof(int));
        serializer(c.Y, typeof(int));
        emitter.Emit(new SequenceEnd());
    }
}

/// <summary>
/// <c>IReadOnlyList&lt;T&gt;</c> ↔ YAML 序列的双向转换器。
/// 原因：YamlDotNet 16.3.0 内置只支持 <c>T[]</c> / <c>List&lt;T&gt;</c> / <c>IList&lt;T&gt;</c> / <c>ICollection&lt;T&gt;</c>，
/// 不支持只读接口。本项目 Domain 层使用不可变 record，把集合表达为 <c>IReadOnlyList&lt;T&gt;</c>。
/// </summary>
internal sealed class ReadOnlyListYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var elementType = type.GetGenericArguments()[0];
        // 关键：返回类型必须是 IReadOnlyList<T>，否则 (IReadOnlyList<T>)IList 强制转型失败
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

        if (parser.Current is SequenceStart)
        {
            parser.MoveNext();
        }
        while (parser.Current is not SequenceEnd)
        {
            var item = rootDeserializer(elementType);
            list.Add(item);
        }
        parser.MoveNext(); // 消费 SequenceEnd
        return list; // List<T> 同时实现 IList<T> + IReadOnlyList<T>，运行时转型由 YamlDotNet 处理
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Block));
        foreach (var item in (IEnumerable)value!)
        {
            serializer(item, item?.GetType() ?? typeof(object));
        }
        emitter.Emit(new SequenceEnd());
    }
}