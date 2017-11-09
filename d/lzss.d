// LZSS compression

import std.array;
import std.file;
import std.stdio;
import std.typecons;

enum MAX_LENGTH = 15 + 3;
enum MAX_DISTANCE = 4095 + 1;

private auto link_matches(const ubyte[] data) {
    auto prev = uninitializedArray!(int[])(256 * 256);
    prev[] = -1;
    
    auto offsets = new int [data.length];
    for (auto i = 0; i < data.length - 1; i++) {
        uint h = (data[i] * 256) + data[i + 1];
        offsets[i] = (prev[h] >= 0) ? (i - prev[h]) : 0;
        prev[h] = i;
    }
    
    return offsets;
}

private auto match_length(const ubyte[] data, int i, int j) {
    int n = 0;
    while (n < MAX_LENGTH && j + n < data.length && data[i + n] == data[j + n]) n++;
    return n;
}

private auto best_match(const ubyte[] data, const int[] offsets, int j) {
    if (offsets[j] == 0) return tuple(0, 0);
    
    int bestN = 0;
    int bestD = 0;
    int i = j - offsets[j];
    while (j - i <= MAX_DISTANCE) {
        auto n = match_length(data, i, j);
        if (n > bestN) {
            bestN = n;
            bestD = j - i;
        }
        
        if (offsets[i] == 0) break;
        i -= offsets[i];
    }
    
    return tuple(bestD, bestN);
}

ubyte[] compress(const ubyte[] data) {
    auto compressed = appender!(ubyte[])();

    ubyte[17] blk;
    int blkI = 1;
    int blkN = 0;
    
    const offsets = link_matches(data);
    for (int j = 0; j < data.length;) {
        immutable m = best_match(data, offsets, j);
        if (m[1] > 2) {
            blk[blkI++] = cast(ubyte)(((m[1] - 3) * 16) | (m[0] - 1) / 256);
            blk[blkI++] = cast(ubyte)((m[0] - 1) % 256);
            j += m[1];
        }
        else {
            blk[0] |= 0x80 >> blkN;
            blk[blkI++] = data[j++];
        }
        
        if (++blkN == 8) {
            compressed.put(blk[0 .. blkI]);
            blk[0] = 0;
            blkI = 1;
            blkN = 0;
        }
    }
    
    if (blkN > 0) {
        compressed.put(blk[0 .. blkI]);
    }
    
    return compressed.data;
}

ubyte[] expand(const ubyte[] data) {
    auto expanded = appender!(ubyte[])();
    
    ubyte blk = 0;
    int blkN = 8;
    
    for (int j = 0; j < data.length;) {
        if (++blkN >= 8) {
            blk = data[j++];
            blkN = 0;
        }
        
        if ((blk & (0x80 >> blkN)) == 0) {
            assert( j + 1 < data.length );
            uint n = (data[j] / 16) + 3;
            uint d = ((data[j] & 0xF) * 256) + data[j + 1] + 1;
            while (n-- > 0) expanded.put(expanded.data[$ - d]);
            j += 2;
        }
        else {
            expanded.put(data[j++]);
        }
    }
    
    return expanded.data;
}

int main(string[] args) {
    if (args.length < 3) {
        writefln("use: lzss [-e] <source-file> <target-file>\n");
        return 1;
    }
    else if (args[1] == "-e" && args.length < 4) {
        writefln("use: lzss [-e] <source-file> <target-file>\n");
        return 1;
    }

    if (args[1] == "-e") {
        std.file.write(args[3], expand(cast(ubyte[]) std.file.read(args[2])));
    }
    else {
        std.file.write(args[2], compress(cast(ubyte[]) std.file.read(args[1])));
    }
    
    return 0;
}
