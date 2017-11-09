using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

class Lzss {

    const int MAX_LENGTH = 15 + 3;
    const int MAX_DISTANCE = 4095 + 1;

    static int[] LinkMatches(byte[] data) {
        var prev = new int [256 * 256];
        for (var i = 0; i < prev.Length; i++) prev[i] = -1;
        
        var offsets = new int [data.Length];
        for (var i = 0; i < data.Length - 1; i++) {
            int h = data[i] * 256 + data[i + 1];
            offsets[i] = (prev[h] <= 0) ? 0 : i - prev[h];
            prev[h] = i;
        }
        
        return offsets;
    }

    static int MatchLength(byte[] data, int i, int j) {
        int n = 0;
        while (n < MAX_LENGTH && j + n < data.Length && data[i + n] == data[j + n]) n++;
        return n;
    }
    
    static Tuple<int, int> BestMatch(byte[] data, int[] offsets, int j) {
        var best = Tuple.Create(0, 0);
        if (offsets[j] == 0) return best;
        
        var i = j - offsets[j];
        while (j - i <= MAX_DISTANCE) {
            var n = MatchLength(data, i, j);
            if (n > best.Item2) best = Tuple.Create(j - i, n);
            if (offsets[i] == 0) break;
            i -= offsets[i];
        }
        
        return best;
    }
    
    static byte[] Compress(byte[] data) {
        var offsets = LinkMatches(data);
        
        var blk = new byte [17];
        var blkI = 1;
        var blkN = 0;
        
        var compressed = new List<byte>();
        for (int j = 0; j < data.Length;) {
            var m = BestMatch(data, offsets, j);
            if (m.Item2 > 2) {
                blk[blkI++] = (byte)((m.Item2 - 3) * 16 + (m.Item1 - 1) / 256);
                blk[blkI++] = (byte)((m.Item1 - 1) % 256);
                j += m.Item2;
            }
            else {
                blk[0] |= (byte)(0x80 >> blkN);
                blk[blkI++] = data[j++];
            }
            
            if (++blkN == 8) {
                compressed.AddRange(blk.Take(blkI));
                blk[0] = 0;
                blkI = 1;
                blkN = 0;
            }
        }

        if (blkN > 0) compressed.AddRange(blk.Take(blkI));
        return compressed.ToArray();
    }

    static byte[] Expand(byte[] data) {
        var blk = 0;
        var blkN = 8;
        
        var expanded = new List<byte>();
        for (int j = 0; j < data.Length;) {
            if (++blkN >= 8) {
                blk = data[j++];
                blkN = 0;
            }
            
            if ((blk & (0x80 >> blkN)) != 0) {
                expanded.Add(data[j++]);
            }
            else {
                var n = (data[j] >> 4) + 3;
                var d = ((data[j] & 0xF) << 8) + data[j + 1] + 1;
                while (n-- > 0) expanded.Add(expanded[expanded.Count - d]);
                j += 2;
            }
        }
        
        return expanded.ToArray();
    }
    
    public static int Main(string[] args) {
        if (args.Length < 2) {
            Console.Error.WriteLine("use: lzss [-e] <source-file> <target-file>");
            return 1;
        }
        else if (args[0] == "-e" && args.Length < 3) {
            Console.Error.WriteLine("use: lzss [-e] <source-file> <target-file>");
            return 1;
        }
        
        try {
            if (args[0] == "-e")
                File.WriteAllBytes(args[2], Expand(File.ReadAllBytes(args[1])));
            else
                File.WriteAllBytes(args[1], Compress(File.ReadAllBytes(args[0])));
        }
        catch (Exception e) {
            Console.Error.WriteLine("lzss: " + e);
            return 1;
        }
        
        return 0;
    }
    
}
