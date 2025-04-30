using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();        // add SignalR services

var app = builder.Build();
app.MapHub<ArenaHub>("/play");        // websocket endpoint → /play
app.Run();

public class ArenaHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var room = Room.Assign(Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id);

        if (room.IsFull)
            await Clients.Group(room.Id).SendAsync("start", room.StartPayload());
    }

    public async Task Move(int cell)
    {
        var room = Room.Find(Context.ConnectionId);
        if (!room.TryMove(Context.ConnectionId, cell, out var update)) return;

        await Clients.Group(room.Id).SendAsync("update", update);

        if (update.result is not null)
            await Clients.Group(room.Id).SendAsync("result", update.result);
    }

    public override async Task OnDisconnectedAsync(Exception? _)
    {
        var room = Room.Find(Context.ConnectionId);
        room.Remove(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.Id);
    }
}

record Update(string?[] board, string turn, string? result);

class Room
{
    /* ---- static pool ---- */
    private static readonly List<Room> Pool = [];
    public static Room Assign(string cid)
    {
        var r = Pool.FirstOrDefault(x => !x.IsFull);
        if (r == null)
        {
            Pool.Add(new Room());
            r = Pool.Last();
        }
        r.players.Add(cid);
        return r;
    }
    public static Room Find(string cid) => Pool.Single(r => r.players.Contains(cid));

    /* ---- instance ---- */
    public string Id { get; } = Guid.NewGuid().ToString();
    private readonly List<string> players = [];
    private readonly string?[] board = new string?[9];
    private string turn = "X";

    public bool IsFull => players.Count == 2;

    public object StartPayload() => new { yourMark = "X", turn };

    public bool TryMove(string cid, int cell, out Update update)
    {
        update = null!;
        if (!IsFull || cell is < 0 or > 8 || board[cell] is not null) return false;

        var mark = cid == players[0] ? "X" : "O";
        if (turn != mark) return false;

        board[cell] = mark;
        turn = mark == "X" ? "O" : "X";

        update = new Update(board, turn, CheckWin());
        return true;
    }

    public void Remove(string cid) => players.Remove(cid);

    private string? CheckWin()
    {
        int[][] lines =
        {
            [0,1,2],[3,4,5],[6,7,8],
            [0,3,6],[1,4,7],[2,5,8],
            [0,4,8],[2,4,6]
        };
        foreach (var ln in lines)
            if (board[ln[0]] is { } m && m == board[ln[1]] && m == board[ln[2]])
                return m;
        return board.All(c => c is not null) ? "draw" : null;
    }
}
