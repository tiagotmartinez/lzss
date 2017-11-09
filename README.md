# README #

A collection of implementations of the [LZSS][1] compression algorithm in multiple languages.

This is *not* meant as a comparison of either code size / easy of implementation or performance, it is only a learning experiment to play with various languages.

Each implementation is in its own directory, and has its own compilation / execution details (in most cases there is no need for dependencies and/or extra set-up).

### The LZSS Algorithm ###

[LZSS][1] is a version of [LZ77][2]:

>LZ77 algorithms achieve compression by replacing repeated occurrences of data with references to a single copy of that data existing earlier in the uncompressed data stream. A match is encoded by a pair of numbers called a length-distance pair, which is equivalent to the statement "each of the next length characters is equal to the characters exactly distance characters behind it in the uncompressed stream". (The "distance" is sometimes called the "offset" instead.)

All implementations limit the offset to 4KiB and the length to 18 bytes, this makes search faster, and allow copy codes to fit in 16-bits.  All implementations also use the same data structures to speed match searching.  A C-like pseudo-code listing of the relevant parts is given below.

```c
enum {
	MAX_DISTANCE = 4095 + 1,	/* (distance - 1) can be encoded in 12 bits */
	MAX_LENGTH = 15 + 3,		/* (length - 3) can be encoded in 4 bits */
}
```

First, an `offsets` array of `unsigned int` (or equivalent) with the same size as the input `data` stream is computed so that `hash(data[i - offsets[i]]) == hash(data[i])`, where `hash` is defined as `hash(i) = data[i] * 256 + data[i + 1]`.  If no previous match is found, `offsets[i] == 0`.

```c
void link_matches(const unsigned char *data, int ndata, unsigned int *offsets) {
	int previous[256 * 256];
	for (int i = 0; i < 256 * 256; i++) previous[i] = -1;
	for (int i = 0; i < ndata - 1; i++) {
		unsigned int h = data[i] * 256 + data[i + 1];
		offsets[i] = (previous[h] <= 0) ? 0 : i - previous[h];
		previous[h] = i;
	}
}
```

For each `data[j]`, scan all previous occurrences of the same 2 bytes for the longest match.  `offsets` is a linked-list of those potential matches.

```c
int best_match(const unsigned char *data, int ndata, const unsigned int *offsets, int j, int *bestN, int *bestD) {
	if (offsets[j] == 0) return 0;	/* no previous position with same hash */
	int i = j - offsets[j];
	while (j - i <= MAX_DISTANCE) {  /* while distance <= MAX_DISTANCE */
		int n = 2;  /* compute match length; it is at least 2 */
		while (n < MAX_LENGTH && j + n < ndata && data[i + n] == data[j + n]) n++;
		if (n > *bestN) { *bestN = n; *bestD = j - i; }
		if (offsets[i] == 0) break;  /* end of linked list */
		i -= offsets[i];
	}
	return *bestN;
}
```

Compression itself is straightforward.  For each position `i` find the `best_match`, if it is longer than 2 (the size of a *copy* code), output a *copy*, otherwise output a *literal*.

```c
void compress(const unsigned char *data, int ndata) {
	unsigned int offsets[ndata] = {0};
	link_matches(data, ndata, offsets);
	for (int j = 0; j < ndata;) {
		int bestN, bestD;
		if (best_match(data, ndata, offsets, j, &bestN, &bestD) > 2) {
			output_copy(bestN, bestD);
			j += bestN;
		} else {
			output_literal(data[j++]);
		}
	}
}
```

Not all implementations are exactly the same, but most include `link_matches`. `best_match` and `compress` functions.  Also, all implementations define a `expand` function that reverse compression.

### Compressed Stream Format ###

The compressed stream is broken in *blocks* which are sequences of exactly 8 *codes*.  Each code is either a *copy* (length-distance pair) or a *literal* (a single byte directly from input `data`).  Each block starts with a single byte *header* field that works as a directory for what is a copy and what is a literal in the block.  If the code index `n` (range 0..7) in the block is a literal, the same bit on the header field will be non-zero (from left to right).  In code: `is_literal(n) = ((header & (0x80 >> n)) != 0)`.

In general, the implementations cache the current block in a 17-byte `blk` array (1 byte header + at most 2 byte * 8 codes) and when 8 codes have been written to this array, it is flushed to a dynamically allocated `compressed` array.  In some cases the block could be written directly to the `compressed` array, but the implementation is cleaner this way.

No meta-data or error checking information is generated, this is not meant as a practical compression / archiver tool!



[1]: https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Storer%E2%80%93Szymanski
[2]: https://en.wikipedia.org/wiki/LZ77_and_LZ78#LZ77
[3]: https://en.wikipedia.org/wiki/Entropy_encoding
