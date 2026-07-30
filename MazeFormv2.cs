using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Adnd.Game;

public sealed class MazeForm : Form
{
    private enum Direction
    {
        North,
        East,
        South,
        West
    }

    private enum CellType { Wall, Floor }

    private const int MaxVisibilityDepth = 5;
    private const int DepthLevels = MaxVisibilityDepth - 1;
    private const int StraightAhead = MaxVisibilityDepth - 1;
    private const int PositionCount = (StraightAhead * 2) + 1;
    private const int TopLeft = 0;
    private const int BottomRight = 1;
    private const int EncounterChanceDenominator = 4;

    private static readonly string[] LevelOneMonsters =
    {
        "Goblin",
        "Kobold",
        "Giant Rat",
        "Orc",
        "Skeleton"
    };

    private readonly PointF[,,] _backWallCoords = new PointF[DepthLevels, PositionCount, 2];

    private CellType[,] _maze;
    private Point _position = new(0, 0);
    private Direction _direction = Direction.North;
    private readonly Random _random = new();

    private void BuildMaze()
    {
        _maze = new CellType[22, 22];

        for (int y = 0; y < 22; y++)
            for (int x = 0; x < 22; x++)
                _maze[x, y] = CellType.Wall;

        for (int y = 1; y <= 8; y++)
            _maze[3, y] = CellType.Floor;

        for (int y = 0; y <= 8; y++)
            _maze[0, y] = CellType.Floor;

        for (int x = 0; x <= 8; x++)
            _maze[x, 0] = CellType.Floor;
    }

    public MazeForm()
    {
        Text = "Maze";
        ClientSize = new Size(1024, 640);
        KeyPreview = true;
        BackColor = Color.Black;
        ForeColor = Color.White;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        BuildMaze();
        KeyDown += MazeForm_KeyDown;
    }

    private void MazeForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.W:
            case Keys.Up:
                TryMoveForward();
                Invalidate();
                break;
            case Keys.A:
            case Keys.Left:
                _direction = TurnLeft(_direction);
                Invalidate();
                break;
            case Keys.D:
            case Keys.Right:
                _direction = TurnRight(_direction);
                Invalidate();
                break;
            case Keys.B:
            case Keys.Escape:
                Close();
                break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.Clear(Color.Black);
        e.Graphics.SmoothingMode = SmoothingMode.None;

        using var pen = new Pen(Color.White, 2f);
        var viewport = new RectangleF(12f, 12f, ClientSize.Width - 24f, ClientSize.Height - 80f);
        e.Graphics.DrawRectangle(pen, viewport.X, viewport.Y, viewport.Width, viewport.Height);

        InitWallCoords(viewport);
        DrawScene(e.Graphics, pen, viewport);
        DrawStatus(e.Graphics);
    }

    private void InitWallCoords(RectangleF viewport)
    {
        var frames = BuildFrames(viewport, DepthLevels);

        for (int depth = 0; depth < DepthLevels; depth++)
        {
            var centerRect = frames[depth + 1];
            SetWallRect(depth, StraightAhead, centerRect);

            float wallWidth = centerRect.Width;
            for (int offset = 1; offset <= StraightAhead; offset++)
            {
                SetWallRect(depth, StraightAhead - offset, ShiftRect(centerRect, -(wallWidth * offset)));
                SetWallRect(depth, StraightAhead + offset, ShiftRect(centerRect, wallWidth * offset));
            }
        }
    }

    private void DrawScene(Graphics graphics, Pen pen, RectangleF viewport)
    {
        for (int depth = DepthLevels - 1; depth >= 0; depth--)
        {
            var centerCell = GetCellFartherAway(_position, _direction, depth);
            DrawCellContents(graphics, pen, viewport, centerCell, depth, StraightAhead);

            for (int i = 1; i <= depth + 1; i++)
            {
                var leftCell = GetCellToTheLeft(centerCell, _direction, i);
                DrawCellContents(graphics, pen, viewport, leftCell, depth, StraightAhead - i);

                var rightCell = GetCellToTheRight(centerCell, _direction, i);
                DrawCellContents(graphics, pen, viewport, rightCell, depth, StraightAhead + i);
            }
        }
    }

    private void DrawCellContents(Graphics graphics, Pen pen, RectangleF viewport, Point cell, int depth, int position)
    {
        if (!IsOpen(cell) || depth < 0 || depth >= DepthLevels || position < 0 || position >= PositionCount)
            return;

        var back = GetWallRect(depth, position);
        if (back.Height < 2f)
            return;

        bool backWallDrawn = false;
        bool leftWallDrawn = false;
        bool rightWallDrawn = false;

        var frontCell = GetCellFartherAway(cell, _direction, 1);
        if (!IsOpen(frontCell))
        {
            using var wallBrush = new SolidBrush(Color.Black);
            graphics.FillRectangle(wallBrush, back);
            graphics.DrawRectangle(pen, back.X, back.Y, back.Width, back.Height);
            backWallDrawn = true;
        }

        if (position <= StraightAhead)
        {
            var leftCell = GetCellToTheLeft(cell, _direction, 1);
            if (!IsOpen(leftCell))
            {
                DrawLeftWall(graphics, pen, viewport, depth, position, back);
                leftWallDrawn = true;
            }
        }

        if (position >= StraightAhead)
        {
            var rightCell = GetCellToTheRight(cell, _direction, 1);
            if (!IsOpen(rightCell))
            {
                DrawRightWall(graphics, pen, viewport, depth, position, back);
                rightWallDrawn = true;
            }
        }

        var frontLeft = GetCellToTheLeft(frontCell, _direction, 1);
        var frontRight = GetCellToTheRight(frontCell, _direction, 1);

        bool drawLeftVertical = (backWallDrawn && (leftWallDrawn || !IsOpen(frontLeft))) ||
                                (leftWallDrawn && !IsOpen(frontLeft));
        bool drawRightVertical = (backWallDrawn && (rightWallDrawn || !IsOpen(frontRight))) ||
                                 (rightWallDrawn && !IsOpen(frontRight));

        if (drawLeftVertical)
        {
            graphics.DrawLine(pen,
                new PointF(back.Left, back.Top),
                new PointF(back.Left, back.Bottom));
        }

        if (drawRightVertical)
        {
            graphics.DrawLine(pen,
                new PointF(back.Right, back.Top),
                new PointF(back.Right, back.Bottom));
        }
    }

    private void DrawLeftWall(Graphics graphics, Pen pen, RectangleF viewport, int depth, int position, RectangleF back)
    {
        PointF nearTop;
        PointF nearBottom;

        if (depth == 0)
        {
            nearTop = new PointF(viewport.Left, viewport.Top);
            nearBottom = new PointF(viewport.Left, viewport.Bottom);
        }
        else
        {
            var nearRect = GetWallRect(depth - 1, position);
            nearTop = new PointF(nearRect.Left, nearRect.Top);
            nearBottom = new PointF(nearRect.Left, nearRect.Bottom);
        }

        var farTop = new PointF(back.Left, back.Top);
        var farBottom = new PointF(back.Left, back.Bottom);

        var wall = new[] { nearTop, farTop, farBottom, nearBottom };
        using var wallBrush = new SolidBrush(Color.Black);
        graphics.FillPolygon(wallBrush, wall);

        graphics.DrawLine(pen, nearTop, farTop);
        graphics.DrawLine(pen, nearBottom, farBottom);
        graphics.DrawLine(pen, nearTop, nearBottom);
        graphics.DrawLine(pen, farTop, farBottom);
    }

    private void DrawRightWall(Graphics graphics, Pen pen, RectangleF viewport, int depth, int position, RectangleF back)
    {
        PointF nearTop;
        PointF nearBottom;

        if (depth == 0)
        {
            nearTop = new PointF(viewport.Right, viewport.Top);
            nearBottom = new PointF(viewport.Right, viewport.Bottom);
        }
        else
        {
            var nearRect = GetWallRect(depth - 1, position);
            nearTop = new PointF(nearRect.Right, nearRect.Top);
            nearBottom = new PointF(nearRect.Right, nearRect.Bottom);
        }

        var farTop = new PointF(back.Right, back.Top);
        var farBottom = new PointF(back.Right, back.Bottom);

        var wall = new[] { nearTop, farTop, farBottom, nearBottom };
        using var wallBrush = new SolidBrush(Color.Black);
        graphics.FillPolygon(wallBrush, wall);

        graphics.DrawLine(pen, nearTop, farTop);
        graphics.DrawLine(pen, nearBottom, farBottom);
        graphics.DrawLine(pen, nearTop, nearBottom);
        graphics.DrawLine(pen, farTop, farBottom);
    }

    private void SetWallRect(int depth, int position, RectangleF rect)
    {
        _backWallCoords[depth, position, TopLeft] = new PointF(rect.Left, rect.Top);
        _backWallCoords[depth, position, BottomRight] = new PointF(rect.Right, rect.Bottom);
    }

    private RectangleF GetWallRect(int depth, int position)
    {
        var tl = _backWallCoords[depth, position, TopLeft];
        var br = _backWallCoords[depth, position, BottomRight];
        return RectangleF.FromLTRB(tl.X, tl.Y, br.X, br.Y);
    }

    private static RectangleF ShiftRect(RectangleF rect, float dx) =>
        RectangleF.FromLTRB(rect.Left + dx, rect.Top, rect.Right + dx, rect.Bottom);

    private void DrawStatus(Graphics graphics)
    {
        var heading = _direction switch
        {
            Direction.North => "North",
            Direction.East => "East",
            Direction.South => "South",
            _ => "West"
        };

        var status = $"A/D or ←/→: Turn   W or ↑: Move   Esc/B: Back   Pos: ({_position.X},{_position.Y})   Facing: {heading}";
        graphics.DrawString(status, Font, Brushes.White, new PointF(16f, ClientSize.Height - 54f));
    }

    private static RectangleF[] BuildFrames(RectangleF viewport, int maxDepth)
    {
        var frames = new RectangleF[maxDepth + 1];
        float cx = viewport.Left + (viewport.Width / 2f);
        float cy = viewport.Top + (viewport.Height / 2f);

        for (int d = 0; d <= maxDepth; d++)
        {
            float u = d / (float)(maxDepth + 1);
            float t = 1f - (float)Math.Pow(1f - u, 1.8f);

            float left = Lerp(viewport.Left + 160f, cx, t);
            float right = Lerp(viewport.Right - 160f, cx, t);
            float top = Lerp(viewport.Top + 100f, cy, t);
            float bottom = Lerp(viewport.Bottom - 100f, cy, t);

            frames[d] = RectangleF.FromLTRB(left, top, right, bottom);
        }

        return frames;
    }

    private bool IsOpen(Point tile)
    {
        if (tile.X < 0 || tile.Y < 0 || tile.X >= _maze.GetLength(0) || tile.Y >= _maze.GetLength(1))
            return false;

        return _maze[tile.X, tile.Y] == CellType.Floor;
    }

    private void TryMoveForward()
    {
        var v = GetForwardVector(_direction);
        var candidate = new Point(_position.X + v.X, _position.Y + v.Y);
        if (IsOpen(candidate))
        {
            _position = candidate;
            TryRandomEncounter();
        }
    }

    private void TryRandomEncounter()
    {
        if (_random.Next(EncounterChanceDenominator) != 0)
            return;

        var monsterName = LevelOneMonsters[_random.Next(LevelOneMonsters.Length)];
        var numberOfMonsters = _random.Next(1, 7); // 1d6

        MessageBox.Show(
            this,
            $"Encounter!\n\n{numberOfMonsters} x {monsterName}",
            "Monsters",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static Point GetCellFartherAway(Point referencePoint, Direction direction, int distance)
    {
        return direction switch
        {
            Direction.North => new Point(referencePoint.X, referencePoint.Y - distance),
            Direction.South => new Point(referencePoint.X, referencePoint.Y + distance),
            Direction.East => new Point(referencePoint.X + distance, referencePoint.Y),
            _ => new Point(referencePoint.X - distance, referencePoint.Y)
        };
    }

    private static Point GetCellToTheLeft(Point referencePoint, Direction referenceDirection, int distance)
    {
        return referenceDirection switch
        {
            Direction.North => new Point(referencePoint.X - distance, referencePoint.Y),
            Direction.South => new Point(referencePoint.X + distance, referencePoint.Y),
            Direction.East => new Point(referencePoint.X, referencePoint.Y - distance),
            _ => new Point(referencePoint.X, referencePoint.Y + distance)
        };
    }

    private static Point GetCellToTheRight(Point referencePoint, Direction referenceDirection, int distance)
    {
        return referenceDirection switch
        {
            Direction.North => new Point(referencePoint.X + distance, referencePoint.Y),
            Direction.South => new Point(referencePoint.X - distance, referencePoint.Y),
            Direction.East => new Point(referencePoint.X, referencePoint.Y + distance),
            _ => new Point(referencePoint.X, referencePoint.Y - distance)
        };
    }

    private static Direction TurnLeft(Direction direction) => direction switch
    {
        Direction.North => Direction.West,
        Direction.West => Direction.South,
        Direction.South => Direction.East,
        _ => Direction.North
    };

    private static Direction TurnRight(Direction direction) => direction switch
    {
        Direction.North => Direction.East,
        Direction.East => Direction.South,
        Direction.South => Direction.West,
        _ => Direction.North
    };

    private static Point GetForwardVector(Direction direction) => direction switch
    {
        Direction.North => new Point(0, -1),
        Direction.East => new Point(1, 0),
        Direction.South => new Point(0, 1),
        _ => new Point(-1, 0)
    };

    private static float Lerp(float start, float end, float t) => start + ((end - start) * t);
}
