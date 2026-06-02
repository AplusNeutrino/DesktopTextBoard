namespace DesktopTextBoard.Models;

public sealed class BoardDocument
{
    public int Version { get; set; } = 1;
    public string ActiveBoardId { get; set; } = "board-default";
    public List<BoardConfig> Boards { get; set; } = new();

    public BoardConfig ActiveBoard
    {
        get
        {
            var board = Boards.FirstOrDefault(x => x.Id == ActiveBoardId);
            if (board is not null)
            {
                return board;
            }

            if (Boards.Count == 0)
            {
                Boards.Add(BoardConfig.CreateDefault());
            }

            ActiveBoardId = Boards[0].Id;
            return Boards[0];
        }
    }

    public static BoardDocument CreateDefault()
    {
        var board = BoardConfig.CreateDefault();
        return new BoardDocument
        {
            ActiveBoardId = board.Id,
            Boards = new List<BoardConfig> { board }
        };
    }
}
