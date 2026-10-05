namespace SharpJsonRepair.Interfaces;

public interface IInputBuffer
{
    void Push(string chunk);
    void Flush(int position);
    string CharAt(int index);
    int CharCodeAt(int index);
    string Substring(int start, int end);
    int Length();
    int CurrentLength();
    int CurrentBufferSize();
    bool IsEnd(int index);
    void Close();
}