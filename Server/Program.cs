// Program.cs – full, self-contained Tic-Tac-Toe WebSocket demo
// ------------------------------------------------------------

using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Console logging is on by default; we’ll use it for Move diagnostics.
builder.Services.AddSignalR();

var app = builder.Build();

// Serve wwwroot/index.html so you can browse to http://localhost:5000/
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<ArenaHub>("/play");    // WebSocket endpoint
app.Run();


// ========== Hub ==========================================================
public class ArenaHub : Hub
{
    private readonly ILogger<ArenaHub> _log;
    public ArenaHub(ILogger<ArenaHub> log) => _log = log;

    public override async Task OnConnectedAsync()
    {
        var room = Room.Assign(Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id);

        if (room.IsFull)
        {
            // Tell *each* caller its real mark.
            foreach (var cid in room.Players)
                await Clients.Client(cid)
                             .SendAsync("start", room.StartPayload(cid));
        }
    }

    public async Task Move(int cell)
    {
        var room = Room.Find(Context.ConnectionId);

        if (!room.TryMove(Context.ConnectionId, cell, out var update))
        {
            _log.LogInformation("Move rejected | cid={Cid} cell={Cell} reason={Reason}",
                                Context.ConnectionId, cell, room.RejectionReason);
            return;
        }

        _log.LogInformation("Move accepted | cid={Cid} cell={Cell} mark={Mark}",
                            Context.ConnectionId, cell, update.board[cell]);

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


// ========== Room (game state) ============================================
record Update(string?[] board, string turn, string? result);

class Room
{
    // ----- static pool of rooms -----------------------------------------
    private static readonly List<Room> Pool = [];
    public static Room Assign(string cid)
    {
        // try to reuse an open room
        var room = Pool.FirstOrDefault(r => !r.IsFull);

        if (room is null)
        {
            room = new Room();
            Pool.Add(room);
        }

        room.players.Add(cid);
        return room;
    }
    public static Room Find(string cid) => Pool.Single(r => r.players.Contains(cid));

    // ----- instance data -------------------------------------------------
    public string Id { get; } = Guid.NewGuid().ToString();
    private readonly string?[] board = new string?[9];
    private string turn = "X";
    private readonly List<string> players = [];
    public IReadOnlyList<string> Players => players.AsReadOnly();

    public bool IsFull => players.Count == 2;
    public string RejectionReason { get; private set; } = "";

    public object StartPayload(string cid)
        => new { yourMark = cid == players[0] ? "X" : "O", turn };

    public bool TryMove(string cid, int cell, out Update update)
    {
        update = null!;

        if (!IsFull) { RejectionReason = "room_not_full"; return false; }
        if (cell is < 0 or > 8) { RejectionReason = "cell_out_of_range"; return false; }
        if (board[cell] is not null) { RejectionReason = "cell_occupied"; return false; }

        var mark = cid == players[0] ? "X" : "O";
        if (turn != mark) { RejectionReason = "wrong_turn"; return false; }

        board[cell] = mark;
        turn = mark == "X" ? "O" : "X";

        update = new Update(board, turn, CheckWin());
        RejectionReason = "";
        return true;
    }

    public void Remove(string cid) => players.Remove(cid);

    // ----- win / draw detection -----------------------------------------
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
                return m;                          // X or O wins

        return board.All(c => c is not null) ? "draw" : null;
    }
}
