using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Bugger.Core;

public sealed class TaskStore(string path)
{
    private static readonly PropertyInfo[] Columns = typeof(TaskItem).GetProperties()
        .Where(p => p.CanRead && p.CanWrite).ToArray();

    public List<TaskItem> Load()
    {
        if (!File.Exists(path)) return [];
        using var reader = new TextFieldParser(path, Encoding.UTF8) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        reader.SetDelimiters(",");
        var header = reader.ReadFields() ?? throw new FormatException("The task file has no header.");
        if (!Columns.Where(p => p.Name != nameof(TaskItem.SnoozedUntil)).All(p => header.Contains(p.Name))) throw new FormatException("The task file is missing required columns. Keep it unchanged and restore tasks.csv.bak if needed.");
        var result = new List<TaskItem>();
        while (!reader.EndOfData)
        {
            var row = reader.ReadFields()!;
            try
            {
                if (row.Length != header.Length) throw new FormatException("Column count does not match the header.");
                var item = new TaskItem();
                foreach (var p in Columns)
                {
                    var index = Array.IndexOf(header, p.Name);
                    if (index < 0) continue; // Additive columns have safe defaults for older task files.
                    var value = row[index];
                    var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    object? parsed = value.Length == 0 && Nullable.GetUnderlyingType(p.PropertyType) != null ? null
                        : type == typeof(DateTime) ? DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                        : type == typeof(TimeSpan) ? TimeSpan.ParseExact(value, @"hh\:mm\:ss", CultureInfo.InvariantCulture)
                        : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                    p.SetValue(item, parsed);
                }
                item.Validate();
                if (string.IsNullOrWhiteSpace(item.Id) || result.Any(t => t.Id == item.Id)) throw new FormatException("Task IDs must be present and unique.");
                if (!new[] { "Waiting", "Pending", "Done", "Expired" }.Contains(item.Status)) throw new FormatException("Unknown task status.");
                if (item.Status == "Pending" && (item.WindowEnd == null || item.OccurrenceDate == null || item.OriginalDueDate == null || item.ActiveTotal < 1 || item.ActiveInterval < 1)) throw new FormatException("Incomplete reminder progress.");
                result.Add(item);
            }
            catch (Exception e) when (e is FormatException or ArgumentException or TargetInvocationException or OverflowException)
            { throw new FormatException($"Cannot read task record {result.Count + 1}: {e.GetBaseException().Message} The original file has not been changed.", e); }
        }
        return result;
    }

    public void Save(IEnumerable<TaskItem> tasks)
    {
        var temp = path + ".tmp";
        static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        static string Format(object? value) => value switch
        {
            null => "", DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            TimeSpan time => time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
        using (var writer = new StreamWriter(temp, false, new UTF8Encoding(true)))
        {
            writer.WriteLine(string.Join(",", Columns.Select(p => p.Name)));
            foreach (var item in tasks) writer.WriteLine(string.Join(",", Columns.Select(p => Csv(Format(p.GetValue(item))))));
            writer.Flush();
            ((FileStream)writer.BaseStream).Flush(true);
        }
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
}
