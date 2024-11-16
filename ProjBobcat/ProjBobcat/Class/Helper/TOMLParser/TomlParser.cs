using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable

namespace ProjBobcat.Class.Helper.TOMLParser;

#region TOML Nodes

public abstract class TomlNode : IEnumerable
{
    public virtual bool HasValue { get; } = false;
    public virtual bool IsArray { get; } = false;
    public virtual bool IsTable { get; } = false;
    public virtual bool IsString { get; } = false;
    public virtual bool IsInteger { get; } = false;
    public virtual bool IsFloat { get; } = false;
    public virtual bool IsDateTime { get; } = false;
    public virtual bool IsBoolean { get; } = false;
    public virtual string Comment { get; set; }
    public virtual int CollapseLevel { get; set; }

    public virtual TomlTable? AsTable => this as TomlTable;
    public virtual TomlString? AsString => this as TomlString;
    public virtual TomlInteger? AsInteger => this as TomlInteger;
    public virtual TomlFloat? AsFloat => this as TomlFloat;
    public virtual TomlBoolean? AsBoolean => this as TomlBoolean;
    public virtual TomlDateTime? AsDateTime => this as TomlDateTime;
    public virtual TomlArray? AsArray => this as TomlArray;

    public virtual int ChildrenCount => 0;

    public virtual TomlNode? this[string key]
    {
        get => null;
        set { }
    }

    public virtual TomlNode? this[int index]
    {
        get => null;
        set { }
    }

    public virtual IEnumerable<TomlNode> Children
    {
        get { yield break; }
    }

    public virtual IEnumerable<string> Keys
    {
        get { yield break; }
    }

    public IEnumerator GetEnumerator()
    {
        return this.Children.GetEnumerator();
    }

    public virtual bool TryGetNode(string key, out TomlNode? node)
    {
        node = null;
        return false;
    }

    public virtual bool HasKey(string key)
    {
        return false;
    }

    public virtual bool HasItemAt(int index)
    {
        return false;
    }

    public virtual void Add(string key, TomlNode node)
    {
    }

    public virtual void Add(TomlNode node)
    {
    }

    public virtual void Delete(TomlNode node)
    {
    }

    public virtual void Delete(string key)
    {
    }

    public virtual void Delete(int index)
    {
    }

    public virtual void AddRange(IEnumerable<TomlNode> nodes)
    {
        foreach (var tomlNode in nodes) this.Add(tomlNode);
    }

    public virtual void WriteTo(TextWriter tw, string name = null)
    {
        tw.WriteLine(this.ToInlineToml());
    }

    public virtual string ToInlineToml()
    {
        return this.ToString();
    }

    #region Native type to TOML cast

    public static implicit operator TomlNode(string value)
    {
        return new TomlString { Value = value };
    }

    public static implicit operator TomlNode(bool value)
    {
        return new TomlBoolean { Value = value };
    }

    public static implicit operator TomlNode(long value)
    {
        return new TomlInteger { Value = value };
    }

    public static implicit operator TomlNode(float value)
    {
        return new TomlFloat { Value = value };
    }

    public static implicit operator TomlNode(double value)
    {
        return new TomlFloat { Value = value };
    }

    public static implicit operator TomlNode(DateTime value)
    {
        return new TomlDateTime { Value = value };
    }

    public static implicit operator TomlNode(TomlNode[] nodes)
    {
        var result = new TomlArray();
        result.AddRange(nodes);
        return result;
    }

    #endregion

    #region TOML to native type cast

    public static implicit operator string(TomlNode value)
    {
        return value.ToString();
    }

    public static implicit operator int(TomlNode value)
    {
        return (int)value.AsInteger.Value;
    }

    public static implicit operator long(TomlNode value)
    {
        return value.AsInteger.Value;
    }

    public static implicit operator float(TomlNode value)
    {
        return (float)value.AsFloat.Value;
    }

    public static implicit operator double(TomlNode value)
    {
        return value.AsFloat.Value;
    }

    public static implicit operator bool(TomlNode value)
    {
        return value.AsBoolean.Value;
    }

    public static implicit operator DateTime(TomlNode value)
    {
        return value.AsDateTime.Value;
    }

    #endregion
}

public class TomlString : TomlNode
{
    public override bool HasValue { get; } = true;
    public override bool IsString { get; } = true;
    public bool IsMultiline { get; set; }
    public bool PreferLiteral { get; set; }

    public string Value { get; set; }

    public override string ToString()
    {
        return this.Value;
    }

    public override string ToInlineToml()
    {
        if (this.Value.IndexOf(TomlSyntax.LITERAL_STRING_SYMBOL) != -1 && this.PreferLiteral)
            this.PreferLiteral = false;
        var quotes = new string(this.PreferLiteral ? TomlSyntax.LITERAL_STRING_SYMBOL : TomlSyntax.BASIC_STRING_SYMBOL,
            this.IsMultiline ? 3 : 1);
        var result = this.PreferLiteral ? this.Value : this.Value.Escape(!this.IsMultiline);
        return $"{quotes}{result}{quotes}";
    }
}

public class TomlInteger : TomlNode
{
    public enum Base
    {
        Binary = 2,
        Octal = 8,
        Decimal = 10,
        Hexadecimal = 16
    }

    public override bool IsInteger { get; } = true;
    public override bool HasValue { get; } = true;
    public Base IntegerBase { get; set; } = Base.Decimal;

    public long Value { get; set; }

    public override string ToString()
    {
        return this.Value.ToString();
    }

    public override string ToInlineToml()
    {
        return this.IntegerBase != Base.Decimal
            ? $"0{TomlSyntax.BaseIdentifiers[(int)this.IntegerBase]}{Convert.ToString(this.Value, (int)this.IntegerBase)}"
            : this.Value.ToString(CultureInfo.InvariantCulture);
    }
}

public class TomlFloat : TomlNode, IFormattable
{
    public override bool IsFloat { get; } = true;
    public override bool HasValue { get; } = true;

    public double Value { get; set; }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return this.Value.ToString(format, formatProvider);
    }

    public override string ToString()
    {
        return this.Value.ToString(CultureInfo.CurrentCulture);
    }

    public string ToString(IFormatProvider formatProvider)
    {
        return this.Value.ToString(formatProvider);
    }

    public override string ToInlineToml()
    {
        return this.Value switch
        {
            var v when double.IsNaN(v) => TomlSyntax.NAN_VALUE,
            var v when double.IsPositiveInfinity(v) => TomlSyntax.INF_VALUE,
            var v when double.IsPositiveInfinity(v) => TomlSyntax.NEG_INF_VALUE,
            var v => v.ToString("G", CultureInfo.InvariantCulture)
        };
    }
}

public class TomlBoolean : TomlNode
{
    public override bool IsBoolean { get; } = true;
    public override bool HasValue { get; } = true;

    public bool Value { get; set; }

    public override string ToString()
    {
        return this.Value.ToString();
    }

    public override string ToInlineToml()
    {
        return this.Value ? TomlSyntax.TRUE_VALUE : TomlSyntax.FALSE_VALUE;
    }
}

public class TomlDateTime : TomlNode, IFormattable
{
    public override bool IsDateTime { get; } = true;
    public override bool HasValue { get; } = true;
    public bool OnlyDate { get; set; }
    public bool OnlyTime { get; set; }
    public int SecondsPrecision { get; set; }

    public DateTime Value { get; set; }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return this.Value.ToString(format, formatProvider);
    }

    public override string ToString()
    {
        return this.Value.ToString(CultureInfo.CurrentCulture);
    }

    public string ToString(IFormatProvider formatProvider)
    {
        return this.Value.ToString(formatProvider);
    }

    public override string ToInlineToml()
    {
        return this.Value switch
        {
            var v when this.OnlyDate => v.ToString(TomlSyntax.LocalDateFormat),
            var v when this.OnlyTime => v.ToString(TomlSyntax.RFC3339LocalTimeFormats[this.SecondsPrecision]),
            var v when v.Kind is DateTimeKind.Local =>
                v.ToString(TomlSyntax.RFC3339LocalDateTimeFormats[this.SecondsPrecision]),
            var v => v.ToString(TomlSyntax.RFC3339Formats[this.SecondsPrecision])
        };
    }
}

public class TomlArray : TomlNode
{
    List<TomlNode> values;

    public override bool HasValue { get; } = true;
    public override bool IsArray { get; } = true;
    public bool IsTableArray { get; set; }
    public List<TomlNode> RawArray => this.values ??= new List<TomlNode>();

    public override TomlNode this[int index]
    {
        get
        {
            if (index < this.RawArray.Count) return this.RawArray[index];
            var lazy = new TomlLazy(this);
            this[index] = lazy;
            return lazy;
        }
        set
        {
            if (index == this.RawArray.Count)
                this.RawArray.Add(value);
            else
                this.RawArray[index] = value;
        }
    }

    public override int ChildrenCount => this.RawArray.Count;

    public override IEnumerable<TomlNode> Children => this.RawArray.AsEnumerable();

    public override void Add(TomlNode node)
    {
        this.RawArray.Add(node);
    }

    public override void AddRange(IEnumerable<TomlNode> nodes)
    {
        this.RawArray.AddRange(nodes);
    }

    public override void Delete(TomlNode node)
    {
        this.RawArray.Remove(node);
    }

    public override void Delete(int index)
    {
        this.RawArray.RemoveAt(index);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(TomlSyntax.ARRAY_START_SYMBOL);

        if (this.ChildrenCount != 0)
            sb.Append(' ')
                .Append($"{TomlSyntax.ITEM_SEPARATOR} ".Join(this.RawArray.Select(n => n.ToInlineToml())))
                .Append(' ');

        sb.Append(TomlSyntax.ARRAY_END_SYMBOL);
        return sb.ToString();
    }

    public override void WriteTo(TextWriter tw, string name = null)
    {
        // If it's a normal array, write it as usual
        if (!this.IsTableArray)
        {
            tw.Write(this.ToInlineToml());
            return;
        }

        tw.WriteLine();
        this.Comment?.AsComment(tw);
        tw.Write(TomlSyntax.ARRAY_START_SYMBOL);
        tw.Write(TomlSyntax.ARRAY_START_SYMBOL);
        tw.Write(name);
        tw.Write(TomlSyntax.ARRAY_END_SYMBOL);
        tw.Write(TomlSyntax.ARRAY_END_SYMBOL);
        tw.WriteLine();

        var first = true;

        foreach (var tomlNode in this.RawArray)
        {
            if (tomlNode is not TomlTable tbl)
                throw new TomlFormatException("The array is marked as array table but contains non-table nodes!");

            // Ensure it's parsed as a section
            tbl.IsInline = false;

            if (!first)
            {
                tw.WriteLine();

                this.Comment?.AsComment(tw);
                tw.Write(TomlSyntax.ARRAY_START_SYMBOL);
                tw.Write(TomlSyntax.ARRAY_START_SYMBOL);
                tw.Write(name);
                tw.Write(TomlSyntax.ARRAY_END_SYMBOL);
                tw.Write(TomlSyntax.ARRAY_END_SYMBOL);
                tw.WriteLine();
            }

            first = false;

            // Don't pass section name because we already specified it
            tbl.WriteTo(tw);

            tw.WriteLine();
        }
    }
}

public class TomlTable : TomlNode
{
    Dictionary<string, TomlNode> children;

    public override bool HasValue { get; } = false;
    public override bool IsTable { get; } = true;
    public bool IsInline { get; set; }
    public Dictionary<string, TomlNode> RawTable => this.children ??= new Dictionary<string, TomlNode>();

    public override TomlNode this[string key]
    {
        get
        {
            if (this.RawTable.TryGetValue(key, out var result)) return result;
            var lazy = new TomlLazy(this);
            this.RawTable[key] = lazy;
            return lazy;
        }
        set => this.RawTable[key] = value;
    }

    public override int ChildrenCount => this.RawTable.Count;
    public override IEnumerable<TomlNode> Children => this.RawTable.Select(kv => kv.Value);
    public override IEnumerable<string> Keys => this.RawTable.Select(kv => kv.Key);

    public override bool HasKey(string key)
    {
        return this.RawTable.ContainsKey(key);
    }

    public override void Add(string key, TomlNode node)
    {
        this.RawTable.Add(key, node);
    }

    public override bool TryGetNode(string key, out TomlNode node)
    {
        return this.RawTable.TryGetValue(key, out node);
    }

    public override void Delete(TomlNode node)
    {
        this.RawTable.Remove(this.RawTable.First(kv => kv.Value == node).Key);
    }

    public override void Delete(string key)
    {
        this.RawTable.Remove(key);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(TomlSyntax.INLINE_TABLE_START_SYMBOL);

        if (this.ChildrenCount != 0)
        {
            var collapsed = this.CollectCollapsedItems(out var nonCollapsible);

            sb.Append(' ');
            sb.Append($"{TomlSyntax.ITEM_SEPARATOR} ".Join(this.RawTable.Where(n => nonCollapsible.Contains(n.Key))
                .Select(n =>
                    $"{n.Key.AsKey()} {TomlSyntax.KEY_VALUE_SEPARATOR} {n.Value.ToInlineToml()}")));

            if (collapsed.Count != 0)
                sb.Append(TomlSyntax.ITEM_SEPARATOR)
                    .Append(' ')
                    .Append($"{TomlSyntax.ITEM_SEPARATOR} ".Join(collapsed.Select(n =>
                        $"{n.Key} {TomlSyntax.KEY_VALUE_SEPARATOR} {n.Value.ToInlineToml()}")));
            sb.Append(' ');
        }

        sb.Append(TomlSyntax.INLINE_TABLE_END_SYMBOL);
        return sb.ToString();
    }

    Dictionary<string, TomlNode> CollectCollapsedItems(out HashSet<string> nonCollapsibleItems,
        string prefix = "",
        Dictionary<string, TomlNode> nodes = null,
        int level = 0)
    {
        nonCollapsibleItems = new HashSet<string>();
        if (nodes == null)
        {
            nodes = new Dictionary<string, TomlNode>();
            foreach (var keyValuePair in this.RawTable)
            {
                var node = keyValuePair.Value;
                var key = keyValuePair.Key.AsKey();
                if (node is TomlTable tbl)
                {
                    tbl.CollectCollapsedItems(out var nonCollapsible, $"{prefix}{key}.", nodes, level + 1);
                    if (nonCollapsible.Count != 0)
                        nonCollapsibleItems.Add(key);
                }
                else
                {
                    nonCollapsibleItems.Add(key);
                }
            }

            return nodes;
        }

        foreach (var keyValuePair in this.RawTable)
        {
            var node = keyValuePair.Value;
            var key = keyValuePair.Key.AsKey();

            if (node.CollapseLevel == level)
            {
                nodes.Add($"{prefix}{key}", node);
            }
            else if (node is TomlTable tbl)
            {
                tbl.CollectCollapsedItems(out var nonCollapsible, $"{prefix}{key}.", nodes, level + 1);
                if (nonCollapsible.Count != 0)
                    nonCollapsibleItems.Add(key);
            }
            else
            {
                nonCollapsibleItems.Add(key);
            }
        }

        return nodes;
    }

    public override void WriteTo(TextWriter tw, string name = null)
    {
        // The table is inline table
        if (this.IsInline && name != null)
        {
            tw.Write(this.ToInlineToml());
            return;
        }

        if (this.RawTable.All(n => n.Value.CollapseLevel != 0))
            return;

        var hasRealValues = !this.RawTable.All(n => n.Value is TomlTable { IsInline: false });

        var collapsedItems = this.CollectCollapsedItems(out _);

        this.Comment?.AsComment(tw);

        if (name != null && (hasRealValues || collapsedItems.Count > 0))
        {
            tw.Write(TomlSyntax.ARRAY_START_SYMBOL);
            tw.Write(name);
            tw.Write(TomlSyntax.ARRAY_END_SYMBOL);
            tw.WriteLine();
        }
        else if (this.Comment != null) // Add some spacing between the first node and the comment
        {
            tw.WriteLine();
        }

        var namePrefix = name == null ? "" : $"{name}.";
        var first = true;

        var sectionableItems = new Dictionary<string, TomlNode>();

        foreach (var child in this.RawTable)
        {
            // If value should be parsed as section, separate if from the bunch
            if (child.Value is TomlArray { IsTableArray: true } || child.Value is TomlTable { IsInline: false })
            {
                sectionableItems.Add(child.Key, child.Value);
                continue;
            }

            // If the value is collapsed, it belongs to the parent
            if (child.Value.CollapseLevel != 0)
                continue;

            if (!first) tw.WriteLine();
            first = false;

            var key = child.Key.AsKey();
            child.Value.Comment?.AsComment(tw);
            tw.Write(key);
            tw.Write(' ');
            tw.Write(TomlSyntax.KEY_VALUE_SEPARATOR);
            tw.Write(' ');

            child.Value.WriteTo(tw, $"{namePrefix}{key}");
        }

        foreach (var collapsedItem in collapsedItems)
        {
            if (collapsedItem.Value is TomlArray { IsTableArray: true } ||
                collapsedItem.Value is TomlTable { IsInline: false })
                throw new
                    TomlFormatException(
                        $"Value {collapsedItem.Key} cannot be defined as collapsed, because it is not an inline value!");

            tw.WriteLine();
            var key = collapsedItem.Key;
            collapsedItem.Value.Comment?.AsComment(tw);
            tw.Write(key);
            tw.Write(' ');
            tw.Write(TomlSyntax.KEY_VALUE_SEPARATOR);
            tw.Write(' ');

            collapsedItem.Value.WriteTo(tw, $"{namePrefix}{key}");
        }

        if (sectionableItems.Count == 0)
            return;

        tw.WriteLine();
        tw.WriteLine();
        first = true;
        foreach (var child in sectionableItems)
        {
            if (!first) tw.WriteLine();
            first = false;

            child.Value.WriteTo(tw, $"{namePrefix}{child.Key}");
        }
    }
}

class TomlLazy : TomlNode
{
    readonly TomlNode parent;
    TomlNode replacement;

    public TomlLazy(TomlNode parent)
    {
        this.parent = parent;
    }

    public override TomlNode this[int index]
    {
        get => this.Set<TomlArray>()[index];
        set => this.Set<TomlArray>()[index] = value;
    }

    public override TomlNode this[string key]
    {
        get => this.Set<TomlTable>()[key];
        set => this.Set<TomlTable>()[key] = value;
    }

    public override void Add(TomlNode node)
    {
        this.Set<TomlArray>().Add(node);
    }

    public override void Add(string key, TomlNode node)
    {
        this.Set<TomlTable>().Add(key, node);
    }

    public override void AddRange(IEnumerable<TomlNode> nodes)
    {
        this.Set<TomlArray>().AddRange(nodes);
    }

    TomlNode Set<T>() where T : TomlNode, new()
    {
        if (this.replacement != null) return this.replacement;

        var newNode = new T
        {
            Comment = this.Comment
        };

        if (this.parent.IsTable)
        {
            var key = this.parent.Keys.FirstOrDefault(s =>
                this.parent.TryGetNode(s, out var node) && node.Equals(this));
            if (key == null) return default(T);

            this.parent[key] = newNode;
        }
        else if (this.parent.IsArray)
        {
            var index = this.parent.Children.TakeWhile(child => child != this).Count();
            if (index == this.parent.ChildrenCount) return default(T);
            this.parent[index] = newNode;
        }
        else
        {
            return default(T);
        }

        this.replacement = newNode;
        return newNode;
    }
}

#endregion

#region Parser

public class TOMLParser : IDisposable
{
    public enum ParseState
    {
        None,
        KeyValuePair,
        SkipToNextLine,
        Table
    }

    readonly TextReader reader;
    ParseState currentState;
    int line, col;
    List<TomlSyntaxException> syntaxErrors;

    public TOMLParser(TextReader reader)
    {
        this.reader = reader;
        this.line = this.col = 0;
    }

    public bool ForceASCII { get; set; }

    public void Dispose()
    {
        this.reader?.Dispose();
    }

    public TomlTable Parse()
    {
        this.syntaxErrors = new List<TomlSyntaxException>();
        this.line = this.col = 0;
        var rootNode = new TomlTable();
        var currentNode = rootNode;
        this.currentState = ParseState.None;
        var keyParts = new List<string>();
        var arrayTable = false;
        var latestComment = new StringBuilder();
        var firstComment = true;

        int currentChar;
        while ((currentChar = this.reader.Peek()) >= 0)
        {
            var c = (char)currentChar;

            if (this.currentState == ParseState.None)
            {
                // Skip white space
                if (TomlSyntax.IsWhiteSpace(c)) goto consume_character;

                if (TomlSyntax.IsNewLine(c))
                {
                    // Check if there are any comments and so far no items being declared
                    if (latestComment.Length != 0 && firstComment)
                    {
                        rootNode.Comment = latestComment.ToString().TrimEnd();
                        latestComment.Length = 0;
                        firstComment = false;
                    }

                    if (TomlSyntax.IsLineBreak(c)) this.AdvanceLine();

                    goto consume_character;
                }

                // Start of a comment; ignore until newline
                if (c == TomlSyntax.COMMENT_SYMBOL)
                {
                    // Consume the comment symbol and buffer the whole comment line
                    this.reader.Read();
                    latestComment.AppendLine(this.reader.ReadLine()?.Trim());
                    this.AdvanceLine(0);
                    continue;
                }

                // Encountered a non-comment value. The comment must belong to it (ignore possible newlines)!
                firstComment = false;

                if (c == TomlSyntax.TABLE_START_SYMBOL)
                {
                    this.currentState = ParseState.Table;
                    goto consume_character;
                }

                if (TomlSyntax.IsBareKey(c) || TomlSyntax.IsQuoted(c))
                {
                    this.currentState = ParseState.KeyValuePair;
                }
                else
                {
                    this.AddError($"Unexpected character \"{c}\"");
                    continue;
                }
            }

            if (this.currentState == ParseState.KeyValuePair)
            {
                var keyValuePair = this.ReadKeyValuePair(keyParts);

                if (keyValuePair == null)
                {
                    latestComment.Length = 0;
                    keyParts.Clear();

                    if (this.currentState != ParseState.None) this.AddError("Failed to parse key-value pair!");
                    continue;
                }

                keyValuePair.Comment = latestComment.ToString().TrimEnd();
                var inserted = this.InsertNode(keyValuePair, currentNode, keyParts);
                latestComment.Length = 0;
                keyParts.Clear();
                if (inserted) this.currentState = ParseState.SkipToNextLine;
                continue;
            }

            if (this.currentState == ParseState.Table)
            {
                if (keyParts.Count == 0)
                {
                    // We have array table
                    if (c == TomlSyntax.TABLE_START_SYMBOL)
                    {
                        // Consume the character
                        this.ConsumeChar();
                        arrayTable = true;
                    }

                    if (!this.ReadKeyName(ref keyParts, TomlSyntax.TABLE_END_SYMBOL, true))
                    {
                        keyParts.Clear();
                        continue;
                    }

                    if (keyParts.Count == 0)
                    {
                        this.AddError("Table name is emtpy.");
                        arrayTable = false;
                        latestComment.Length = 0;
                        keyParts.Clear();
                    }

                    continue;
                }

                if (c == TomlSyntax.TABLE_END_SYMBOL)
                {
                    if (arrayTable)
                    {
                        // Consume the ending bracket so we can peek the next character
                        this.ConsumeChar();
                        var nextChar = this.reader.Peek();
                        if (nextChar < 0 || (char)nextChar != TomlSyntax.TABLE_END_SYMBOL)
                        {
                            this.AddError($"Array table {".".Join(keyParts)} has only one closing bracket.");
                            keyParts.Clear();
                            arrayTable = false;
                            latestComment.Length = 0;
                            continue;
                        }
                    }

                    currentNode = this.CreateTable(rootNode, keyParts, arrayTable);
                    if (currentNode != null)
                    {
                        currentNode.IsInline = false;
                        currentNode.Comment = latestComment.ToString().TrimEnd();
                    }

                    keyParts.Clear();
                    arrayTable = false;
                    latestComment.Length = 0;

                    if (currentNode == null)
                    {
                        if (this.currentState != ParseState.None) this.AddError("Error creating table array!");
                        continue;
                    }

                    this.currentState = ParseState.SkipToNextLine;
                    goto consume_character;
                }

                if (keyParts.Count != 0)
                {
                    this.AddError($"Unexpected character \"{c}\"");
                    keyParts.Clear();
                    arrayTable = false;
                    latestComment.Length = 0;
                }
            }

            if (this.currentState == ParseState.SkipToNextLine)
            {
                if (TomlSyntax.IsWhiteSpace(c) || c == TomlSyntax.NEWLINE_CARRIAGE_RETURN_CHARACTER)
                    goto consume_character;

                if (c == TomlSyntax.COMMENT_SYMBOL || c == TomlSyntax.NEWLINE_CHARACTER)
                {
                    this.currentState = ParseState.None;
                    this.AdvanceLine();

                    if (c == TomlSyntax.COMMENT_SYMBOL)
                    {
                        this.col++;
                        this.reader.ReadLine();
                        continue;
                    }

                    goto consume_character;
                }

                this.AddError($"Unexpected character \"{c}\" at the end of the line.");
            }

            consume_character:
            this.reader.Read();
            this.col++;
        }

        if (this.currentState != ParseState.None && this.currentState != ParseState.SkipToNextLine)
            this.AddError("Unexpected end of file!");

        if (this.syntaxErrors.Count > 0)
            throw new TomlParseException(rootNode, this.syntaxErrors);

        return rootNode;
    }

    bool AddError(string message)
    {
        this.syntaxErrors.Add(new TomlSyntaxException(message, this.currentState, this.line, this.col));
        // Skip the whole line in hope that it was only a single faulty value (and non-multiline one at that)
        this.reader.ReadLine();
        this.AdvanceLine(0);
        this.currentState = ParseState.None;
        return false;
    }

    void AdvanceLine(int startCol = -1)
    {
        this.line++;
        this.col = startCol;
    }

    int ConsumeChar()
    {
        this.col++;
        return this.reader.Read();
    }

    #region Key-Value pair parsing

    /**
         * Reads a single key-value pair.
         * Assumes the cursor is at the first character that belong to the pair (including possible whitespace).
         * Consumes all characters that belong to the key and the value (ignoring possible trailing whitespace at the end).
         *
         * Example:
         * foo = "bar"  ==> foo = "bar"
         * ^                           ^
         */
    TomlNode ReadKeyValuePair(List<string> keyParts)
    {
        int cur;
        while ((cur = this.reader.Peek()) >= 0)
        {
            var c = (char)cur;

            if (TomlSyntax.IsQuoted(c) || TomlSyntax.IsBareKey(c))
            {
                if (keyParts.Count != 0)
                {
                    this.AddError("Encountered extra characters in key definition!");
                    return null;
                }

                if (!this.ReadKeyName(ref keyParts, TomlSyntax.KEY_VALUE_SEPARATOR))
                    return null;

                continue;
            }

            if (TomlSyntax.IsWhiteSpace(c))
            {
                this.ConsumeChar();
                continue;
            }

            if (c == TomlSyntax.KEY_VALUE_SEPARATOR)
            {
                this.ConsumeChar();
                return this.ReadValue();
            }

            this.AddError($"Unexpected character \"{c}\" in key name.");
            return null;
        }

        return null;
    }

    /**
         * Reads a single value.
         * Assumes the cursor is at the first character that belongs to the value (including possible starting whitespace).
         * Consumes all characters belonging to the value (ignoring possible trailing whitespace at the end).
         *
         * Example:
         * "test"  ==> "test"
         * ^                 ^
         */
    TomlNode ReadValue(bool skipNewlines = false)
    {
        int cur;
        while ((cur = this.reader.Peek()) >= 0)
        {
            var c = (char)cur;

            if (TomlSyntax.IsWhiteSpace(c))
            {
                this.ConsumeChar();
                continue;
            }

            if (c == TomlSyntax.COMMENT_SYMBOL)
            {
                this.AddError("No value found!");
                return null;
            }

            if (TomlSyntax.IsNewLine(c))
            {
                if (skipNewlines)
                {
                    this.reader.Read();
                    this.AdvanceLine(0);
                    continue;
                }

                this.AddError("Encountered a newline when expecting a value!");
                return null;
            }

            if (TomlSyntax.IsQuoted(c))
            {
                var isMultiline = this.IsTripleQuote(c, out var excess);

                // Error occurred in triple quote parsing
                if (this.currentState == ParseState.None)
                    return null;

                var value = isMultiline
                    ? this.ReadQuotedValueMultiLine(c)
                    : this.ReadQuotedValueSingleLine(c, excess);

                return new TomlString
                {
                    Value = value,
                    IsMultiline = isMultiline,
                    PreferLiteral = c == TomlSyntax.LITERAL_STRING_SYMBOL
                };
            }

            return c switch
            {
                TomlSyntax.INLINE_TABLE_START_SYMBOL => this.ReadInlineTable(),
                TomlSyntax.ARRAY_START_SYMBOL => this.ReadArray(),
                _ => this.ReadTomlValue()
            };
        }

        return null;
    }

    /**
         * Reads a single key name.
         * Assumes the cursor is at the first character belonging to the key (with possible trailing whitespace if `skipWhitespace = true`).
         * Consumes all the characters until the `until` character is met (but does not consume the character itself).
         *
         * Example 1:
         * foo.bar  ==>  foo.bar           (`skipWhitespace = false`, `until = ' '`)
         * ^                    ^
         *
         * Example 2:
         * [ foo . bar ] ==>  [ foo . bar ]     (`skipWhitespace = true`, `until = ']'`)
         * ^                             ^
         */
    bool ReadKeyName(ref List<string> parts, char until, bool skipWhitespace = false)
    {
        var buffer = new StringBuilder();
        var quoted = false;
        var prevWasSpace = false;
        int cur;
        while ((cur = this.reader.Peek()) >= 0)
        {
            var c = (char)cur;

            // Reached the final character
            if (c == until) break;

            if (TomlSyntax.IsWhiteSpace(c))
                if (skipWhitespace)
                {
                    prevWasSpace = true;
                    goto consume_character;
                }
                else
                {
                    break;
                }

            if (buffer.Length == 0) prevWasSpace = false;

            if (c == TomlSyntax.SUBKEY_SEPARATOR)
            {
                if (buffer.Length == 0)
                    return this.AddError($"Found an extra subkey separator in {".".Join(parts)}...");

                parts.Add(buffer.ToString());
                buffer.Length = 0;
                quoted = false;
                prevWasSpace = false;
                goto consume_character;
            }

            if (prevWasSpace)
                return this.AddError("Invalid spacing in key name");

            if (TomlSyntax.IsQuoted(c))
            {
                if (quoted)

                    return this.AddError("Expected a subkey separator but got extra data instead!");

                if (buffer.Length != 0)
                    return this.AddError("Encountered a quote in the middle of subkey name!");

                // Consume the quote character and read the key name
                this.col++;
                buffer.Append(this.ReadQuotedValueSingleLine((char)this.reader.Read()));
                quoted = true;
                continue;
            }

            if (TomlSyntax.IsBareKey(c))
            {
                buffer.Append(c);
                goto consume_character;
            }

            // If we see an invalid symbol, let the next parser handle it
            break;

            consume_character:
            this.reader.Read();
            this.col++;
        }

        if (buffer.Length == 0)
            return this.AddError($"Found an extra subkey separator in {".".Join(parts)}...");

        parts.Add(buffer.ToString());

        return true;
    }

    #endregion

    #region Non-string value parsing

    /**
         * Reads the whole raw value until the first non-value character is encountered.
         * Assumes the cursor start position at the first value character and consumes all characters that may be related to the value.
         * Example:
         *
         * 1_0_0_0  ==>  1_0_0_0
         * ^                    ^
         */
    string ReadRawValue()
    {
        var result = new StringBuilder();
        int cur;
        while ((cur = this.reader.Peek()) >= 0)
        {
            var c = (char)cur;
            if (c == TomlSyntax.COMMENT_SYMBOL || TomlSyntax.IsNewLine(c) || TomlSyntax.IsValueSeparator(c)) break;
            result.Append(c);
            this.ConsumeChar();
        }

        // Replace trim with manual space counting?
        return result.ToString().Trim();
    }

    /**
         * Reads and parses a non-string, non-composite TOML value.
         * Assumes the cursor at the first character that is related to the value (with possible spaces).
         * Consumes all the characters that are related to the value.
         *
         * Example
         * 1_0_0_0 # This is a comment
         * <newline>
         *     ==>  1_0_0_0 # This is a comment
         *     ^                                                  ^
         */
    TomlNode ReadTomlValue()
    {
        var value = this.ReadRawValue();
        TomlNode node = value switch
        {
            var v when TomlSyntax.IsBoolean(v) => bool.Parse(v),
            var v when TomlSyntax.IsNaN(v) => double.NaN,
            var v when TomlSyntax.IsPosInf(v) => double.PositiveInfinity,
            var v when TomlSyntax.IsNegInf(v) => double.NegativeInfinity,
            var v when TomlSyntax.IsInteger(v) => long.Parse(value.RemoveAll(TomlSyntax.INT_NUMBER_SEPARATOR),
                CultureInfo.InvariantCulture),
            var v when TomlSyntax.IsFloat(v) => double.Parse(value.RemoveAll(TomlSyntax.INT_NUMBER_SEPARATOR),
                CultureInfo.InvariantCulture),
            var v when TomlSyntax.IsIntegerWithBase(v, out var numberBase) => new TomlInteger
            {
                Value = Convert.ToInt64(value[2..].RemoveAll(TomlSyntax.INT_NUMBER_SEPARATOR), numberBase),
                IntegerBase = (TomlInteger.Base)numberBase
            },
            _ => null
        };
        if (node != null) return node;

        value = value.Replace("T", " ");
        if (StringUtils.TryParseDateTime(value,
                TomlSyntax.RFC3339LocalDateTimeFormats,
                DateTimeStyles.AssumeLocal,
                out var dateTimeResult,
                out var precision))
            return new TomlDateTime
            {
                Value = dateTimeResult,
                SecondsPrecision = precision
            };

        if (StringUtils.TryParseDateTime(value,
                TomlSyntax.RFC3339Formats,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out dateTimeResult,
                out precision))
            return new TomlDateTime
            {
                Value = dateTimeResult,
                SecondsPrecision = precision
            };


        if (DateTime.TryParseExact(value,
                TomlSyntax.LocalDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out dateTimeResult))
            return new TomlDateTime
            {
                Value = dateTimeResult,
                OnlyDate = true
            };

        if (StringUtils.TryParseDateTime(value,
                TomlSyntax.RFC3339LocalTimeFormats,
                DateTimeStyles.AssumeLocal,
                out dateTimeResult,
                out precision))
            return new TomlDateTime
            {
                Value = dateTimeResult,
                OnlyTime = true,
                SecondsPrecision = precision
            };

        this.AddError($"Value \"{value}\" is not a valid TOML 0.5.0 value!");
        return null;
    }

    /**
         * Reads an array value.
         * Assumes the cursor is at the start of the array definition. Reads all character until the array closing bracket.
         *
         * Example:
         * [1, 2, 3]  ==>  [1, 2, 3]
         * ^                        ^
         */
    TomlArray ReadArray()
    {
        // Consume the start of array character
        this.ConsumeChar();
        var result = new TomlArray();
        TomlNode currentValue = null;

        int cur;
        while ((cur = this.reader.Peek()) >= 0)
        {
            var c = (char)cur;

            if (c == TomlSyntax.ARRAY_END_SYMBOL)
            {
                this.ConsumeChar();
                break;
            }

            if (c == TomlSyntax.COMMENT_SYMBOL)
            {
                this.reader.ReadLine();
                this.AdvanceLine(0);
                continue;
            }

            if (TomlSyntax.IsWhiteSpace(c) || TomlSyntax.IsNewLine(c))
            {
                if (TomlSyntax.IsLineBreak(c)) this.AdvanceLine();
                goto consume_character;
            }

            if (c == TomlSyntax.ITEM_SEPARATOR)
            {
                if (currentValue == null)
                {
                    this.AddError("Encountered multiple value separators in an array!");
                    return null;
                }

                result.Add(currentValue);
                currentValue = null;
                goto consume_character;
            }

            currentValue = this.ReadValue(true);
            if (currentValue == null)
            {
                if (this.currentState != ParseState.None) this.AddError("Failed to determine and parse a value!");
                return null;
            }

            if (result.ChildrenCount != 0 && result[0].GetType() != currentValue.GetType())
            {
                this.AddError(
                    $"Arrays cannot have mixed types! Inferred type: {result[0].GetType().FullName}. Element type: {currentValue.GetType().FullName}");
                return null;
            }

            continue;
            consume_character:
            this.ConsumeChar();
        }

        if (currentValue != null) result.Add(currentValue);
        return result;
    }

    /**
         * Reads an inline table.
         * Assumes the cursor is at the start of the table definition. Reads all character until the table closing bracket.
         *
         * Example:
         * { test = "foo", value = 1 }  ==>  { test = "foo", value = 1 }
         * ^                                                            ^
         */
    TomlNode ReadInlineTable()
    {
        this.ConsumeChar();
        var result = new TomlTable { IsInline = true };
        TomlNode currentValue = null;
        var keyParts = new List<string>();
        int cur;
        while ((cur = this.reader.Peek()) >= 0)
        {
            var c = (char)cur;

            if (c == TomlSyntax.INLINE_TABLE_END_SYMBOL)
            {
                this.ConsumeChar();
                break;
            }

            if (c == TomlSyntax.COMMENT_SYMBOL)
            {
                this.AddError("Incomplete inline table definition!");
                return null;
            }

            if (TomlSyntax.IsNewLine(c))
            {
                this.AddError("Inline tables are only allowed to be on single line");
                return null;
            }

            if (TomlSyntax.IsWhiteSpace(c))
                goto consume_character;

            if (c == TomlSyntax.ITEM_SEPARATOR)
            {
                if (currentValue == null)
                {
                    this.AddError("Encountered multiple value separators in inline table!");
                    return null;
                }

                if (!this.InsertNode(currentValue, result, keyParts))
                    return null;
                keyParts.Clear();
                currentValue = null;
                goto consume_character;
            }

            currentValue = this.ReadKeyValuePair(keyParts);
            continue;

            consume_character:
            this.ConsumeChar();
        }

        if (currentValue != null && !this.InsertNode(currentValue, result, keyParts))
            return null;

        return result;
    }

    #endregion

    #region String parsing

    /**
         * Checks if the string value a multiline string (i.e. a triple quoted string).
         * Assumes the cursor is at the first quote character. Consumes the least amount of characters needed to determine if the string is multiline.
         *
         * If the result is false, returns the consumed character through the `excess` variable.
         *
         * Example 1:
         * """test"""  ==>  """test"""
         * ^                   ^
         *
         * Example 2:
         * "test"  ==>  "test"         (doesn't return the first quote)
         * ^             ^
         *
         * Example 3:
         * ""  ==>  ""        (returns the extra `"` through the `excess` variable)
         * ^          ^
         */
    bool IsTripleQuote(char quote, out char excess)
    {
        // Copypasta, but it's faster...

        int cur;
        // Consume the first quote
        this.ConsumeChar();
        if ((cur = this.reader.Peek()) < 0)
        {
            excess = '\0';
            return this.AddError("Unexpected end of file!");
        }

        if ((char)cur != quote)
        {
            excess = '\0';
            return false;
        }

        // Consume the second quote
        excess = (char)this.ConsumeChar();
        if ((cur = this.reader.Peek()) < 0 || (char)cur != quote) return false;

        // Consume the final quote
        this.ConsumeChar();
        excess = '\0';
        return true;
    }

    /**
         * A convenience method to process a single character within a quote.
         */
    bool ProcessQuotedValueCharacter(char quote,
        bool isNonLiteral,
        char c,
        StringBuilder sb,
        ref bool escaped)
    {
        if (TomlSyntax.ShouldBeEscaped(c))
            return this.AddError($"The character U+{c:X8} must be escaped in a string!");

        if (escaped)
        {
            sb.Append(c);
            escaped = false;
            return false;
        }

        if (c == quote) return true;
        if (isNonLiteral && c == TomlSyntax.ESCAPE_SYMBOL)
            escaped = true;
        if (c == TomlSyntax.NEWLINE_CHARACTER)
            return this.AddError("Encountered newline in single line string!");

        sb.Append(c);
        return false;
    }

    /**
         * Reads a single-line string.
         * Assumes the cursor is at the first character that belongs to the string.
         * Consumes all characters that belong to the string (including the closing quote).
         *
         * Example:
         * "test"  ==>  "test"
         * ^                 ^
         */
    string ReadQuotedValueSingleLine(char quote, char initialData = '\0')
    {
        var isNonLiteral = quote == TomlSyntax.BASIC_STRING_SYMBOL;
        var sb = new StringBuilder();
        var escaped = false;

        if (initialData != '\0')
        {
            var shouldReturn = this.ProcessQuotedValueCharacter(quote, isNonLiteral, initialData, sb, ref escaped);
            if (this.currentState == ParseState.None) return null;
            if (shouldReturn) return isNonLiteral ? sb.ToString().Unescape() : sb.ToString();
        }

        int cur;
        while ((cur = this.reader.Read()) >= 0)
        {
            // Consume the character
            this.col++;
            var c = (char)cur;
            if (this.ProcessQuotedValueCharacter(quote, isNonLiteral, c, sb, ref escaped))
            {
                if (this.currentState == ParseState.None) return null;
                break;
            }
        }

        return isNonLiteral ? sb.ToString().Unescape() : sb.ToString();
    }

    /**
         * Reads a multiline string.
         * Assumes the cursor is at the first character that belongs to the string.
         * Consumes all characters that belong to the string and the three closing quotes.
         *
         * Example:
         * """test"""  ==>  """test"""
         * ^                       ^
         */
    string ReadQuotedValueMultiLine(char quote)
    {
        var isBasic = quote == TomlSyntax.BASIC_STRING_SYMBOL;
        var sb = new StringBuilder();
        var escaped = false;
        var skipWhitespace = false;
        var quotesEncountered = 0;
        var first = true;
        int cur;
        while ((cur = this.ConsumeChar()) >= 0)
        {
            var c = (char)cur;
            if (TomlSyntax.ShouldBeEscaped(c))
                throw new Exception($"The character U+{c:X8} must be escaped!");
            // Trim the first newline
            if (first && TomlSyntax.IsNewLine(c))
            {
                if (TomlSyntax.IsLineBreak(c))
                    first = false;
                else
                    this.AdvanceLine();
                continue;
            }

            first = false;
            //TODO: Reuse ProcessQuotedValueCharacter
            // Skip the current character if it is going to be escaped later
            if (escaped)
            {
                sb.Append(c);
                escaped = false;
                continue;
            }

            // If we are currently skipping empty spaces, skip
            if (skipWhitespace)
            {
                if (TomlSyntax.IsEmptySpace(c))
                {
                    if (TomlSyntax.IsLineBreak(c)) this.AdvanceLine();
                    continue;
                }

                skipWhitespace = false;
            }

            // If we encounter an escape sequence...
            if (isBasic && c == TomlSyntax.ESCAPE_SYMBOL)
            {
                var next = this.reader.Peek();
                if (next >= 0)
                {
                    // ...and the next char is empty space, we must skip all whitespaces
                    if (TomlSyntax.IsEmptySpace((char)next))
                    {
                        skipWhitespace = true;
                        continue;
                    }

                    // ...and we have \", skip the character
                    if ((char)next == quote) escaped = true;
                }
            }

            // Count the consecutive quotes
            if (c == quote)
                quotesEncountered++;
            else
                quotesEncountered = 0;

            // If the are three quotes, count them as closing quotes
            if (quotesEncountered == 3) break;

            sb.Append(c);
        }

        // Remove last two quotes (third one wasn't included by default
        sb.Length -= 2;
        return isBasic ? sb.ToString().Unescape() : sb.ToString();
    }

    #endregion

    #region Node creation

    bool InsertNode(TomlNode node, TomlNode root, IList<string> path)
    {
        var latestNode = root;
        if (path.Count > 1)
            for (var index = 0; index < path.Count - 1; index++)
            {
                var subkey = path[index];
                if (latestNode.TryGetNode(subkey, out var currentNode))
                {
                    if (currentNode.HasValue)
                        return this.AddError($"The key {".".Join(path)} already has a value assigned to it!");
                }
                else
                {
                    currentNode = new TomlTable();
                    latestNode[subkey] = currentNode;
                }

                latestNode = currentNode;
            }

        if (latestNode.HasKey(path[^1]))
            return this.AddError($"The key {".".Join(path)} is already defined!");
        latestNode[path[^1]] = node;
        node.CollapseLevel = path.Count - 1;
        return true;
    }

    TomlTable CreateTable(TomlNode root, IList<string> path, bool arrayTable)
    {
        if (path.Count == 0) return null;
        var latestNode = root;
        for (var index = 0; index < path.Count; index++)
        {
            var subkey = path[index];

            if (latestNode.TryGetNode(subkey, out var node))
            {
                if (node.IsArray && arrayTable)
                {
                    var arr = (TomlArray)node;

                    if (!arr.IsTableArray)
                    {
                        this.AddError($"The array {".".Join(path)} cannot be redefined as an array table!");
                        return null;
                    }

                    if (index == path.Count - 1)
                    {
                        latestNode = new TomlTable();
                        arr.Add(latestNode);
                        break;
                    }

                    latestNode = arr[arr.ChildrenCount - 1];
                    continue;
                }

                if (node.HasValue)
                {
                    if (node is not TomlArray { IsTableArray: true } array)
                    {
                        this.AddError($"The key {".".Join(path)} has a value assigned to it!");
                        return null;
                    }

                    latestNode = array[array.ChildrenCount - 1];
                    continue;
                }

                if (index == path.Count - 1)
                {
                    if (arrayTable && !node.IsArray)
                    {
                        this.AddError($"The table {".".Join(path)} cannot be redefined as an array table!");
                        return null;
                    }

                    if (node is TomlTable { IsInline: false })
                    {
                        this.AddError($"The table {".".Join(path)} is defined multiple times!");
                        return null;
                    }
                }
            }
            else
            {
                if (index == path.Count - 1 && arrayTable)
                {
                    var table = new TomlTable();
                    var arr = new TomlArray
                    {
                        IsTableArray = true
                    };
                    arr.Add(table);
                    latestNode[subkey] = arr;
                    latestNode = table;
                    break;
                }

                node = new TomlTable
                {
                    IsInline = true
                };
                latestNode[subkey] = node;
            }

            latestNode = node;
        }

        var result = (TomlTable)latestNode;
        return result;
    }

    #endregion
}

#endregion

public static class TOML
{
    public static bool ForceASCII { get; set; } = false;

    public static TomlTable Parse(TextReader reader)
    {
        using var parser = new TOMLParser(reader) { ForceASCII = ForceASCII };
        return parser.Parse();
    }
}

#region Exception Types

public class TomlFormatException : Exception
{
    public TomlFormatException(string message) : base(message)
    {
    }
}

public class TomlParseException : Exception
{
    public TomlParseException(TomlTable parsed, IEnumerable<TomlSyntaxException> exceptions) :
        base("TOML file contains format errors")
    {
        this.ParsedTable = parsed;
        this.SyntaxErrors = exceptions;
    }

    public TomlTable ParsedTable { get; }

    public IEnumerable<TomlSyntaxException> SyntaxErrors { get; }
}

public class TomlSyntaxException : Exception
{
    public TomlSyntaxException(string message, TOMLParser.ParseState state, int line, int col) : base(message)
    {
        this.ParseState = state;
        this.Line = line;
        this.Column = col;
    }

    public TOMLParser.ParseState ParseState { get; }

    public int Line { get; }

    public int Column { get; }
}

#endregion

#region Parse utilities

static partial class TomlSyntax
{
    #region Type Patterns

    public const string TRUE_VALUE = "true";
    public const string FALSE_VALUE = "false";
    public const string NAN_VALUE = "nan";
    public const string POS_NAN_VALUE = "+nan";
    public const string NEG_NAN_VALUE = "-nan";
    public const string INF_VALUE = "inf";
    public const string POS_INF_VALUE = "+inf";
    public const string NEG_INF_VALUE = "-inf";

    public static bool IsBoolean(string s)
    {
        return s is TRUE_VALUE or FALSE_VALUE;
    }

    public static bool IsPosInf(string s)
    {
        return s is INF_VALUE or POS_INF_VALUE;
    }

    public static bool IsNegInf(string s)
    {
        return s == NEG_INF_VALUE;
    }

    public static bool IsNaN(string s)
    {
        return s is NAN_VALUE or POS_NAN_VALUE or NEG_NAN_VALUE;
    }

    public static bool IsInteger(string s)
    {
        return IntegerPattern().IsMatch(s);
    }

    public static bool IsFloat(string s)
    {
        return FloatPattern().IsMatch(s);
    }

    public static bool IsIntegerWithBase(string s, out int numberBase)
    {
        numberBase = 10;

        var match = BasedIntegerPattern().Match(s);

        if (!match.Success) return false;
        IntegerBases.TryGetValue(match.Groups["base"].Value, out numberBase);
        return true;
    }


    /**
         * A helper dictionary to map TOML base codes into the radii.
         */
    public static readonly Dictionary<string, int> IntegerBases = new()
    {
        ["x"] = 16,
        ["o"] = 8,
        ["b"] = 2
    };

    /**
         * A helper dictionary to map non-decimal bases to their TOML identifiers
         */
    public static readonly Dictionary<int, string> BaseIdentifiers = new()
    {
        [2] = "b",
        [8] = "o",
        [16] = "x"
    };

    /**
         * Valid date formats with timezone as per RFC3339.
         */
    public static readonly string[] RFC3339Formats =
    {
        "yyyy'-'MM-dd HH':'mm':'ssK", "yyyy'-'MM-dd HH':'mm':'ss'.'fK", "yyyy'-'MM-dd HH':'mm':'ss'.'ffK",
        "yyyy'-'MM-dd HH':'mm':'ss'.'fffK", "yyyy'-'MM-dd HH':'mm':'ss'.'ffffK",
        "yyyy'-'MM-dd HH':'mm':'ss'.'fffffK", "yyyy'-'MM-dd HH':'mm':'ss'.'ffffffK",
        "yyyy'-'MM-dd HH':'mm':'ss'.'fffffffK"
    };

    /**
         * Valid date formats without timezone (assumes local) as per RFC3339.
         */
    public static readonly string[] RFC3339LocalDateTimeFormats =
    {
        "yyyy'-'MM-dd HH':'mm':'ss", "yyyy'-'MM-dd HH':'mm':'ss'.'f", "yyyy'-'MM-dd HH':'mm':'ss'.'ff",
        "yyyy'-'MM-dd HH':'mm':'ss'.'fff", "yyyy'-'MM-dd HH':'mm':'ss'.'ffff",
        "yyyy'-'MM-dd HH':'mm':'ss'.'fffff", "yyyy'-'MM-dd HH':'mm':'ss'.'ffffff",
        "yyyy'-'MM-dd HH':'mm':'ss'.'fffffff"
    };

    /**
         * Valid full date format as per TOML spec.
         */
    public const string LocalDateFormat = "yyyy'-'MM'-'dd";

    /**
         * Valid time formats as per TOML spec.
         */
    public static readonly string[] RFC3339LocalTimeFormats =
    {
        "HH':'mm':'ss", "HH':'mm':'ss'.'f", "HH':'mm':'ss'.'ff", "HH':'mm':'ss'.'fff", "HH':'mm':'ss'.'ffff",
        "HH':'mm':'ss'.'fffff", "HH':'mm':'ss'.'ffffff", "HH':'mm':'ss'.'fffffff"
    };

    #endregion

    #region Character definitions

    public const char ARRAY_END_SYMBOL = ']';
    public const char ITEM_SEPARATOR = ',';
    public const char ARRAY_START_SYMBOL = '[';
    public const char BASIC_STRING_SYMBOL = '\"';
    public const char COMMENT_SYMBOL = '#';
    public const char ESCAPE_SYMBOL = '\\';
    public const char KEY_VALUE_SEPARATOR = '=';
    public const char NEWLINE_CARRIAGE_RETURN_CHARACTER = '\r';
    public const char NEWLINE_CHARACTER = '\n';
    public const char SUBKEY_SEPARATOR = '.';
    public const char TABLE_END_SYMBOL = ']';
    public const char TABLE_START_SYMBOL = '[';
    public const char INLINE_TABLE_START_SYMBOL = '{';
    public const char INLINE_TABLE_END_SYMBOL = '}';
    public const char LITERAL_STRING_SYMBOL = '\'';
    public const char INT_NUMBER_SEPARATOR = '_';

    public static readonly char[] NewLineCharacters = { NEWLINE_CHARACTER, NEWLINE_CARRIAGE_RETURN_CHARACTER };

    public static bool IsQuoted(char c)
    {
        return c is BASIC_STRING_SYMBOL or LITERAL_STRING_SYMBOL;
    }

    public static bool IsWhiteSpace(char c)
    {
        return c is ' ' or '\t';
    }

    public static bool IsNewLine(char c)
    {
        return c is NEWLINE_CHARACTER or NEWLINE_CARRIAGE_RETURN_CHARACTER;
    }

    public static bool IsLineBreak(char c)
    {
        return c == NEWLINE_CHARACTER;
    }

    public static bool IsEmptySpace(char c)
    {
        return IsWhiteSpace(c) || IsNewLine(c);
    }

    public static bool IsBareKey(char c)
    {
        return c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-';
    }

    public static bool ShouldBeEscaped(char c)
    {
        return c is <= '\u001f' or '\u007f' && !IsNewLine(c);
    }

    public static bool IsValueSeparator(char c)
    {
        return c is ITEM_SEPARATOR or ARRAY_END_SYMBOL or INLINE_TABLE_END_SYMBOL;
    }

    [GeneratedRegex("^(\\+|-)?(?!_)(0|(?!0)(_?\\d)*)$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(
        "^(\\+|-)?(?!_)(0|(?!0)(_?\\d)+)(((e(\\+|-)?(?!_)(_?\\d)+)?)|(\\.(?!_)(_?\\d)+(e(\\+|-)?(?!_)(_?\\d)+)?))$",
        RegexOptions.IgnoreCase)]
    private static partial Regex FloatPattern();

    [GeneratedRegex("^(\\+|-)?0(?<base>x|b|o)(?!_)(_?[0-9A-F])*$", RegexOptions.IgnoreCase)]
    private static partial Regex BasedIntegerPattern();

    #endregion
}

static class StringUtils
{
    public static string AsKey(this string key)
    {
        var quote = key.Any(c => !TomlSyntax.IsBareKey(c));
        return !quote ? key : $"{TomlSyntax.BASIC_STRING_SYMBOL}{key.Escape()}{TomlSyntax.BASIC_STRING_SYMBOL}";
    }

    public static string Join(this string self, IEnumerable<string> subItems)
    {
        var sb = new StringBuilder();
        var first = true;

        foreach (var subItem in subItems)
        {
            if (!first) sb.Append(self);
            first = false;
            sb.Append(subItem);
        }

        return sb.ToString();
    }

    public static bool TryParseDateTime(string s,
        string[] formats,
        DateTimeStyles styles,
        out DateTime dateTime,
        out int parsedFormat)
    {
        parsedFormat = 0;
        dateTime = new DateTime();

        for (var i = 0; i < formats.Length; i++)
        {
            var format = formats[i];
            if (!DateTime.TryParseExact(s, format, CultureInfo.InvariantCulture, styles, out dateTime)) continue;
            parsedFormat = i;
            return true;
        }

        return false;
    }

    public static void AsComment(this string self, TextWriter tw)
    {
        foreach (var line in self.Split(TomlSyntax.NEWLINE_CHARACTER))
            tw.WriteLine($"{TomlSyntax.COMMENT_SYMBOL} {line.Trim()}");
    }

    public static string RemoveAll(this string txt, char toRemove)
    {
        var sb = new StringBuilder(txt.Length);
        foreach (var c in txt.Where(c => c != toRemove))
            sb.Append(c);
        return sb.ToString();
    }

    public static string Escape(this string txt, bool escapeNewlines = true)
    {
        var stringBuilder = new StringBuilder(txt.Length + 2);
        for (var i = 0; i < txt.Length; i++)
        {
            var c = txt[i];

            static string CodePoint(string txt, ref int i, char c)
            {
                return char.IsSurrogatePair(txt, i)
                    ? $"\\U{char.ConvertToUtf32(txt, i++):X8}"
                    : $"\\u{c:X4}";
            }

            stringBuilder.Append(c switch
            {
                '\b' => @"\b",
                '\t' => @"\t",
                '\n' when escapeNewlines => @"\n",
                '\f' => @"\f",
                '\r' when escapeNewlines => @"\r",
                '\\' => @"\\",
                '\"' => @"\""",
                _ when TomlSyntax.ShouldBeEscaped(c) || (TOML.ForceASCII && c > sbyte.MaxValue) =>
                    CodePoint(txt, ref i, c),
                _ => c
            });
        }

        return stringBuilder.ToString();
    }

    public static string Unescape(this string txt)
    {
        if (string.IsNullOrEmpty(txt)) return txt;
        var stringBuilder = new StringBuilder(txt.Length);
        for (var i = 0; i < txt.Length;)
        {
            var num = txt.IndexOf('\\', i);
            var next = num + 1;
            if (num < 0 || num == txt.Length - 1) num = txt.Length;
            stringBuilder.Append(txt, i, num - i);
            if (num >= txt.Length) break;
            var c = txt[next];

            static string CodePoint(int next, string txt, ref int num, int size)
            {
                if (next + size >= txt.Length) throw new Exception("Undefined escape sequence!");
                num += size;
                return char.ConvertFromUtf32(Convert.ToInt32(txt.Substring(next + 1, size), 16));
            }

            stringBuilder.Append(c switch
            {
                'b' => "\b",
                't' => "\t",
                'n' => "\n",
                'f' => "\f",
                'r' => "\r",
                '\'' => "\'",
                '\"' => "\"",
                '\\' => "\\",
                'u' => CodePoint(next, txt, ref num, 4),
                'U' => CodePoint(next, txt, ref num, 8),
                _ => throw new Exception("Undefined escape sequence!")
            });
            i = num + 2;
        }

        return stringBuilder.ToString();
    }
}

#endregion