namespace SharpJsonRepair.Interfaces;

public interface IOutputBuffer
{
    void Push(string text);
    void Unshift(string text);
    void Remove(int start, int? end = null);
    void InsertAt(int index, string text);
    int Length();
    void Flush();
    void StripLastOccurrence(string textToStrip, bool stripRemainingText = false);
    void InsertBeforeLastWhitespace(string textToInsert);
    bool EndsWithIgnoringWhitespace(string character);
}