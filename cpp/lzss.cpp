// LZSS compression

#include <iostream>
#include <vector>
#include <cstdint>
#include <utility>
#include <fstream>
#include <cassert>

using namespace std;

const size_t MAX_LENGTH = 15 + 3;
const size_t MAX_DISTANCE = 4095 + 1;

using Buffer = vector<uint8_t>;

vector<int> link_matches(const Buffer & data) {
    vector<int> prev(256 * 256, -1);
    vector<int> offsets(data.size(), 0);
    for (auto i = 0u; i < data.size() - 1; i++) {
        int h = data[i] * 256 + data[i + 1];
        offsets[i] = (prev[h] >= 0) ? (i - prev[h]) : 0;
        prev[h] = i;
    }
    return offsets;
}

int match_length(const Buffer & data, int i, int j) {
    int n = 0;
    const int ndata = (int) data.size();
    while (n < MAX_LENGTH && j + n < ndata && data[i + n] == data[j + n]) n++;
    return n;
}

pair<int, int> best_match(const Buffer & data, const vector<int> & offsets, int j) {
    if (offsets[j] == 0) return { 0, 0 };
    
    pair<int, int> best { 0, 0 };
    int i = j - offsets[j];
    while (j - i <= MAX_DISTANCE) {
        auto n = match_length(data, i, j);
        if (n > best.second) best = { j - i, n };
        if (offsets[i] == 0) break;
        i -= offsets[i];
    }
    
    return best;
}

Buffer compress(const Buffer & data) {
    auto offsets = link_matches(data);
    Buffer compressed;
    
    uint8_t blk[17] = { 0 };
    int blkI = 1;
    int blkN = 0;
    
    for (auto j = 0u; j < data.size();) {
        auto m = best_match(data, offsets, j);
        if (m.second > 2) {
            blk[blkI++] = ((m.second - 3) * 16) + ((m.first - 1) / 256);
            blk[blkI++] = (m.first - 1) % 256;
            j += m.second;
        }
        else {
            blk[0] |= (0x80 >> blkN);
            blk[blkI++] = data[j++];
        }
        
        if (++blkN == 8) {
            compressed.insert(compressed.end(), blk, blk + blkI);
            blk[0] = 0;
            blkI = 1;
            blkN = 0;
        }
    }
    
    if (blkN > 0) {
        compressed.insert(compressed.end(), blk, blk + blkI);
    }
    
    return compressed;
}

Buffer expand(const Buffer & data) {
    uint8_t blk = 0;
    int blkN = 8;
    
    Buffer expanded;
    for (auto j = 0u; j < data.size();) {
        if (++blkN >= 8) {
            blk = data[j++];
            blkN = 0;
        }
        
        if (blk & (0x80 >> blkN)) {
            expanded.push_back(data[j++]);
        }
        else {
            assert(j + 1 < data.size());
            int n = (data[j] / 16) + 3;
            int d = (data[j] % 16) * 256 + data[j + 1] + 1;
            while (n-- > 0) expanded.push_back(expanded[expanded.size() - d]);
            j += 2;
        }
    }
    
    return expanded;
}

Buffer read_file(const string & name) {
    ifstream input(name, ios::binary);
    if (!input) throw runtime_error("file " + name + " not found!");
    
    input.seekg(0, ios::end);
    size_t size = (size_t) input.tellg();
    input.seekg(0);
    
    Buffer data(size);
    input.read((char *) &data[0], size);
    return data;
}

void write_file(const string & name, const Buffer & data) {
    ofstream output(name, ios::binary);
    if (!output) throw runtime_error("cannot write to " + name + "!");
    output.write((char *) &data[0], data.size());
}

int main(int argc, char *argv[])
try {
    if (argc < 3) {
        cerr << "use: lzss <source-file> <target-file>\n\n";
        return 1;
    }
    else if (strcmp(argv[1], "-e") == 0 && argc < 4) {
        cerr << "use: lzss <source-file> <target-file>\n\n";
        return 1;
    }

    if (strcmp(argv[1], "-e") == 0) {
        write_file(argv[3], expand(read_file(argv[2])));
    }
    else {
        write_file(argv[2], compress(read_file(argv[1])));
    }
}
catch (const exception & e) {
    cerr << "lzss: " << e.what() << endl;
    return 1;
}
