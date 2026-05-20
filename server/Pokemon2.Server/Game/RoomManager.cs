using Pokemon2.Server.Infrastructure;

namespace Pokemon2.Server.Game;

public sealed class RoomManager
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RoomActor> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly MapCatalog _maps;
    private readonly ServerMetrics _metrics;

    public RoomManager(MapCatalog maps, ServerMetrics metrics)
    {
        _maps = maps;
        _metrics = metrics;
        CreateRoom("기본 방", "hometown");
    }

    public RoomSummary CreateRoom(string? roomName, string? mapId)
    {
        var id = $"room-{Guid.NewGuid():N}"[..13];
        var map = _maps.GetOrDefault(mapId);
        var name = string.IsNullOrWhiteSpace(roomName) ? $"방 {_rooms.Count + 1}" : roomName.Trim();
        var room = new RoomActor(id, name, map, _metrics);

        lock (_gate)
        {
            _rooms[id] = room;
        }

        _metrics.IncrementRoomCreated();
        return room.ToSummary();
    }

    public RoomActor GetOrCreate(string? roomId)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(roomId) && _rooms.TryGetValue(roomId, out var room))
            {
                return room;
            }

            return _rooms.Values.First();
        }
    }

    public IReadOnlyCollection<RoomSummary> ListRooms()
    {
        lock (_gate)
        {
            return _rooms.Values.Select(room => room.ToSummary()).ToArray();
        }
    }
}
