using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// The single level document used by runtime gameplay and the Level Tool.
/// The source format is the format already used by Resources/Levels:
/// { "level": { "id": ..., "boardData": { "items": [] } } }.
/// The legacy runtime lists are a non-serialized projection for gradual migration.
/// </summary>
[Serializable]
public sealed class LevelData
{
    [JsonProperty("level")]
    public LevelDocument level = new LevelDocument();

    [JsonIgnore] public int levelId = 1;
    [JsonIgnore] public int i = 1;
    [JsonIgnore] public int j = 1;
    [JsonIgnore] public List<BusData> buses = new List<BusData>();
    [JsonIgnore] public List<WaitingPassengerColumnData> waitingPassengerColumns = new List<WaitingPassengerColumnData>();
    [JsonIgnore] public List<LevelBoardItem> preservedBoardItems = new List<LevelBoardItem>();

    public static LevelData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        JObject root = JObject.Parse(json);
        LevelData data;
        if (root["level"] != null)
        {
            data = root.ToObject<LevelData>(CreateDeserializer());
        }
        else
        {
            // Compatibility with files written by the previous ToolLevel.
            LegacyLevelData legacy = root.ToObject<LegacyLevelData>(CreateDeserializer());
            data = new LevelData
            {
                levelId = Mathf.Max(1, legacy.levelId),
                i = Mathf.Max(1, legacy.i),
                j = Mathf.Max(1, legacy.j),
                buses = legacy.buses ?? new List<BusData>(),
                waitingPassengerColumns = legacy.waitingPassengerColumns ?? new List<WaitingPassengerColumnData>()
            };
            data.SyncDocumentFromRuntimeData();
            return data;
        }

        if (data == null)
        {
            return null;
        }

        data.RebuildRuntimeData();
        return data;
    }

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    /// <summary>
    /// Recreates the runtime view. Bus items map directly. Imported Stop items
    /// containing passengers map to waiting columns in this project.
    /// </summary>
    public void RebuildRuntimeData()
    {
        EnsureDocument();
        buses.Clear();
        waitingPassengerColumns.Clear();
        preservedBoardItems.Clear();

        levelId = ParseLevelId(level.id, levelId);
        i = level.boardData.grid.width;
        j = level.boardData.grid.height;

        int maxColumn = -1;
        int maxRow = -1;
        for (int itemIndex = 0; itemIndex < level.boardData.items.Count; itemIndex++)
        {
            LevelBoardItem item = level.boardData.items[itemIndex];
            if (item == null || item.data == null || !IsType(item.type, "Bus"))
            {
                continue;
            }

            maxColumn = Mathf.Max(maxColumn, item.data.column);
            maxRow = Mathf.Max(maxRow, item.data.row);
        }

        // ToolLevel centers rows using the total grid height. Imported levels
        // may omit Bus.data.position, so use that same height for the fallback.
        int fallbackGridHeight = Mathf.Max(1, j, maxRow + 1);
        for (int itemIndex = 0; itemIndex < level.boardData.items.Count; itemIndex++)
        {
            LevelBoardItem item = level.boardData.items[itemIndex];
            if (item == null || item.data == null)
            {
                continue;
            }

            if (IsType(item.type, "Bus"))
            {
                BusData bus = CreateBusData(item.data, fallbackGridHeight);
                buses.Add(bus);
                maxColumn = Mathf.Max(maxColumn, bus.column);
                maxRow = Mathf.Max(maxRow, bus.row);
            }
            else if (IsType(item.type, "WaitingPoint", "Waiting", "PassengerQueue") ||
                     (IsType(item.type, "Stop") && HasPassengers(item.data)))
            {
                WaitingPassengerColumnData waiting = CreateWaitingData(
                    item.data,
                    itemIndex,
                    IsType(item.type, "Stop"));
                if (waiting != null)
                {
                    waitingPassengerColumns.Add(waiting);
                }
            }
            else if (!IsType(item.type, "Conveyor"))
            {
                // Keep authored items that this gameplay slice does not consume
                // (for example empty Stops or future mechanics) during Tool saves.
                preservedBoardItems.Add(item);
            }
        }

        if (i <= 0) i = Mathf.Max(1, maxColumn + 1);
        if (j <= 0) j = Mathf.Max(1, maxRow + 1);
    }

    /// <summary>
    /// Rebuilds the canonical board from the runtime projection before saving.
    /// </summary>
    public void SyncDocumentFromRuntimeData()
    {
        EnsureDocument();
        if (string.IsNullOrWhiteSpace(level.id))
        {
            level.id = "Level_" + Mathf.Max(1, levelId).ToString("D2");
        }

        level.boardData.grid.width = Mathf.Max(1, i);
        level.boardData.grid.height = Mathf.Max(1, j);
        level.boardData.items.Clear();

        if (buses != null)
        {
            for (int index = 0; index < buses.Count; index++)
            {
                BusData bus = buses[index];
                if (bus == null) continue;

                LevelBoardItemData itemData = new LevelBoardItemData
                {
                    position = new LevelVector3Data(bus.posX, bus.posY, bus.posZ),
                    column = bus.column,
                    row = bus.row,
                    colorId = bus.colorId,
                    passengerType = bus.colorId,
                    capacity = bus.capacity > 0 ? bus.capacity : BusData.DefaultCapacity,
                    passengerTypes = new List<int>(),
                    passengerTypesCounts = new List<int>(),
                    isVisible = true
                };
                AppendGroupedPassengers(bus.passengerData, itemData.passengerTypes, itemData.passengerTypesCounts);
                level.boardData.items.Add(new LevelBoardItem { type = "Bus", typeId = 3, data = itemData });
            }
        }

        if (waitingPassengerColumns != null)
        {
            for (int index = 0; index < waitingPassengerColumns.Count; index++)
            {
                WaitingPassengerColumnData waiting = waitingPassengerColumns[index];
                if (waiting == null) continue;

                LevelBoardItemData itemData = new LevelBoardItemData
                {
                    position = new LevelVector3Data(waiting.posX, waiting.posY, waiting.posZ),
                    colorId = waiting.colorId,
                    direction = waiting.Direction,
                    passengerType = waiting.colorId,
                    passengerTypes = new List<int>(),
                    passengerTypesCounts = new List<int>(),
                    isVisible = true
                };
                AppendPassengers(waiting.listStickmans, itemData.passengerTypes, itemData.passengerTypesCounts);
                level.boardData.items.Add(new LevelBoardItem { type = "WaitingPoint", typeId = 7, data = itemData });
            }
        }

        if (preservedBoardItems != null)
        {
            for (int index = 0; index < preservedBoardItems.Count; index++)
            {
                if (preservedBoardItems[index] != null)
                {
                    level.boardData.items.Add(preservedBoardItems[index]);
                }
            }
        }
    }

    public void AppendBoardItem(LevelBoardItem item)
    {
        EnsureDocument();
        if (item != null) level.boardData.items.Add(item);
    }

    public IReadOnlyList<LevelBoardItem> BoardItems
    {
        get { EnsureDocument(); return level.boardData.items; }
    }

    private void EnsureDocument()
    {
        if (level == null) level = new LevelDocument();
        if (level.boardData == null) level.boardData = new LevelBoardData();
        if (level.boardData.grid == null) level.boardData.grid = new LevelGridData();
        if (level.boardData.items == null) level.boardData.items = new List<LevelBoardItem>();
        if (buses == null) buses = new List<BusData>();
        if (waitingPassengerColumns == null) waitingPassengerColumns = new List<WaitingPassengerColumnData>();
        if (preservedBoardItems == null) preservedBoardItems = new List<LevelBoardItem>();
    }

    private static JsonSerializer CreateDeserializer()
    {
        return JsonSerializer.Create(new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        });
    }

    private static BusData CreateBusData(LevelBoardItemData source, int fallbackGridHeight)
    {
        LevelVector3Data position = source.position ?? GetDefaultBusPosition(
            source.column,
            source.row,
            fallbackGridHeight
        );
        BusData result = new BusData
        {
            posX = position.x,
            posY = position.y,
            posZ = position.z,
            colorId = source.colorId >= 0 ? source.colorId : Mathf.Max(0, source.passengerType),
            column = source.column,
            row = source.row,
            usesDefaultPosition = source.position == null,
            capacity = source.capacity > 0 ? source.capacity : BusData.DefaultCapacity,
            passengerData = new BusPassengerData()
        };

        List<int> passengerColors = ExpandPassengers(source.passengerTypes, source.passengerTypesCounts);
        for (int index = 0; index < passengerColors.Count; index++)
        {
            result.passengerData.passengers[index] = new StickManData { colorId = passengerColors[index] };
        }
        return result;
    }

    private static WaitingPassengerColumnData CreateWaitingData(LevelBoardItemData source, int itemIndex, bool importedStop)
    {
        if (source == null) return null;
        LevelVector3Data position = source.position ?? new LevelVector3Data();
        if (importedStop && source.position != null)
        {
            // Imported Stop positions are 2D (x/y). The current gameplay board
            // uses x/z for world placement and y=2 for the waiting layer.
            position = new LevelVector3Data(source.position.x, 2f, source.position.y);
        }
        List<int> passengerColors = ExpandPassengers(source.passengerTypes, source.passengerTypesCounts);
        int color = source.colorId >= 0 ? source.colorId : (passengerColors.Count > 0 ? passengerColors[0] : -1);
        return new WaitingPassengerColumnData
        {
            columnSlotId = itemIndex,
            posX = position.x,
            posY = position.y,
            posZ = position.z,
            colorId = color,
            Direction = NormalizeDirection(source.direction),
            listStickmans = CreateStickManList(passengerColors)
        };
    }

    private static bool HasPassengers(LevelBoardItemData source)
    {
        return source != null && source.passengerTypes != null && source.passengerTypes.Count > 0;
    }

    private static List<int> ExpandPassengers(List<int> types, List<int> counts)
    {
        List<int> result = new List<int>();
        if (types == null) return result;
        for (int index = 0; index < types.Count; index++)
        {
            int count = counts != null && index < counts.Count ? counts[index] : 1;
            for (int passengerIndex = 0; passengerIndex < Mathf.Max(0, count); passengerIndex++)
            {
                result.Add(Mathf.Max(0, types[index]));
            }
        }
        return result;
    }

    private static void AppendGroupedPassengers(BusPassengerData source, List<int> types, List<int> counts)
    {
        if (source == null || source.passengers == null) return;
        Dictionary<int, int> grouped = new Dictionary<int, int>();
        foreach (KeyValuePair<int, StickManData> passenger in source.passengers)
        {
            if (passenger.Value == null) continue;
            int color = Mathf.Max(0, passenger.Value.colorId);
            if (!grouped.ContainsKey(color)) grouped[color] = 0;
            grouped[color]++;
        }
        foreach (KeyValuePair<int, int> group in grouped)
        {
            types.Add(group.Key);
            counts.Add(group.Value);
        }
    }

    private static void AppendPassengers(List<StickManData> source, List<int> types, List<int> counts)
    {
        if (source == null) return;
        for (int index = 0; index < source.Count; index++)
        {
            if (source[index] == null) continue;
            types.Add(Mathf.Max(0, source[index].colorId));
            counts.Add(1);
        }
    }

    private static List<StickManData> CreateStickManList(List<int> colors)
    {
        List<StickManData> result = new List<StickManData>();
        for (int index = 0; index < colors.Count; index++) result.Add(new StickManData { colorId = colors[index] });
        return result;
    }

    private static bool IsType(string value, params string[] acceptedTypes)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        for (int index = 0; index < acceptedTypes.Length; index++)
        {
            if (string.Equals(value, acceptedTypes[index], StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static int NormalizeDirection(int value)
    {
        return value == -2 || value == 2 || value == 1 || value == -1 ? value : 1;
    }

    private static int ParseLevelId(string value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return Mathf.Max(1, fallback);
        if (!value.StartsWith("Level", StringComparison.OrdinalIgnoreCase))
        {
            return Mathf.Max(1, fallback);
        }
        int parsed = 0;
        bool foundDigit = false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character < '0' || character > '9')
            {
                if (foundDigit) break;
                continue;
            }
            foundDigit = true;
            parsed = Mathf.Min(100000000, parsed * 10 + character - '0');
        }
        return foundDigit ? Mathf.Max(1, parsed) : Mathf.Max(1, fallback);
    }

    private static LevelVector3Data GetDefaultBusPosition(
        int column,
        int row,
        int gridHeight)
    {
        float centeredRow = row - (Mathf.Max(1, gridHeight) - 1f) * 0.5f;
        return new LevelVector3Data(centeredRow, 2f, -8.6f - column * 2.32f);
    }

    [Serializable]
    private sealed class LegacyLevelData
    {
        public int levelId = 1;
        public int i = 1;
        public int j = 1;
        public List<BusData> buses = new List<BusData>();
        public List<WaitingPassengerColumnData> waitingPassengerColumns = new List<WaitingPassengerColumnData>();
    }
}

[Serializable]
public sealed class LevelDocument
{
    public string id;
    public LevelBoardData boardData = new LevelBoardData();
}

[Serializable]
public sealed class LevelBoardData
{
    public LevelGridData grid = new LevelGridData();
    public List<LevelBoardItem> items = new List<LevelBoardItem>();
}

[Serializable]
public sealed class LevelGridData
{
    public int width;
    public int height;
}

[Serializable]
public sealed class LevelBoardItem
{
    public string type;
    public int typeId;
    public LevelBoardItemData data = new LevelBoardItemData();
}

[Serializable]
public sealed class LevelBoardItemData
{
    public List<LevelVector3Data> positionsColl;
    public List<LevelVector3Data> tanInsColl;
    public List<LevelVector3Data> tanOutsColl;
    public List<float> sizesColl;
    public LevelVector3Data position;
    public LevelVector3Data passengerStopPosition;
    public float rotation;
    public int column = -1;
    public int row = -1;
    public int colorId = -1;
    public int direction = 1;
    public int capacity = BusData.DefaultCapacity;
    public int passengerType = -1;
    public List<int> passengerTypes;
    public List<int> passengerTypesCounts;
    public bool isVisible = true;
    public bool hasLink;
    public int linkId;
    public bool hasKey;
    public int keyId;
    public bool hasAccordion;
    public int accordionId;
    public bool hasMystery;
    public bool isBlocker;
    public List<int> blockerPassengerTypes;
    public List<int> blockerPassengerTypesCounts;
    public bool hasIce;
    public int iceCount;
    public int bombCounter;
    public bool hasLock;
    public int lockId;
    public int idDataId;
    public int splineDirectionData;
    public float splineProgressData;
    public int stopCounterValue;
    public bool isVIPStop;
    public List<int> vipStopPassengerTypes;
    public List<int> vipStopPassengerTypesCounts;
}

[Serializable]
public sealed class LevelVector3Data
{
    public float x;
    public float y;
    public float z;
    public LevelVector3Data() { }
    public LevelVector3Data(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

[Serializable]
public sealed class BusData
{
    public const int DefaultCapacity = 16;
    public float posX;
    public float posY;
    public float posZ;
    public int colorId;
    public int column = -1;
    public int row = -1;
    [JsonIgnore] public bool usesDefaultPosition;
    public int capacity = DefaultCapacity;
    public BusPassengerData passengerData = new BusPassengerData();
}

[Serializable]
public sealed class BusPassengerData
{
    public Dictionary<int, StickManData> passengers = new Dictionary<int, StickManData>();
}

[Serializable]
public sealed class WaitingPassengerColumnData
{
    public int columnSlotId;
    public float posX;
    public float posY;
    public float posZ;
    public int colorId = -1;
    /// <summary>left: -2, right: 2, top: 1, bottom: -1.</summary>
    public int Direction = 1;
    public List<StickManData> listStickmans = new List<StickManData>();
}

[Serializable]
public sealed class StickManData
{
    public int colorId;
}

