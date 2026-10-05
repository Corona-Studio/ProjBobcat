namespace SharpJsonRepair.Class;

public class RepairDataHolder(string text)
{
    public int Index { get; set; } = 0;
    public string Text { get; set; } = text;
    public string Output { get; set; } = string.Empty;

    public char CurrentChar => Text[Index];
    public char CharAt(int index) => Text[index];
}