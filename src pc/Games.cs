using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Video2Gif
{
    /* --------------------------------------------------------------- Сапёр */

    internal sealed class MinesweeperGame
    {
        public const int Cols = 9;
        public const int Rows = 9;
        public const int MineCount = 10;

        private readonly bool[] mines = new bool[Cols * Rows];
        private readonly bool[] revealed = new bool[Cols * Rows];
        private readonly bool[] flags = new bool[Cols * Rows];
        private readonly Random rng = new Random();
        private bool generated;

        public bool Dead { get; private set; }
        public bool Won { get; private set; }
        public int Flags { get; private set; }
        public int Opened { get; private set; }

        public bool IsGenerated
        {
            get { return generated; }
        }

        public int MinesLeft
        {
            get { return MineCount - Flags; }
        }

        public void Reset()
        {
            Array.Clear(mines, 0, mines.Length);
            Array.Clear(revealed, 0, revealed.Length);
            Array.Clear(flags, 0, flags.Length);
            generated = false;
            Dead = false;
            Won = false;
            Flags = 0;
            Opened = 0;
        }

        public bool IsMine(int x, int y)
        {
            return Inside(x, y) && mines[Index(x, y)];
        }

        public bool IsRevealed(int x, int y)
        {
            return Inside(x, y) && revealed[Index(x, y)];
        }

        public bool IsFlagged(int x, int y)
        {
            return Inside(x, y) && flags[Index(x, y)];
        }

        public int Near(int x, int y)
        {
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    if (Inside(nx, ny) && mines[Index(nx, ny)]) count++;
                }
            }
            return count;
        }

        public void ToggleFlag(int x, int y)
        {
            if (Dead || Won || !Inside(x, y)) return;
            int i = Index(x, y);
            if (revealed[i]) return;
            if (!flags[i] && Flags >= MineCount) return;
            flags[i] = !flags[i];
            Flags += flags[i] ? 1 : -1;
        }

        public void Reveal(int x, int y)
        {
            if (Dead || Won || !Inside(x, y)) return;
            int i = Index(x, y);
            if (revealed[i] || flags[i]) return;

            if (!generated) Generate(x, y);
            if (mines[i])
            {
                Dead = true;
                return;
            }

            FloodReveal(x, y);
            if (Opened >= Cols * Rows - MineCount) Won = true;
        }

        private void Generate(int safeX, int safeY)
        {
            generated = true;
            List<int> free = new List<int>();
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    if (Math.Abs(x - safeX) <= 1 && Math.Abs(y - safeY) <= 1) continue;
                    free.Add(Index(x, y));
                }
            }
            for (int i = 0; i < MineCount && free.Count > 0; i++)
            {
                int pick = rng.Next(free.Count);
                mines[free[pick]] = true;
                free.RemoveAt(pick);
            }
        }

        private void FloodReveal(int startX, int startY)
        {
            Stack<Point> stack = new Stack<Point>();
            stack.Push(new Point(startX, startY));
            while (stack.Count > 0)
            {
                Point p = stack.Pop();
                if (!Inside(p.X, p.Y)) continue;
                int i = Index(p.X, p.Y);
                if (revealed[i] || flags[i] || mines[i]) continue;
                revealed[i] = true;
                Opened++;
                if (Near(p.X, p.Y) > 0) continue;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = p.X + dx;
                        int ny = p.Y + dy;
                        if (Inside(nx, ny) && !revealed[Index(nx, ny)] &&
                            !flags[Index(nx, ny)])
                        {
                            stack.Push(new Point(nx, ny));
                        }
                    }
                }
            }
        }

        private static bool Inside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Cols && y < Rows;
        }

        private static int Index(int x, int y)
        {
            return y * Cols + x;
        }
    }

    internal sealed class MinesweeperForm : ThemedForm
    {
        private readonly MinesweeperGame game = new MinesweeperGame();
        private readonly Timer timer = new Timer();
        private readonly int cell;
        private readonly Rectangle board;
        private readonly PillButton restartButton;
        private readonly PillButton closeButton;

        private int elapsed;
        private int best;
        private bool started;
        private int hoverX = -1;
        private int hoverY = -1;

        public MinesweeperForm()
        {
            cell = Ui.P(34);
            board = new Rectangle(Ui.P(24), Ui.P(84), MinesweeperGame.Cols * cell,
                MinesweeperGame.Rows * cell);

            Text = "Сапёр — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            ClientSize = new Size(board.Right + Ui.P(24), Ui.P(470));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            best = AppSettings.GetInt("mines.best", 0);

            restartButton = new PillButton();
            restartButton.Ghost = true;
            restartButton.Text = "Заново";
            restartButton.Font = Ui.Small;
            restartButton.SetBounds(Ui.P(24), board.Bottom + Ui.P(16), Ui.P(140), Ui.P(40));
            restartButton.Click += delegate { Restart(); };
            Controls.Add(restartButton);

            closeButton = new PillButton();
            closeButton.Ghost = true;
            closeButton.Text = "Закрыть";
            closeButton.Font = Ui.Small;
            closeButton.SetBounds(board.Right - Ui.P(160), board.Bottom + Ui.P(16),
                Ui.P(160), Ui.P(40));
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);

            timer.Interval = 1000;
            timer.Tick += delegate
            {
                if (!started || game.Dead || game.Won) return;
                elapsed++;
                Invalidate();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyTitleBar(Handle);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            timer.Dispose();
            base.OnFormClosed(e);
        }

        private void Restart()
        {
            game.Reset();
            timer.Stop();
            elapsed = 0;
            started = false;
            Invalidate();
        }

        private void StartTimer()
        {
            if (started) return;
            started = true;
            timer.Start();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int x, y;
            if (CellAt(e.Location, out x, out y))
            {
                if (x != hoverX || y != hoverY)
                {
                    hoverX = x;
                    hoverY = y;
                    Invalidate(NewRect(x, y));
                }
            }
            else if (hoverX >= 0)
            {
                Rectangle old = NewRect(hoverX, hoverY);
                hoverX = -1;
                hoverY = -1;
                Invalidate(old);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverX >= 0) Invalidate(NewRect(hoverX, hoverY));
            hoverX = -1;
            hoverY = -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int x, y;
            if (!CellAt(e.Location, out x, out y)) return;
            if (e.Button == MouseButtons.Right)
            {
                game.ToggleFlag(x, y);
                Invalidate();
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            if (game.Dead || game.Won)
            {
                Restart();
                return;
            }
            if (!started && !game.IsGenerated) StartTimer();
            game.Reveal(x, y);
            if (game.Dead || game.Won)
            {
                timer.Stop();
                started = false;
                if (game.Won && (best == 0 || elapsed < best))
                {
                    best = elapsed;
                    AppSettings.Set("mines.best", best.ToString());
                }
            }
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyCode == Keys.R)
            {
                Restart();
                e.Handled = true;
            }
        }

        private bool CellAt(Point point, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (!board.Contains(point)) return false;
            x = (point.X - board.X) / cell;
            y = (point.Y - board.Y) / cell;
            return x >= 0 && y >= 0 && x < MinesweeperGame.Cols && y < MinesweeperGame.Rows;
        }

        private Rectangle NewRect(int x, int y)
        {
            return new Rectangle(board.X + x * cell, board.Y + y * cell, cell, cell);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Ui.Write(g, "Сапёр", Ui.H1,
                new Rectangle(Ui.P(24), Ui.P(12), Ui.P(220), Ui.P(30)), Ui.Text, Ui.LeftMid);
            Ui.Write(g, "ЛКМ — открыть · ПКМ — флажок · R — заново", Ui.Tiny,
                new Rectangle(Ui.P(24), Ui.P(48), Ui.P(300), Ui.P(18)),
                Ui.Faint, Ui.LeftMid);

            TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            string status = "Мины: " + game.MinesLeft + "   ·   Время: " + elapsed + " с";
            if (best > 0) status += "   ·   рекорд: " + best + " с";
            Ui.Write(g, status, Ui.Small,
                new Rectangle(Ui.P(24), Ui.P(66), ClientSize.Width - Ui.P(48), Ui.P(18)),
                game.Dead ? Ui.Bad : (game.Won ? Ui.Good : Ui.Muted), right);

            using (GraphicsPath path = Ui.Round(board, Ui.P(10)))
            {
                using (SolidBrush brush = new SolidBrush(Ui.SurfaceColor(Ui.Card)))
                    g.FillPath(brush, path);
                g.SetClip(path);
                for (int y = 0; y < MinesweeperGame.Rows; y++)
                {
                    for (int x = 0; x < MinesweeperGame.Cols; x++)
                    {
                        DrawCell(g, x, y);
                    }
                }
                g.ResetClip();
                Ui.StrokeRound(g, board, Ui.P(10), Ui.Border, 1f);
            }

            if (game.Dead || game.Won)
            {
                Rectangle overlay = new Rectangle(board.X, board.Bottom - Ui.P(40),
                    board.Width, Ui.P(40));
                using (SolidBrush veil = new SolidBrush(Color.FromArgb(190, Ui.Card)))
                    g.FillRectangle(veil, overlay);
                Ui.Write(g, game.Won ? "Победа!" : "Взрыв!",
                    Ui.H2, overlay, game.Won ? Ui.Good : Ui.Bad, Ui.CenterMid);
            }
        }

        private void DrawCell(Graphics g, int x, int y)
        {
            Rectangle r = NewRect(x, y);
            Rectangle inner = new Rectangle(r.X + 1, r.Y + 1,
                Math.Max(1, r.Width - 2), Math.Max(1, r.Height - 2));
            bool open = game.IsRevealed(x, y);
            if (open)
            {
                Ui.FillRound(g, inner, Ui.P(4), Ui.Bg);
                if (game.IsMine(x, y))
                {
                    DrawMine(g, inner);
                }
                else
                {
                    int near = game.Near(x, y);
                    if (near > 0)
                    {
                        Color color = near == 1 ? Ui.Muted
                            : near == 2 ? Ui.Good
                            : near == 3 ? Ui.Bad
                            : Ui.Accent;
                        Ui.Write(g, near.ToString(), Ui.Semi, inner, color, Ui.CenterMid);
                    }
                }
                return;
            }

            Color hidden = hoverX == x && hoverY == y
                ? Ui.BorderStrong
                : Ui.Track;
            Ui.FillRound(g, inner, Ui.P(4), hidden);
            Ui.StrokeRound(g, inner, Ui.P(4), Ui.Border, 1f);
            if (game.IsFlagged(x, y)) DrawFlag(g, inner);
            else if (game.Dead && game.IsMine(x, y)) DrawMine(g, inner);
        }

        private static void DrawMine(Graphics g, Rectangle r)
        {
            int size = Math.Max(4, r.Width / 3);
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            using (SolidBrush brush = new SolidBrush(Ui.Text))
                g.FillEllipse(brush, cx - size / 2, cy - size / 2, size, size);
            using (Pen pen = new Pen(Ui.Bad, Math.Max(1.2f, size * 0.16f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - size, cy, cx + size, cy);
                g.DrawLine(pen, cx, cy - size, cx, cy + size);
            }
        }

        private static void DrawFlag(Graphics g, Rectangle r)
        {
            int cx = r.X + r.Width / 2;
            int top = r.Y + r.Height / 4;
            int height = Math.Max(4, r.Height / 2);
            using (Pen pole = new Pen(Ui.Muted, Math.Max(1.2f, Ui.P(1.4f))))
            {
                pole.StartCap = LineCap.Round;
                pole.EndCap = LineCap.Round;
                g.DrawLine(pole, cx, top + height, cx, top);
            }
            using (SolidBrush flag = new SolidBrush(Ui.Bad))
            {
                Point[] points =
                {
                    new Point(cx, top),
                    new Point(cx + Math.Max(4, r.Width / 4), top + Math.Max(2, r.Height / 8)),
                    new Point(cx, top + Math.Max(4, r.Height / 4))
                };
                g.FillPolygon(flag, points);
            }
        }
    }

    /* ---------------------------------------------------------- Блок Бласт */

    internal sealed class BlockPiece
    {
        public readonly Point[] Cells;
        public readonly int ColorIndex;

        public BlockPiece(Point[] cells, int colorIndex)
        {
            Cells = cells;
            ColorIndex = colorIndex;
        }

        public int Width
        {
            get
            {
                int max = 0;
                foreach (Point p in Cells) if (p.X + 1 > max) max = p.X + 1;
                return Math.Max(1, max);
            }
        }

        public int Height
        {
            get
            {
                int max = 0;
                foreach (Point p in Cells) if (p.Y + 1 > max) max = p.Y + 1;
                return Math.Max(1, max);
            }
        }
    }

    internal sealed class BlockBlastGame
    {
        public const int Size = 8;

        private static readonly int[][,] Shapes =
        {
            new int[,] { {0,0} },
            new int[,] { {0,0},{0,1} },
            new int[,] { {0,0},{1,0} },
            new int[,] { {0,0},{0,1},{0,2} },
            new int[,] { {0,0},{1,0},{2,0} },
            new int[,] { {0,0},{0,1},{1,0},{1,1} },
            new int[,] { {0,0},{0,1},{0,2},{1,0},{1,1},{1,2} },
            new int[,] { {0,0},{1,0},{2,0},{2,1} },
            new int[,] { {0,0},{0,1},{1,0} },
            new int[,] { {0,1},{1,0},{1,1} },
            new int[,] { {0,0},{0,1},{1,1} },
            new int[,] { {0,0},{1,0},{1,1},{2,1} },
            new int[,] { {0,1},{1,0},{1,1},{2,0} },
            new int[,] { {0,0},{0,1},{0,2},{1,1} }
        };

        private readonly Random rng = new Random();
        public readonly int[,] Board = new int[Size, Size];
        public readonly BlockPiece[] Pieces = new BlockPiece[3];
        public int Score;
        public bool Over;

        public BlockBlastGame()
        {
            Reset();
        }

        public void Reset()
        {
            Array.Clear(Board, 0, Board.Length);
            Score = 0;
            Over = false;
            FillPieces();
        }

        public bool CanPlace(BlockPiece piece, int x, int y)
        {
            if (piece == null) return false;
            foreach (Point cell in piece.Cells)
            {
                int bx = x + cell.X;
                int by = y + cell.Y;
                if (bx < 0 || by < 0 || bx >= Size || by >= Size) return false;
                if (Board[bx, by] != 0) return false;
            }
            return true;
        }

        public bool Place(int index, int x, int y)
        {
            if (Over || index < 0 || index >= Pieces.Length) return false;
            BlockPiece piece = Pieces[index];
            if (!CanPlace(piece, x, y)) return false;

            foreach (Point cell in piece.Cells)
            {
                Board[x + cell.X, y + cell.Y] = piece.ColorIndex + 1;
            }
            Pieces[index] = null;
            Score += piece.Cells.Length;

            int lines = ClearLines();
            if (lines > 0) Score += lines * 15;

            if (Pieces[0] == null && Pieces[1] == null && Pieces[2] == null)
                FillPieces();
            if (!HasMoves()) Over = true;
            return true;
        }

        public bool HasMoves()
        {
            for (int i = 0; i < Pieces.Length; i++)
            {
                if (Pieces[i] == null) continue;
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        if (CanPlace(Pieces[i], x, y)) return true;
                    }
                }
            }
            return false;
        }

        private int ClearLines()
        {
            bool[] rows = new bool[Size];
            bool[] cols = new bool[Size];
            int lines = 0;
            for (int y = 0; y < Size; y++)
            {
                bool full = true;
                for (int x = 0; x < Size; x++)
                    if (Board[x, y] == 0) { full = false; break; }
                rows[y] = full;
                if (full) lines++;
            }
            for (int x = 0; x < Size; x++)
            {
                bool full = true;
                for (int y = 0; y < Size; y++)
                    if (Board[x, y] == 0) { full = false; break; }
                cols[x] = full;
                if (full) lines++;
            }
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (rows[y] || cols[x]) Board[x, y] = 0;
            return lines;
        }

        private void FillPieces()
        {
            for (int i = 0; i < Pieces.Length; i++)
            {
                int[,] shape = Shapes[rng.Next(Shapes.Length)];
                int count = shape.GetLength(0);
                Point[] cells = new Point[count];
                for (int j = 0; j < count; j++)
                    cells[j] = new Point(shape[j, 0], shape[j, 1]);
                Pieces[i] = new BlockPiece(cells, rng.Next(5));
            }
        }
    }

    internal sealed class BlockBlastForm : ThemedForm
    {
        private static readonly Color[] BlockColors =
        {
            Color.FromArgb(232, 201, 83),
            Color.FromArgb(96, 160, 235),
            Color.FromArgb(235, 110, 160),
            Color.FromArgb(120, 200, 130),
            Color.FromArgb(235, 150, 80)
        };

        private readonly BlockBlastGame game = new BlockBlastGame();
        private readonly int cell;
        private readonly Rectangle board;
        private readonly Rectangle[] slots = new Rectangle[3];
        private readonly PillButton restartButton;
        private readonly PillButton closeButton;

        private int selected = -1;
        private int hoverX = -1;
        private int hoverY = -1;
        private int best;

        public BlockBlastForm()
        {
            cell = Ui.P(40);
            board = new Rectangle(Ui.P(38), Ui.P(92), BlockBlastGame.Size * cell,
                BlockBlastGame.Size * cell);
            int slotY = board.Bottom + Ui.P(16);
            int slotWidth = Ui.P(100);
            int gap = Ui.P(10);
            int total = slotWidth * 3 + gap * 2;
            int left = board.X + (board.Width - total) / 2;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new Rectangle(left + i * (slotWidth + gap), slotY,
                    slotWidth, Ui.P(94));

            Text = "Блок Бласт — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            ClientSize = new Size(board.Right + Ui.P(38), Ui.P(620));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            best = AppSettings.GetInt("blockblast.best", 0);

            restartButton = new PillButton();
            restartButton.Ghost = true;
            restartButton.Text = "Заново";
            restartButton.Font = Ui.Small;
            restartButton.SetBounds(Ui.P(38), Ui.P(552), Ui.P(140), Ui.P(40));
            restartButton.Click += delegate { Restart(); };
            Controls.Add(restartButton);

            closeButton = new PillButton();
            closeButton.Ghost = true;
            closeButton.Text = "Закрыть";
            closeButton.Font = Ui.Small;
            closeButton.SetBounds(board.Right - Ui.P(160), Ui.P(552), Ui.P(160), Ui.P(40));
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyTitleBar(Handle);
        }

        private void Restart()
        {
            game.Reset();
            selected = -1;
            hoverX = -1;
            hoverY = -1;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int x = -1, y = -1;
            if (board.Contains(e.Location))
            {
                x = (e.Location.X - board.X) / cell;
                y = (e.Location.Y - board.Y) / cell;
            }
            if (x != hoverX || y != hoverY)
            {
                hoverX = x;
                hoverY = y;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverX = -1;
            hoverY = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Contains(e.Location))
                {
                    selected = selected == i ? -1 : i;
                    Invalidate();
                    return;
                }
            }

            if (!board.Contains(e.Location)) return;
            if (game.Over || selected < 0 || game.Pieces[selected] == null) return;
            int x = (e.Location.X - board.X) / cell;
            int y = (e.Location.Y - board.Y) / cell;
            if (game.Place(selected, x, y))
            {
                selected = -1;
                if (game.Score > best)
                {
                    best = game.Score;
                    AppSettings.Set("blockblast.best", best.ToString());
                }
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyCode == Keys.R)
            {
                Restart();
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Ui.Write(g, "Блок Бласт", Ui.H1,
                new Rectangle(Ui.P(38), Ui.P(12), Ui.P(300), Ui.P(30)), Ui.Text, Ui.LeftMid);
            Ui.Write(g, "Выберите фигуру и поставьте её на поле", Ui.Tiny,
                new Rectangle(Ui.P(38), Ui.P(48), Ui.P(340), Ui.P(18)),
                Ui.Faint, Ui.LeftMid);
            TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            string info = "Счёт: " + game.Score + "   ·   рекорд: " + best;
            Ui.Write(g, info, Ui.Small,
                new Rectangle(Ui.P(38), Ui.P(66), ClientSize.Width - Ui.P(76), Ui.P(18)),
                game.Over ? Ui.Bad : Ui.Muted, right);

            using (GraphicsPath path = Ui.Round(board, Ui.P(12)))
            {
                using (SolidBrush brush = new SolidBrush(Ui.SurfaceColor(Ui.Card)))
                    g.FillPath(brush, path);
                g.SetClip(path);
                for (int y = 0; y < BlockBlastGame.Size; y++)
                {
                    for (int x = 0; x < BlockBlastGame.Size; x++)
                    {
                        DrawBoardCell(g, x, y);
                    }
                }
                g.ResetClip();
                Ui.StrokeRound(g, board, Ui.P(12), Ui.Border, 1f);
            }

            for (int i = 0; i < slots.Length; i++)
                DrawSlot(g, i);
            DrawPreview(g);

            if (game.Over)
            {
                Rectangle overlay = new Rectangle(board.X, board.Bottom - Ui.P(46),
                    board.Width, Ui.P(46));
                using (SolidBrush veil = new SolidBrush(Color.FromArgb(195, Ui.Card)))
                    g.FillRectangle(veil, overlay);
                Ui.Write(g, "Игра окончена", Ui.H2, overlay, Ui.Bad, Ui.CenterMid);
            }
        }

        private Rectangle CellRect(int x, int y)
        {
            return new Rectangle(board.X + x * cell, board.Y + y * cell, cell, cell);
        }

        private void DrawBoardCell(Graphics g, int x, int y)
        {
            Rectangle c = CellRect(x, y);
            Rectangle inner = new Rectangle(c.X + 1, c.Y + 1,
                Math.Max(1, c.Width - 2), Math.Max(1, c.Height - 2));
            int value = game.Board[x, y];
            if (value == 0)
            {
                Ui.FillRound(g, inner, Ui.P(6), Ui.Bg);
                Ui.StrokeRound(g, inner, Ui.P(6), Ui.Border, 1f);
            }
            else
            {
                Ui.FillRound(g, inner, Ui.P(6), BlockColors[(value - 1) % BlockColors.Length]);
            }
        }

        private void DrawSlot(Graphics g, int index)
        {
            Rectangle slot = slots[index];
            bool active = selected == index;
            Ui.FillRound(g, slot, Ui.P(10), Ui.SurfaceColor(Ui.Bg));
            Ui.StrokeRound(g, slot, Ui.P(10), active ? Ui.Accent : Ui.Border,
                active ? Math.Max(1.6f, Ui.P(1.8f)) : 1f);
            BlockPiece piece = game.Pieces[index];
            if (piece == null) return;

            int shapeCell = Math.Max(8, Math.Min(Ui.P(22),
                Math.Min((slot.Width - Ui.P(20)) / piece.Width,
                    (slot.Height - Ui.P(20)) / piece.Height)));
            int width = piece.Width * shapeCell;
            int height = piece.Height * shapeCell;
            int ox = slot.X + (slot.Width - width) / 2;
            int oy = slot.Y + (slot.Height - height) / 2;
            foreach (Point p in piece.Cells)
            {
                Rectangle r = new Rectangle(ox + p.X * shapeCell, oy + p.Y * shapeCell,
                    shapeCell, shapeCell);
                Rectangle inner = new Rectangle(r.X + 1, r.Y + 1,
                    Math.Max(1, r.Width - 2), Math.Max(1, r.Height - 2));
                Ui.FillRound(g, inner, Ui.P(3),
                    BlockColors[piece.ColorIndex % BlockColors.Length]);
            }
        }

        private void DrawPreview(Graphics g)
        {
            if (selected < 0 || hoverX < 0 || hoverY < 0) return;
            BlockPiece piece = game.Pieces[selected];
            if (piece == null) return;
            bool valid = game.CanPlace(piece, hoverX, hoverY);
            if (!valid) return;
            Color color = BlockColors[piece.ColorIndex % BlockColors.Length];
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(110, color)))
            {
                foreach (Point p in piece.Cells)
                {
                    int x = hoverX + p.X;
                    int y = hoverY + p.Y;
                    if (x < 0 || y < 0 || x >= BlockBlastGame.Size || y >= BlockBlastGame.Size)
                        continue;
                    Rectangle r = CellRect(x, y);
                    Rectangle inner = new Rectangle(r.X + 2, r.Y + 2,
                        Math.Max(1, r.Width - 4), Math.Max(1, r.Height - 4));
                    Ui.FillRound(g, inner, Ui.P(5), brush.Color);
                }
            }
        }
    }

    /* ------------------------------------------------------------- Выбор игры */

    internal sealed class GamesForm : ThemedForm
    {
        public GamesForm()
        {
            Text = "Игры — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            ClientSize = new Size(Ui.P(380), Ui.P(330));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Controls.Add(Lbl("Игры", Ui.H1, Ui.Text, 24, 16, 300, 30));
            Controls.Add(Lbl("Выберите игру", Ui.Small, Ui.Muted, 24, 48, 300, 18));

            AddGame("Змейка", 82, delegate
            {
                using (SnakeForm game = new SnakeForm()) ShowGame(game);
            });
            AddGame("Сапёр", 146, delegate
            {
                using (MinesweeperForm game = new MinesweeperForm()) ShowGame(game);
            });
            AddGame("Блок Бласт", 210, delegate
            {
                using (BlockBlastForm game = new BlockBlastForm()) ShowGame(game);
            });

            PillButton close = new PillButton();
            close.Ghost = true;
            close.Text = "Закрыть";
            close.Font = Ui.Small;
            close.SetBounds(Ui.P(24), Ui.P(274), Ui.P(332), Ui.P(36));
            close.Click += delegate { Close(); };
            Controls.Add(close);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyTitleBar(Handle);
        }

        private void AddGame(string title, int y, EventHandler click)
        {
            PillButton button = new PillButton();
            button.Ghost = true;
            button.Text = title;
            button.Font = Ui.Bold;
            button.SetBounds(Ui.P(24), Ui.P(y), Ui.P(332), Ui.P(52));
            button.Click += click;
            Controls.Add(button);
        }

        private void ShowGame(Form game)
        {
            Form owner = Owner;
            Hide();
            try
            {
                if (owner != null) game.ShowDialog(owner);
                else game.ShowDialog();
            }
            finally
            {
                Close();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        private static Label Lbl(string text, Font font, Color color,
            int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.UseMnemonic = false;
            label.Tag = Ui.RoleOf(color);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(width), Ui.P(height));
            return label;
        }
    }
}
