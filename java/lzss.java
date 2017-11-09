import java.io.*;
import java.util.*;

public class lzss {
    
    static final int MAX_LENGTH = 15 + 3;
    static final int MAX_DISTANCE = 4095 + 1;
    
    static int[] linkMatches(final byte[] data) {
        int[] prev = new int [256 * 256];
        for (int i = 0; i < prev.length; i++) prev[i] = -1;
        
        int[] offsets = new int [data.length];
        for (int i = 0; i < data.length - 1; i++) {
            final int h = ((int)data[i] & 0xFF) * 256 + ((int)data[i + 1] & 0xFF);
            offsets[i] = (prev[h] >= 0) ? (i - prev[h]) : 0;
            prev[h] = i;
        }
        
        return offsets;
    }
    
    static int matchLength(final byte[] data, int i, int j) {
        int n = 0;
        while (n < MAX_LENGTH && n + j < data.length && data[i + n] == data[j + n]) n++;
        return n;
    }
    
    static int[] bestMatch(final byte[] data, final int[] offsets, int j) {
        if (offsets[j] == 0) return new int[] { 0, 0 };
        
        int bestN = 0;
        int bestD = 0;
        int i = j - offsets[j];
        while (j - i <= MAX_DISTANCE) {
            final int n = matchLength(data, i, j);
            if (n > bestN) {
                bestN = n;
                bestD = j - i;
            }
            if (offsets[i] == 0) break;
            i -= offsets[i];
        }
        
        return new int[] { bestD, bestN };
    }
    
    static byte[] compress(final byte[] data) {
        final int[] offsets = linkMatches(data);
        final ByteArrayOutputStream compressed = new ByteArrayOutputStream();
        
        final byte[] blk = new byte [17];
        int blkI = 1;
        int blkN = 0;
        
        for (int j = 0; j < data.length;) {
            final int[] m = bestMatch(data, offsets, j);
            if (m[1] > 2) {
                blk[blkI++] = (byte)(((m[1] - 3) * 16) | ((m[0] - 1) / 256));
                blk[blkI++] = (byte)((m[0] - 1) % 256);
                j += m[1];
            }
            else {
                blk[0] |= (0x80 >> blkN);
                blk[blkI++] = data[j++];
            }
            
            if (++blkN == 8) {
                compressed.write(blk, 0, blkI);
                blk[0] = 0;
                blkI = 1;
                blkN = 0;
            }
        }
        
        if (blkN > 0) compressed.write(blk, 0, blkI);
        return compressed.toByteArray();
    }

    static byte[] expand(final byte[] data) {
        byte[] expanded = new byte [65536];
        int nexpanded = 0;
        
        byte blk = 0;
        int blkN = 8;
        
        for (int j = 0; j < data.length;) {
            if (++blkN >= 8) {
                blk = data[j++];
                blkN = 0;
            }

            if ((blk & (0x80 >> blkN)) == 0) {
                assert(j + 1 < data.length);
                int n = (((int)data[j] & 0xF0) / 16) + 3;
                int d = ((int)data[j] & 0x0F) * 256 + ((int)data[j + 1] & 0xFF) + 1;
                if (nexpanded + n > expanded.length)
                    expanded = Arrays.copyOf(expanded, expanded.length + expanded.length / 2);
                while (n-- > 0) {
                    expanded[nexpanded] = expanded[nexpanded - d];
                    nexpanded++;
                }
                j += 2;
            }
            else {
                if (expanded.length == nexpanded)
                    expanded = Arrays.copyOf(expanded, expanded.length + expanded.length / 2);
                expanded[nexpanded++] = data[j++];
            }
        }
        
        return Arrays.copyOf(expanded, nexpanded);
    }
    
    static byte[] read(String name) throws IOException {
        FileInputStream f = new FileInputStream(name);
        try {
            byte[] contents = new byte [f.available()];
            f.read(contents);
            return contents;
        }
        finally {
            f.close();
        }
    }
    
    static void write(String name, final byte[] contents) throws IOException {
        FileOutputStream f = new FileOutputStream(name);
        try {
            f.write(contents, 0, contents.length);
        }
        finally {
            f.close();
        }
    }
    
    public static void main(String[] args) {
        try {
            if (args.length < 2) {
                System.err.println("use: lzss [-e] <source-file> <target-file>");
                System.exit(1);
            }
            else if (args[0].equals("-e") && args.length < 3) {
                System.err.println("use: lzss [-e] <source-file> <target-file>");
                System.exit(1);
            }

            if (args[0].equals("-e"))
                write(args[2], expand(read(args[1])));
            else
                write(args[1], compress(read(args[0])));
        }
        catch (Exception e) {
            System.err.println("lzss: " + e.getMessage());
            // e.printStackTrace();
            System.exit(1);
        }
    }
}
