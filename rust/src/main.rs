use std::io::Result;
use std::io::prelude::*;
use std::fs::File;
use std::path::Path;
use std::env;
use std::process::exit;

const MAX_LENGTH: usize = 15 + 3;
const MAX_DISTANCE: usize = 4095 + 1;

fn link_matches(data: &[u8]) -> Vec<usize> {
    let mut prev : Vec<i32> = vec![-1; 256 * 256];
    let mut offsets = vec![0usize; data.len()];
    for j in 0 .. data.len() - 1 {
        let h = (data[j] as usize) * 256 + (data[j + 1] as usize);
        offsets[j] = if prev[h] <= 0 { 0 } else { j - prev[h] as usize };
        prev[h] = j as i32
    }
    offsets
}

fn match_length(data: &[u8], i: usize, j: usize) -> usize {
    let mut n = 0usize;
    while n < MAX_LENGTH && j + n < data.len() && data[i + n] == data[j + n] { n += 1 }
    n
}

fn best_match(data: &[u8], offsets: &[usize], j: usize) -> (usize, usize) {
    if offsets[j] == 0 {
        (0, 0)
    } else {
        let mut i = j - offsets[j];
        let mut best = (0usize, 0usize);
        while j - i <= MAX_DISTANCE {
            let n = match_length(data, i, j);
            if n > best.1 {
                best = (j - i, n)
            }
            if offsets[i] == 0 {
                break
            }
            i -= offsets[i]
        }
        best
    }
}

fn compress(data: &[u8]) -> Vec<u8> {
    let offsets = link_matches(data);
    let mut compressed = Vec::new();
    
    let mut blk = vec![0u8; 17];
    let mut blk_i = 1;
    let mut blk_n = 0;
    
    let mut j = 0usize;
    while j < data.len() {
        let (distance, length) = best_match(data, &offsets, j);
        if length > 2 {
            blk[blk_i] = ((length - 3) * 16 | (distance - 1) / 256) as u8;
            blk_i += 1;
            blk[blk_i] = ((distance - 1) % 256) as u8;
            blk_i += 1;
            j += length;
        } else {
            blk[0] = blk[0] | (0x80u8 >> blk_n);
            blk[blk_i] = data[j];
            blk_i += 1;
            j += 1;
        }
        
        blk_n += 1;
        if blk_n == 8 {
            compressed.extend_from_slice(&blk[0 .. blk_i]);
            blk[0] = 0;
            blk_i = 1;
            blk_n = 0;
        }
    }
    
    if blk_n > 0 {
        compressed.extend_from_slice(&blk[0 .. blk_i]);
    }
    
    compressed
}

fn expand(data: &[u8]) -> Vec<u8> {
    let mut expanded = Vec::new();

    let mut blk = 0u8;
    let mut blk_n = 8usize;
    let mut j = 0usize;
    while j < data.len() {
        blk_n += 1;
        if blk_n >= 8 {
            blk = data[j];
            j += 1;
            blk_n = 0;
        }
        
        if (blk & (0x80u8 >> blk_n)) != 0 {
            expanded.push(data[j]);
            j += 1
        } else {
            assert!(j + 1 < data.len());
            let n = (data[j] / 16 + 3) as usize;
            let d = ((data[j] % 16) as usize * 256) + (data[j + 1] as usize) + 1;
            for _ in 0 .. n {
                let b = expanded[expanded.len() - d];
                expanded.push(b)
            }
            j += 2
        }
    }
    
    expanded
}

fn read_all<P: AsRef<Path>>(path: P) -> Result<Vec<u8>> {
    let mut f = File::open(path)?;
    let mut buffer = Vec::new();
    f.read_to_end(&mut buffer)?;
    Ok(buffer)
}

fn write_all<P: AsRef<Path>>(path: P, data: &[u8]) -> Result<()> {
    let mut f = File::create(path)?;
    f.write_all(data)?;
    Ok(())
}

fn main() {
    if env::args().len() < 3 {
        println!("use: lzss [-e] <source-file> <target-file>");
        exit(1);
    } else if env::args().nth(1).unwrap() == "-e" && env::args().len() < 4 {
        println!("use: lzss [-e] <source-file> <target-file>");
        exit(1);
    }
    
    if env::args().nth(1).unwrap() == "-e" {
        let source_file = env::args().nth(2).unwrap();
        let target_file = env::args().nth(3).unwrap();
        let data = match read_all(&source_file) {
            Ok(data) => data,
            Err(s) => panic!("lzss: {}", s)
        };
        let expanded = expand(&data);
        match write_all(target_file, &expanded) {
            Ok(_) => (),
            Err(s) => panic!("lzss: {}", s)
        }
    } else {
        let source_file = env::args().nth(1).unwrap();
        let target_file = env::args().nth(2).unwrap();
        let data = match read_all(&source_file) {
            Ok(data) => data,
            Err(s) => panic!("lzss: {}", s)
        };
        let compressed = compress(&data);
        match write_all(target_file, &compressed) {
            Ok(_) => (),
            Err(s) => panic!("lzss: {}", s)
        }
    }
}
