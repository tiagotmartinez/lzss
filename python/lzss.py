'''LZSS compression'''

import sys

MAX_DISTANCE = 4095 + 1
MAX_LENGTH = 15 + 3

def _link_matches(data):
    offsets = [0] * len(data)
    prev = [-1] * (256 * 256)
    for i in range(len(data) - 1):
        h = data[i] * 256 + data[i + 1]
        offsets[i] = i - prev[h] if prev[h] >= 0 else 0
        prev[h] = i
    return offsets
    
def _best_match(data, offsets, j):
    if offsets[j] == 0: return (0, 0)
    best = (0, 0)
    ndata = len(data)
    i = j - offsets[j]
    while j - i <= MAX_DISTANCE:
        n = 0
        while n < MAX_LENGTH and j + n < ndata and data[i + n] == data[j + n]: n += 1
        if n > best[1]:
            best = (j - i, n)
            if best[1] == MAX_LENGTH: break
        if offsets[i] == 0: break
        i -= offsets[i]
    return best

def compress(data):
    offsets = _link_matches(data)
    compressed = bytearray()
    blk = bytearray(1)
    blk_n = 0
    j = 0
    ndata = len(data)
    while j < ndata:
        (d, n) = _best_match(data, offsets, j)
        if n > 2:
            blk.append((n - 3) * 16 + (d - 1) // 256)
            blk.append((d - 1) % 256)
            j += n
        else:
            blk[0] |= 0x80 >> blk_n
            blk.append(data[j])
            j += 1
        blk_n += 1
        if blk_n == 8:
            compressed.extend(blk)
            blk = bytearray(1)
            blk_n = 0
    if blk_n > 0: compressed.extend(blk)
    return bytes(compressed)
    
if __name__ == '__main__':
    if len(sys.argv) < 3:
        print('use: lzss <source-file> <target-file>')
        exit(1)
    elif sys.argv[1] == '-e' and len(sys.argv) < 4:
        print('use: lzss <source-file> <target-file>')
        exit(1)
    
    if sys.argv[1] == '-e':
        pass
    else:
        with open(sys.argv[1], 'rb') as input:
            compressed = compress(input.read())
            with open(sys.argv[2], 'wb') as output:
                output.write(compressed)