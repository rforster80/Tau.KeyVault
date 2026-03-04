namespace Tau.KeyVault.Models;

public class KeyEntry
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty; // blank = global
    public DataType DataType { get; set; } = DataType.Text;
    public bool IsSensitive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
