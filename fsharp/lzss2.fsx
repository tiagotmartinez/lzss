open System
open System.IO
open System.Collections.Generic

let max_length = 15 + 3
let max_distance = 4095 + 1

let link_matches (data : byte[]) =
    let ndata = Array.length data
    let offsets = Array.create ndata 0
    let prev = Array.create (256 * 256) -1
    let rec loop i =
        if i < ndata - 1 then
            let h = int(data.[i]) * 256 + int(data.[i + 1])
            offsets.[i] <- if prev.[h] < 0 then 0 else i - prev.[h]
            prev.[h] <- i
            loop (i + 1)
        else
            offsets
    loop 0
;;

let match_length (data : byte[]) i j =
    let ndata = Array.length data
    let rec loop n =
        if (n < max_length) && (n + j < ndata) && (data.[n + i] = data.[n + j]) then
            loop (n + 1)
        else
            n
    loop 0
;;

let best_match (data : byte[]) (offsets : int[]) j =
    let rec loop (bestd, bestn) i =
        if (j - i > max_distance) then
            (bestd, bestn)
        else
            let n = match_length data i j
            let (bestd, bestn) = if n > bestn then (j - i, n) else (bestd, bestn)
            if offsets.[i] = 0
                then (bestd, bestn)
                else loop (bestd, bestn) (i - offsets.[i])
    if offsets.[j] = 0
        then (0, 0)
        else loop (0, 0) (j - offsets.[j])
;;

let compress (data : byte[]) =
    let ndata = Array.length data
    let offsets = link_matches data
    let compressed = new MemoryStream()
    let blk = Array.create 17 0uy
    let flush blk_i =
        compressed.Write(blk, 0, blk_i)
        blk.[0] <- 0uy
        1
    let rec loop j blk_i blk_n =
        let next j blk_i =
            if (blk_n + 1) >= 8
                then loop j (flush blk_i) 0
                else loop j blk_i (blk_n + 1)
        if j < ndata then
            let (d, n) = best_match data offsets j
            if n > 2 then
                blk.[blk_i] <- byte(((n - 3) * 16) + ((d - 1) / 256))
                blk.[blk_i + 1] <- byte((d - 1) % 256)
                next (j + n) (blk_i + 2)
            else
                blk.[0] <- blk.[0] ||| byte(0x80 >>> blk_n)
                blk.[blk_i] <- data.[j]
                next (j + 1) (blk_i + 1)
        else
            if blk_n > 0 then flush blk_i |> ignore
            compressed.ToArray()
    loop 0 1 0
;;

let expand (data : byte[]) =
    let ndata = Array.length data
    let expanded = new List<byte>()
    let rec loop j blk blk_n =
        let rec copy n d =
            if n > 0 then
                assert(d > 0)
                assert(d <= expanded.Count)
                expanded.Add(expanded.[expanded.Count - d])
                copy (n - 1) d
        if j >= ndata then
            expanded.ToArray()
        else
            if (blk &&& (0x80uy >>> blk_n)) <> 0uy then
                expanded.Add(data.[j])
                next (j + 1) blk blk_n
            else
                assert ((j + 1) < ndata)
                let n = int(data.[j] / 16uy) + 3
                let d = (int(data.[j] &&& 15uy) * 256 + int(data.[j + 1])) + 1
                copy n d
                next (j + 2) blk blk_n
    and next j blk blk_n =
        if (blk_n + 1) >= 8 then
            loop (j + 1) data.[j] 0
        else
            loop j blk (blk_n + 1)
    next 0 0uy 8

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
