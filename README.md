Compressor benchmark
====================

This is a tiny benchmark program I wrote a couple years ago for my personal use.
It generates a few data files and feeds them to different compression libraries
to measure compression ratio and speed.

Data files generated:
* bench1.dat — binary, long running lengths
* bench2.dat — text, generated using Markov chains
* bench3.dat — random uncompressible data

Results
=======
```
Cpu: Intel(R) Core(TM) i5 CPU M 430 @ 2.27GHz
Operating System: Microsoft Windows NT 6.1.7601 Service Pack 1
Running in 64-bit process: yes

Generating test data...
Binary: 111190 bytes.
Textual: 100000 bytes.
Uncompressible: 100066 bytes.

Benchmark name: LZF, binary data
Compression:    size: 111190 ->  74333, elapsed: 00:00:03.6844055, speed: 30,179 mb/s
Decompression:  size:  74333 -> 111190, elapsed: 00:00:01.7519817, speed: 42,428 mb/s

Benchmark name: LZ4, binary data
Compression:    size: 111190 ->  70130, elapsed: 00:00:01.7417989, speed: 63,836 mb/s
Decompression:  size:  70130 -> 111190, elapsed: 00:00:00.8299794, speed: 84,496 mb/s

Benchmark name: QuickLZ, binary data
Compression:    size: 111190 ->  85842, elapsed: 00:00:03.4959857, speed: 31,805 mb/s
Decompression:  size:  85842 -> 111190, elapsed: 00:00:02.8499823, speed: 30,120 mb/s

Benchmark name: DeflateStream, binary data
Compression:    size: 111190 ->  41311, elapsed: 00:00:24.4338982, speed: 4,551 mb/s
Decompression:  size:  41311 -> 111190, elapsed: 00:00:04.1819262, speed: 9,878 mb/s

Benchmark name: MiniLZO, binary data
Compression:    size: 111190 ->  58049, elapsed: 00:00:04.9801071, speed: 22,327 mb/s
Decompression:  size:  58049 -> 111190, elapsed: 00:00:02.3217732, speed: 25,002 mb/s

Benchmark name: iROLZ, binary data
Compression:    size: 111190 ->  43173, elapsed: 00:00:27.9434671, speed: 0,398 mb/s
Decompression:  size:  43173 -> 111190, elapsed: 00:00:26.7576039, speed: 0,161 mb/s

Benchmark name: LZF, textual data
Compression:    size: 100000 ->  71216, elapsed: 00:00:03.2371836, speed: 30,891 mb/s
Decompression:  size:  71216 -> 100000, elapsed: 00:00:01.5770957, speed: 45,156 mb/s

Benchmark name: LZ4, textual data
Compression:    size: 100000 ->  69379, elapsed: 00:00:01.6759052, speed: 59,669 mb/s
Decompression:  size:  69379 -> 100000, elapsed: 00:00:00.7203209, speed: 96,317 mb/s

Benchmark name: QuickLZ, textual data
Compression:    size: 100000 ->  59508, elapsed: 00:00:01.9237337, speed: 51,982 mb/s
Decompression:  size:  59508 -> 100000, elapsed: 00:00:02.0248642, speed: 29,389 mb/s

Benchmark name: DeflateStream, textual data
Compression:    size: 100000 ->  38925, elapsed: 00:00:28.0695938, speed: 3,563 mb/s
Decompression:  size:  38925 -> 100000, elapsed: 00:00:03.5624840, speed: 10,926 mb/s

Benchmark name: MiniLZO, textual data
Compression:    size: 100000 ->  64721, elapsed: 00:00:04.7377885, speed: 21,107 mb/s
Decompression:  size:  64721 -> 100000, elapsed: 00:00:02.1345576, speed: 30,321 mb/s

Benchmark name: iROLZ, textual data
Compression:    size: 100000 ->  37424, elapsed: 00:00:26.9814807, speed: 0,371 mb/s
Decompression:  size:  37424 -> 100000, elapsed: 00:00:22.9918447, speed: 0,163 mb/s

Benchmark name: LZF, uncompressible data
Compression:    size: 100066 -> 103187, elapsed: 00:00:04.1211411, speed: 24,281 mb/s
Decompression:  size: 103187 -> 100066, elapsed: 00:00:00.6422561, speed: 160,663 mb/s

Benchmark name: LZ4, uncompressible data
Compression:    size: 100066 -> 100460, elapsed: 00:00:00.2278921, speed: 439,094 mb/s
Decompression:  size: 100460 -> 100066, elapsed: 00:00:00.4321636, speed: 232,458 mb/s

Benchmark name: QuickLZ, uncompressible data
Compression:    size: 100066 -> 100075, elapsed: 00:00:01.5112337, speed: 66,215 mb/s
Decompression:  size: 100075 -> 100066, elapsed: 00:00:00.1508237, speed: 663,523 mb/s

Benchmark name: DeflateStream, uncompressible data
Compression:    size: 100066 -> 100101, elapsed: 00:00:11.6235236, speed: 8,609 mb/s
Decompression:  size: 100101 -> 100066, elapsed: 00:00:00.2153021, speed: 464,933 mb/s

Benchmark name: MiniLZO, uncompressible data
Compression:    size: 100066 -> 100468, elapsed: 00:00:08.5771120, speed: 11,667 mb/s
Decompression:  size: 100468 -> 100066, elapsed: 00:00:00.6379958, speed: 157,474 mb/s

Benchmark name: iROLZ, uncompressible data
Compression:    size: 100066 -> 101438, elapsed: 00:00:30.1338656, speed: 0,332 mb/s
Decompression:  size: 101438 -> 100066, elapsed: 00:00:33.7285467, speed: 0,301 mb/s
```