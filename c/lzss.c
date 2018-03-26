// LZSS compression

#define _CRT_SECURE_NO_WARNINGS

#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

enum {
    MAX_LENGTH = 15 + 3,
    MAX_DISTANCE = 4095 + 1
};

void link_matches(const uint8_t *data, size_t ndata, ptrdiff_t *offsets) {
    assert(data);
    assert(offsets);
    
    ptrdiff_t prev[256 * 256];
    for (size_t i = 0; i < 256 * 256; i++) prev[i] = -1;
    for (size_t i = 0; i < ndata - 1; i++) {
        uint32_t h = data[i] * 256 + data[i + 1];
        offsets[i] = (prev[h] >= 0) ? (i - prev[h]) : 0;
        prev[h] = i;
    }
}

size_t match_length(const uint8_t *data, size_t ndata, size_t i, size_t j) {
    size_t n = 0;
    while (n < MAX_LENGTH && j + n < ndata && data[i + n] == data[j + n]) n++;
    return n;
}

size_t best_match(const uint8_t *data, size_t ndata, const ptrdiff_t *offsets, size_t j, ptrdiff_t *offset, size_t *length) {
    *offset = 0;
    *length = 0;
    
    if (offsets[j] == 0) return 0;
    
    for (size_t i = j - offsets[j]; j - i <= MAX_DISTANCE; i -= offsets[i]) {
        const size_t n = match_length(data, ndata, i, j);
        if (n > *length) {
            *length = n;
            *offset = j - i;
        }
        
        if (offsets[i] == 0) break;
    }
    
    return *length;
}

size_t compress(const uint8_t *data, size_t ndata, FILE *output) {
    ptrdiff_t *offsets = calloc(ndata, sizeof(ptrdiff_t));
    link_matches(data, ndata, offsets);
    
    size_t noutput = 0;
    uint8_t blk[17] = { 0 };
    size_t blkI = 1, blkN = 0;
    
    for (size_t j = 0; j < ndata;) {
        size_t length;
        ptrdiff_t offset;
        if (best_match(data, ndata, offsets, j, &offset, &length) >= 3) {
            blk[blkI++] = (uint8_t) (((length - 3) * 16) + ((offset - 1) / 256));
            blk[blkI++] = (uint8_t) ((offset - 1) % 256);
            j += length;
        } else {
            blk[0] |= 0x80 >> blkN;
            blk[blkI++] = data[j++];
        }
        
        if (++blkN == 8) {
            noutput += fwrite(blk, 1, blkI, output);
            blk[0] = 0;
            blkI = 1;
            blkN = 0;
        }
    }
    
    if (blkN != 0) {
        noutput += fwrite(blk, 1, blkI, output);
    }
    
    return noutput;
}

uint8_t *read_file(const char *name, size_t *size) {
    FILE *f = fopen(name, "rb");
    if (!f) return NULL;
    
    fseek(f, 0, SEEK_END);
    *size = ftell(f);
    fseek(f, 0, SEEK_SET);
    
    uint8_t *data = calloc(*size, 1);
    assert(data);
    
    fread(data, 1, *size, f);
    fclose(f);
    
    return data;
}

int main(int argc, char *argv[]) {
    if (argc < 3) {
        fprintf(stderr, "use: lzss [-e] <source-file> <target-file>\n\n");
        return 1;
    }
    
    size_t ndata = 0;
    uint8_t *data = read_file(argv[1], &ndata);
    if (!data) {
        fprintf(stderr, "lzss: error reading \"%s\".\n", argv[1]);
        return 2;
    }
    
    FILE *output = fopen(argv[2], "wb");
    if (!output) {
        fprintf(stderr, "lzss: error creating \"%s\".\n", argv[2]);
        return 3;
    }
    
    compress(data, ndata, output);
    
    fclose(output);
    free(data);
    
    return EXIT_SUCCESS;
}
