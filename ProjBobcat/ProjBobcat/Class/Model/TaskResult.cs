namespace ProjBobcat.Class.Model;

public class TaskResult<T>(TaskResultStatus taskStatus, string message = "", T? value = default)
{
    public T? Value { get; } = value;

    public string Message { get; set; } = message;

    public TaskResultStatus TaskStatus { get; set; } = taskStatus;
}