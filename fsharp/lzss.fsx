// LZSS compression

open System
open System.IO
open System.Collections.Generic

let max_length = 15 + 3
let max_distance = 4095 + 1

let link_matches (data : byte[]) =
    let ndata = Array.length data
    let offsets = Array.create ndata 0
    let prev = Array.create (256 * 256) -1
    for i = 0 to ndata - 2 do
        let h = int(data.[i]) * 256 + int(data.[i+1])
        offsets.[i] <- if prev.[h] < 0 then 0 else i - prev.[h]
        prev.[h] <- i
    offsets
;;

let match_length (data : byte[]) i j =
    let ndata = Array.length data
    let mutable n = 0
    while (n < max_length) && (n + j < ndata) && (data.[n + i] = data.[n + j]) do
        n <- n + 1
    n
;;

let best_match (data : byte[]) (offsets : int[]) j =
    if offsets.[j] = 0 then
        (0, 0)
    else
        let mutable best_n = 0
        let mutable best_i = 0
        let mutable i = j - offsets.[j]
        while j - i <= max_distance do
            let n = match_length data i j
            if n > best_n then
                best_n <- n
                best_i <- i
            if offsets.[i] = 0 then
                i <- j - max_distance - 1
            else
                i <- i - offsets.[i]
        (j - best_i, best_n)
;;

let compress (data : byte[]) =
    let ndata = Array.length data
    let compressed = new MemoryStream()
    let offsets = link_matches data
    let blk = Array.create 17 0uy
    let mutable blk_i = 1
    let mutable blk_n = 0
    let mutable j = 0
    while j < ndata do
        let (d, n) = best_match data offsets j
        if n < 3 then
            blk.[0] <- blk.[0] ||| byte(0x80 >>> blk_n)
            blk.[blk_i] <- data.[j]
            blk_i <- blk_i + 1
            j <- j + 1
        else
            blk.[blk_i] <- byte(((n - 3) * 16) + ((d - 1) / 256))
            blk_i <- blk_i + 1
            blk.[blk_i] <- byte((d - 1) % 256)
            blk_i <- blk_i + 1
            j <- j + n
        blk_n <- blk_n + 1
        if blk_n = 8 then
            compressed.Write(blk, 0, blk_i)
            blk.[0] <- 0uy
            blk_i <- 1
            blk_n <- 0
    if blk_n > 0 then
        compressed.Write(blk, 0, blk_i)
    compressed.ToArray()
;;

let expand (data : byte[]) =
    let ndata = Array.length data
    let expanded = new List<byte>()
    let mutable blk = 0uy
    let mutable blk_n = 8
    let mutable j = 0
    while j < ndata do
        if blk_n = 8 then
            blk <- data.[j]
            j <- j + 1
            blk_n <- 0
        if (blk &&& (0x80uy >>> blk_n)) <> 0uy then
            expanded.Add(data.[j])
            j <- j + 1
            blk_n <- blk_n + 1
        else
            assert ((j + 1) < ndata)
            let mutable n = int(data.[j] / 16uy) + 3
            let d = (int(data.[j] &&& 15uy) * 256 + int(data.[j + 1])) + 1
            while n > 0 do
                expanded.Add(expanded.[expanded.Count - d])
                n <- n - 1
            j <- j + 2
            blk_n <- blk_n + 1
    expanded.ToArray()
;;

let args = Environment.GetCommandLineArgs()
if (args.Length < 3) then
    Console.Error.WriteLine("use: lzss [-e] <source-file> <target-file>\n\n")
    exit 1
else if (args.[1] = "-e") && (args.Length < 4) then
    Console.Error.WriteLine("use: lzss [-e] <source-file> <target-file>\n\n")
    exit 1

if args.[1] = "-e" then
    let expanded = File.ReadAllBytes(args.[2]) |> expand
    File.WriteAllBytes(args.[3], expanded)
else
    let compressed = File.ReadAllBytes(args.[1]) |> compress
    File.WriteAllBytes(args.[2], compressed)
