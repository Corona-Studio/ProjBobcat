using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Model;

public class GameConfigurationManager : IEnumerable<KeyValuePair<string, string>>
{
    readonly Dictionary<string, string> _configuration;

    public GameConfigurationManager()
    {
        this._configuration = [];
    }

    public GameConfigurationManager(string path)
    {
        var content = File.ReadAllLines(path);
        this._configuration = GetConfigurationDictionary(content);
    }

    public GameConfigurationManager(IReadOnlyCollection<string> list)
    {
        this._configuration = GetConfigurationDictionary(list);
    }

    public string this[string key]
    {
        get => this._configuration[key];
        set => this._configuration[key] = value;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return this._configuration.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this._configuration.GetEnumerator();
    }

    public async Task SaveAsync(string path)
    {
        var sb = new StringBuilder();

        foreach (var (key, value) in this._configuration) sb.AppendLine($"{key}:{value}");

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    public static Dictionary<string, string> GetConfigurationDictionary(IReadOnlyCollection<string>? lines)
    {
        var result = new Dictionary<string, string>();

        if ((lines?.Count ?? 0) == 0) return result;

        foreach (var line in lines!)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.Contains(':')) continue;

            var arr = line.Split(':');

            if (arr.Length != 2) continue;
            if (result.ContainsKey(arr[0]))
            {
                result[arr[0]] = arr[1];
                continue;
            }

            result.Add(arr[0], arr[1]);
        }

        return result;
    }
}