// LZSS compression
package main

import (
	"fmt"
	"io/ioutil"
	"os"
)

const (
	MAX_LENGTH   = 15 + 3
	MAX_DISTANCE = 4095 + 1
)

func linkMatches(data []byte) []int {
	var prev [256 * 256]int
	for i := range prev {
		prev[i] = -1
	}

	offsets := make([]int, len(data))
	for i := 0; i < len(data)-1; i++ {
		h := int(data[i])*256 + int(data[i+1])
		if prev[h] < 0 {
			offsets[i] = 0
		} else {
			offsets[i] = i - prev[h]
		}
		prev[h] = i
	}

	return offsets
}

func matchLength(data []byte, i, j int) int {
	n := 0
	for n < MAX_LENGTH && j+n < len(data) && data[i+n] == data[j+n] {
		n++
	}
	return n
}

func bestMatch(data []byte, offsets []int, j int) (distance int, length int) {
	if offsets[j] == 0 {
		return
	}

	i := j - offsets[j]
	for j-i <= MAX_DISTANCE {
		n := matchLength(data, i, j)
		if n > length {
			length = n
			distance = j - i
		}
		if offsets[i] == 0 {
			break
		}
		i -= offsets[i]
	}

	return
}

func compress(data []byte) (compressed []byte) {
	var blk [17]byte
	blkI := 1
	blkN := uint(0)

	offsets := linkMatches(data)
	for j := 0; j < len(data); {
		distance, length := bestMatch(data, offsets, j)
		if length > 2 {
			blk[blkI] = byte(((length - 3) * 16) | ((distance - 1) / 256))
			blkI++
			blk[blkI] = byte((distance - 1) % 256)
			blkI++
			j += length
		} else {
			blk[0] |= 0x80 >> blkN
			blk[blkI] = data[j]
			blkI++
			j++
		}

		blkN++
		if blkN == 8 {
			compressed = append(compressed, blk[0:blkI]...)
			blk[0] = 0
			blkI = 1
			blkN = 0
		}
	}

	if blkN > 0 {
		compressed = append(compressed, blk[0:blkI]...)
	}

	return
}

func expand(data []byte) (expanded []byte) {
	var blk byte
	blkN := uint(8)
	for j := 0; j < len(data); {
		blkN++
		if blkN >= 8 {
			blk = data[j]
			blkN = 0
			j++
		}

		if (blk & (0x80 >> blkN)) != 0 {
			expanded = append(expanded, data[j])
			j++
		} else {
			if j+1 >= len(data) {
				panic("lzss: invalid input stream")
			}
			d := int(data[j]%16)*256 + int(data[j+1]) + 1
			for n := int(data[j]/16) + 3; n > 0; n-- {
				expanded = append(expanded, expanded[len(expanded)-d])
			}
			j += 2
		}
	}

	return
}

func main() {
	if len(os.Args) < 3 {
		fmt.Fprintf(os.Stderr, "use: lzss <source-file> <target-file>\n")
		os.Exit(1)
	} else if os.Args[1] == "-e" && len(os.Args) < 4 {
		fmt.Fprintf(os.Stderr, "use: lzss <source-file> <target-file>\n")
		os.Exit(1)
	}

	if os.Args[1] == "-e" {
		data, err := ioutil.ReadFile(os.Args[2])
		if err != nil {
			fmt.Fprintf(os.Stderr, "lzss: %s\n", err)
			os.Exit(1)
		}

		err = ioutil.WriteFile(os.Args[3], expand(data), 0)
		if err != nil {
			fmt.Fprintf(os.Stderr, "lzss: %s\n", err)
			os.Exit(1)
		}
	} else {
		data, err := ioutil.ReadFile(os.Args[1])
		if err != nil {
			fmt.Fprintf(os.Stderr, "lzss: %s\n", err)
			os.Exit(1)
		}

		err = ioutil.WriteFile(os.Args[2], compress(data), 0)
		if err != nil {
			fmt.Fprintf(os.Stderr, "lzss: %s\n", err)
			os.Exit(1)
		}
	}
}
