// LZSS compression

import java.io.ByteArrayOutputStream
import java.io.File
import kotlin.system.exitProcess

const val MAX_DISTANCE = 4095 + 1
const val MAX_LENGTH = 15 + 3

fun linkMatches(data : ByteArray) : IntArray {
    val prev = IntArray(256 * 256) { -1 }
    val offsets = IntArray(data.size) { 0 }
    for (i in 0 until data.size - 1) {
        val h = (data[i].toInt() and 0xFF) * 256 + (data[i + 1].toInt() and 0xFF)
        offsets[i] = if (prev[h] < 0) 0 else i - prev[h]
        prev[h] = i
    }
    return offsets;
}

fun bestMatch(data: ByteArray, offsets: IntArray, j: Int): Pair<Int, Int> {
    var best = Pair(0, 0)
    if (offsets[j] == 0) return best

    var i = j - offsets[j]
    while (j - i <= MAX_DISTANCE) {
        var n = 2
        while (j + n < data.size && data[i + n] == data[j + n] && n < MAX_LENGTH) n += 1
        if (n > best.second) best = Pair(j - i, n)
        if (offsets[i] == 0) break
        i -= offsets[i]
    }

    return best
}

fun compress(data: ByteArray): ByteArray {
    val compressed = ByteArrayOutputStream()

    var blk = ByteArray(17)
    var blkI = 1
    var blkN = 0

    val offsets = linkMatches(data)
    var j = 0
    while (j < data.size) {
        val (d, n) = bestMatch(data, offsets, j)
        if (n > 2) {
            blk[blkI++] = (((n - 3) shl 4) + (d - 1) / 256).toByte()
            blk[blkI++] = ((d - 1) % 256).toByte()
            j += n
        }
        else {
            blk[0] = (blk[0].toInt() or (0x80 shr blkN)).toByte()
            blk[blkI++] = data[j++]
        }

        if (++blkN == 8) {
            compressed.write(blk, 0, blkI)
            blk[0] = 0
            blkI = 1
            blkN = 0
        }
    }

    if (blkN > 0) compressed.write(blk, 0, blkI)
    return compressed.toByteArray()
}

fun expand(data: ByteArray): ByteArray {
    var expanded = ByteArray(65536)
    var expandedN = 0

    var blk = 0
    var blkN = 8

    var j = 0
    while (j < data.size) {
        if (++blkN >= 8) {
            blk = data[j++].toInt() and 0xFF
            blkN = 0
        }

        if ((blk and (0x80 shr blkN)) == 0) {
            var n = (data[j].toInt() and 0xF0) / 16 + 3
            val d = (data[j].toInt() and 0x0F) * 256 + (data[j + 1].toInt() and 0xFF) + 1
            if (expandedN + n > expanded.size)
                expanded = expanded.copyOf(expanded.size + expanded.size / 2)
            while (n-- > 0) {
                expanded[expandedN] = expanded[expandedN - d]
                expandedN ++
            }
            j += 2
        }
        else {
            if (expanded.size == expandedN)
                expanded = expanded.copyOf(expanded.size + expanded.size / 2)
            expanded[expandedN++] = data[j++]
        }
    }

    return expanded.copyOf(expandedN)
}

fun readFile(name: String): ByteArray {
    return File(name).readBytes()
}

fun writeFile(name: String, data: ByteArray) {
    File(name).writeBytes(data)
}

fun main(args: Array<String>) {
    if (args.size < 2) {
        println("use: lzss <source-file> <target-file>")
        exitProcess(1)
    }
    else if (args[0] == "-e" && args.size < 3) {
        println("use: lzss <source-file> <target-file>")
        exitProcess(1)
    }

    if (args[0] == "-e") {
        writeFile(args[2], expand(readFile(args[1])))
    }
    else {
        writeFile(args[1], compress(readFile(args[0])))
    }
}