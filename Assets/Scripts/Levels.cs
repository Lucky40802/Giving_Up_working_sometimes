using System.Collections.Generic;

public class LevelData
{
    public string name;
    public string hint;
    public string[] map;
}

public static class Levels
{
    public const int COLS = 25;
    public const int ROWS = 15;

    static char[,] Base()
    {
        var g = new char[ROWS, COLS];
        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++) g[r, c] = '.';
        Fill(g, 0, 13, COLS - 1, ROWS - 1, '#');
        Fill(g, 0, 0, 0, ROWS - 1, '#');
        Fill(g, COLS - 1, 0, COLS - 1, ROWS - 1, '#');
        return g;
    }

    static void Fill(char[,] g, int c1, int r1, int c2, int r2, char ch)
    {
        for (int r = r1; r <= r2; r++)
            for (int c = c1; c <= c2; c++) g[r, c] = ch;
    }

    static LevelData Finish(string name, string hint, char[,] g)
    {
        var rows = new string[ROWS];
        for (int r = 0; r < ROWS; r++)
        {
            var sb = new System.Text.StringBuilder();
            for (int c = 0; c < COLS; c++) sb.Append(g[r, c]);
            rows[r] = sb.ToString();
        }
        return new LevelData { name = name, hint = hint, map = rows };
    }

    public static List<LevelData> Get()
    {
        var o = new List<LevelData>();
        var g = Base();
        g[12, 2] = 'P'; g[12, 22] = 'G';
        Fill(g, 12, 10, 12, 12, '#'); Fill(g, 7, 12, 8, 12, '^');
        o.Add(Finish("Step Up", "The wall is too tall. Press R to give up right where you stand.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 21] = 'G';
        Fill(g, 9, 13, 14, 13, '.'); Fill(g, 9, 14, 14, 14, '^');
        o.Add(Finish("Pit Stop", "Spikes only hurt the living.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 21] = 'G';
        Fill(g, 8, 10, 8, 12, '#'); Fill(g, 11, 12, 12, 12, '^'); Fill(g, 16, 8, 16, 12, '#');
        o.Add(Finish("Build Stairs", "Bodies stack. You can only jump about two and a half tiles.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 20] = 'G';
        Fill(g, 9, 12, 11, 12, '^'); Fill(g, 12, 9, 12, 12, '#');
        o.Add(Finish("Spiked Moat", "Fill the spikes first, then climb your own pile.", g));

        g = Base(); g[12, 2] = 'P'; g[7, 21] = 'G';
        Fill(g, 12, 12, 15, 12, '^'); Fill(g, 18, 8, 23, 12, '#');
        o.Add(Finish("The Long Way Up", "Higher than it looks. Think stairs.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 22] = 'G'; g[5, 10] = 'E';
        Fill(g, 14, 9, 14, 12, '#');
        o.Add(Finish("Bouncer", "The red ball bounces off walls and bodies. Hide behind a body.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 22] = 'G'; g[12, 8] = 'W'; g[12, 17] = 'W';
        Fill(g, 12, 10, 12, 12, '#');
        o.Add(Finish("Patrol", "Orange walkers turn at walls and ledges.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 22] = 'G'; g[6, 17] = 'C';
        Fill(g, 11, 10, 11, 12, '#'); Fill(g, 14, 12, 15, 12, '^');
        o.Add(Finish("Hunted", "The purple one hunts you when you get close. Bodies block it.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 22] = 'G'; g[5, 16] = 'B';
        Fill(g, 10, 9, 10, 12, '#');
        o.Add(Finish("Wrecking Ball", "The teal one smashes bodies. Keep it busy.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 22] = 'G'; g[8, 5] = 'K';
        Fill(g, 12, 8, 12, 12, '#');
        o.Add(Finish("Launch Pad", "The blue one will not hurt you. It will throw you.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 22] = 'G'; g[12, 4] = 'j';
        Fill(g, 7, 12, 8, 12, '^'); Fill(g, 11, 7, 11, 12, '#');
        o.Add(Finish("Spring Time", "Grab the green orb. Your next body is a jump pad. Die beside the wall.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 21] = 'G'; g[12, 5] = 't'; g[12, 18] = 'O';
        Fill(g, 12, 0, 12, 12, '#'); Fill(g, 19, 12, 20, 12, '^');
        o.Add(Finish("Wormhole", "Grab the purple orb. Your next body is a portal to the other side.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 22] = 'G'; g[3, 8] = 'E'; g[2, 17] = 'E'; g[12, 19] = 'W';
        Fill(g, 13, 9, 13, 12, '#');
        o.Add(Finish("Ballroom", "Everything here bounces. Bodies are your friends and your shields.", g));

        g = Base(); g[12, 2] = 'P'; g[12, 22] = 'G'; g[12, 3] = 'j'; g[4, 17] = 'B';
        Fill(g, 8, 12, 10, 12, '^'); Fill(g, 12, 7, 12, 12, '#');
        o.Add(Finish("Smash Hit", "The teal one breaks your pad. Be quick or be creative.", g));

        g = Base(); g[12, 2] = 'P'; g[6, 23] = 'G'; g[12, 3] = 't'; g[12, 15] = 'j'; g[12, 14] = 'O';
        Fill(g, 10, 4, 10, 12, '#'); Fill(g, 17, 12, 18, 12, '^'); Fill(g, 21, 7, 23, 12, '#');
        o.Add(Finish("Grand Finale", "Portal first, then a spring. Use everything you have.", g));
        return o;
    }
}
