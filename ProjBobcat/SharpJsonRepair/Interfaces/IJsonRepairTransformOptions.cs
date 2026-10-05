namespace SharpJsonRepair.Interfaces;

public interface IJsonRepairTransformOptions
{
    int? ChunkSize { get; set; }
    int? BufferSize { get; set; }
}