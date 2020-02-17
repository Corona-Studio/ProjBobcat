namespace ProjBobcat.Class.Model
{
    public class TaskResult<T>
    {
        public TaskResult(TaskResultStatus taskStatus, string message = "", T value = default)
        {
            TaskStatus = taskStatus;
            Message = message;
            Value = value;
        }

        public T Value { get; }

        public string Message { get; set; }

        public TaskResultStatus TaskStatus { get; set; }
    }
}