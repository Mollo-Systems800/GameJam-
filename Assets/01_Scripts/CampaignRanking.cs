using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CampaignRanking
{
    public const string StorageKey = "MazeBeta.CampaignRanking.V1";
    public const int PageSize = 10;
    [Serializable] public sealed class Entry { public string name; public float seconds; }
    [Serializable] sealed class Board { public List<Entry> entries = new List<Entry>(); }
    public static IReadOnlyList<Entry> Read(string key = StorageKey)
    {
        try
        {
            var board = JsonUtility.FromJson<Board>(PlayerPrefs.GetString(key, "{}"));
            return (board?.entries ?? new List<Entry>()).Where(e => e != null && ValidTime(e.seconds) && CleanName(e.name).Length > 0)
                .OrderBy(e => e.seconds).Select(e => new Entry { name = CleanName(e.name), seconds = e.seconds }).ToArray();
        }
        catch (ArgumentException) { return Array.Empty<Entry>(); }
    }
    public static bool IsNewRecord(float seconds, string key = StorageKey)
    {
        var rows = Read(key);
        return ValidTime(seconds) && (rows.Count == 0 || seconds < rows[0].seconds - .001f);
    }
    public static bool TrySave(string name, float seconds, string key = StorageKey)
    {
        name = CleanName(name);
        if (name.Length == 0 || !ValidTime(seconds)) return false;
        var entries = Read(key).ToList(); entries.Add(new Entry { name = name, seconds = seconds });
        var board = new Board { entries = entries.OrderBy(e => e.seconds).ToList() };
        PlayerPrefs.SetString(key, JsonUtility.ToJson(board)); PlayerPrefs.Save(); return true;
    }
    static bool ValidTime(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    public static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return new string(name.Trim().Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(20).ToArray()).Trim();
    }
    public static string Format(float seconds)
    {
        int hundredths = Mathf.FloorToInt(Mathf.Max(0, seconds) * 100);
        return $"{hundredths / 6000:00}:{hundredths / 100 % 60:00}.{hundredths % 100:00}";
    }
}
