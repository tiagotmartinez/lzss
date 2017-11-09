program lzss;

type
    TByteArray = Array of Byte;
    TOffsetArray = Array of Longint;

const
    MaxLength = 15 + 3;
    MaxDistance = 4095 + 1;
    
procedure LinkMatches(constref data: TByteArray; var offsets: TOffsetArray);
var prev: TOffsetArray;
    h: Cardinal;
    i: Longint;
begin
    SetLength(prev, 256 * 256);
    for i := Low(prev) to High(prev) do prev[i] := -1;
    SetLength(offsets, Length(data));
    for i := Low(data) to High(data) - 1 do begin
        h := (data[i] * 256) + data[i + 1];
        if prev[h] < 0
            then offsets[i] := 0
            else offsets[i] := i - prev[h];
        prev[h] := i
    end
end;

function MatchLength(constref data: TByteArray; i, j: Longint): Longint;
var n: Longint;
begin
    n := 0;
    while (n < MaxLength) and (n + j < Length(data)) and (data[i + n] = data[j + n])
        do Inc(n);
    MatchLength := n
end;

function BestMatch(constref data: TByteArray; constref offsets: TOffsetArray; j: Longint; var len, distance: Longint): Longint;
var i, n: Longint;
begin
    len := 0;
    distance := 0;
    if offsets[j] > 0 then begin
        i := j - offsets[j];
        while j - i <= MaxDistance do begin
            n := MatchLength(data, i, j);
            if n > len then begin
                len := n;
                distance := j - i
            end;
            if offsets[i] = 0 then Break;
            Dec(i, offsets[i])
        end
    end;
    
    BestMatch := len
end;

procedure Compress(constref data: TByteArray; var compressed: TByteArray);
var blk: TByteArray;
    blkI, blkN: Longint;
    j, len, distance, ncompressed: Longint;
    offsets: TOffsetArray;
    
    procedure Flush;
    var i: Longint;
    begin
        for i := 0 to blkI - 1 do compressed[ncompressed + i] := blk[i];
        Inc(ncompressed, blkI);
        blk[0] := 0;
        blkI := 1;
        blkN := 0
    end;
    
begin
    LinkMatches(data, offsets);
    SetLength(compressed, Length(data) + (Length(data) div 8) + 8);
    ncompressed := 0;

    SetLength(blk, 17);
    blk[0] := 0;
    blkI := 1;
    blkN := 0;
    
    j := Low(data);
    while j <= High(data) do begin
        if BestMatch(data, offsets, j, len, distance) > 2 then begin
            blk[blkI] := (len - 3) * 16 + (distance - 1) div 256;
            Inc(blkI);
            blk[blkI] := (distance - 1) mod 256;
            Inc(blkI);
            Inc(j, len);
        end else begin
            blk[0] := blk[0] or ($80 shr blkN);
            blk[blkI] := data[j];
            Inc(blkI);
            Inc(j);
        end;
        
        Inc(blkN);
        if blkN = 8 then Flush
    end;
    
    if blkN > 0 then Flush;
    SetLength(compressed, ncompressed)
end;

procedure Expand(constref data: TByteArray; var expanded: TByteArray);
var blk: Byte;
    blkN, j: Longint;
    len, distance: Longint;
    nexpanded: Longint;
    
    procedure Append(b: Byte);
    begin
        if nexpanded = Length(expanded) then
            if Length(expanded) = 0
                then SetLength(expanded, 65536)
                else SetLength(expanded, Length(expanded) + Length(expanded) div 2);
        expanded[nexpanded] := b;
        Inc(nexpanded)
    end;
    
begin
    nexpanded := 0;
    blkN := 8;
    j := 0;
    while j < Length(data) do begin
        Inc(blkN);
        if blkN >= 8 then begin
            blk := data[j];
            Inc(j);
            blkN := 0
        end;
        
        if (blk and ($80 shr blkN)) = 0 then begin
            Assert(j + 1 < Length(data));
            len := (data[j] div 16) + 3;
            distance := (data[j] mod 16) * 256 + data[j + 1] + 1;
            while len > 0 do begin
                Append(expanded[nexpanded - distance]);
                Dec(len)
            end;
            Inc(j, 2)
        end else begin
            Append(data[j]);
            Inc(j)
        end
    end;
    
    SetLength(expanded, nexpanded)
end;

procedure ReadFile(constref name: string; var data: TByteArray);
var f: file of Byte;
begin
    Assign(f, name);
    Reset(f);
    SetLength(data, FileSize(f));
    BlockRead(f, data[0], Length(data));
    Close(f)
end;

procedure WriteFile(constref name: string; constref data: TByteArray);
var f: file of Byte;
begin
    Assign(f, name);
    Rewrite(f);
    BlockWrite(f, data[0], Length(data));
    Close(f)
end;

var
    Input: TByteArray;
    Output: TByteArray;

BEGIN
    if ParamCount < 2 then begin
        WriteLn('use: lzss [-e] <source-file> <target-file>');
        Halt(1)
    end else if (ParamStr(1) = '-e') and (ParamCount < 3) then begin
        WriteLn('use: lzss [-e] <source-file> <target-file>');
        Halt(1)
    end;
    
    if ParamStr(1) = '-e' then begin
        ReadFile(ParamStr(2), Input);
        Expand(Input, Output);
        WriteFile(ParamStr(3), Output);
    end else begin
        ReadFile(ParamStr(1), Input);
        Compress(Input, Output);
        WriteFile(ParamStr(2), Output);
    end
END.
